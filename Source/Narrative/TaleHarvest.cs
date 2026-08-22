using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Logger = RimTalk.Util.Logger;

namespace RimTalk.Narrative;

/// <summary>
/// The generalised event harvest, in one patch.
///
/// RimWorld already decides what is memorable and records a Tale for it — that is
/// what the art-description system reads — and every one of them goes through
/// <see cref="TaleRecorder.RecordTale"/>. Patching there covers sixty event classes
/// at once; the roadmap's "one class at a time" would have cost sixty patches.
///
/// This file reads the game and nothing else. The whitelist and the wording are in
/// <see cref="TaleClause"/>, which runs in the test project.
/// </summary>
[HarmonyPatch(typeof(TaleRecorder), nameof(TaleRecorder.RecordTale))]
public static class TaleRecorder_Narrative
{
    [HarmonyPostfix]
    public static void Postfix(TaleDef def, object[] args)
    {
        try
        {
            var kind = def?.defName;
            if (!TaleClause.IsHarvested(kind)) return;
            if (Find.World == null) return;

            var people = (args ?? System.Array.Empty<object>())
                .OfType<Pawn>()
                .Where(p => p != null)
                .ToList();

            // A raider's wedding is not colony news. Colony-wide tales carry no
            // subject at all, so they are exempt from the test rather than failing it.
            var subject = people.FirstOrDefault(IsColonyPerson);
            if (subject == null && RequiresAPerson(kind)) return;

            var other = people.FirstOrDefault(p => p != subject && (p.RaceProps?.Humanlike ?? false));
            var detail = DetailOf(args, subject, other, people);

            var clause = TaleClause.For(kind, subject?.LabelShort, other?.LabelShort, detail);
            if (clause == null) return;

            var tick = GenTicks.TicksGame;
            var key = TaleClause.DedupeKey(kind, subject?.LabelShort, detail);
            if (Chronicle.Record(tick, kind, key, clause))
                Logger.Debug($"Chronicle: {clause}");
        }
        catch (System.Exception ex)
        {
            // Never let narrative bookkeeping break the game's own tale recording.
            Logger.Warning($"Tale harvest failed for {def?.defName}: {ex.Message}");
        }
    }

    /// <summary>
    /// The three colony-wide tales are about the settlement, not about whoever
    /// happened to trigger the record. Everything else needs a colonist in it.
    /// </summary>
    static bool RequiresAPerson(string kind) =>
        kind != "AttendedParty" && kind != "AttendedConcert" && kind != "TileSettled";

    static bool IsColonyPerson(Pawn p) =>
        (p.RaceProps?.Humanlike ?? false) &&
        (p.IsFreeColonist || p.IsSlaveOfColony || p.IsPrisonerOfColony);

    /// <summary>
    /// The thing the tale is about, as a label.
    ///
    /// An ANIMAL FIRST, and this is the whole reason the extraction is not one LINQ
    /// line: Hunted, TamedAnimal and BondedWithAnimal all pass the animal as a Pawn,
    /// so a naive "first non-Pawn argument" finds the weapon def instead of the boar
    /// and the colony remembers that Kess hunted a bolt-action rifle.
    /// </summary>
    static string DetailOf(object[] args, Pawn subject, Pawn other, List<Pawn> people)
    {
        var animal = people.FirstOrDefault(p => p != subject && p != other && !(p.RaceProps?.Humanlike ?? false));
        if (animal != null) return animal.def?.label ?? animal.LabelShort;

        foreach (var a in args ?? System.Array.Empty<object>())
        {
            switch (a)
            {
                case Pawn: continue;
                case Def d when !string.IsNullOrWhiteSpace(d.label): return d.label;
                case Thing t when !string.IsNullOrWhiteSpace(t.def?.label): return t.def.label;
            }
        }
        return null;
    }
}
