using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using Verse;
using Logger = RimTalk.Util.Logger;

namespace RimTalk.Narrative;

/// <summary>
/// The Archive as a second event source. rim-universe S169.
///
/// Deliberately a separate patch from RimTalk.Patch.ArchivePatch, which hangs off the
/// same method. That one turns a letter into a REQUEST — somebody says something
/// about it now. This one turns it into HISTORY — the colony remembers it happened.
/// Two different lifetimes and two different failure modes, so they do not share a
/// method however convenient the hook is.
///
/// This file reads the game and nothing else; the wording and the whitelist are in
/// <see cref="ArchiveClause"/>, which runs in the test project.
/// </summary>
[HarmonyPatch(typeof(Archive), nameof(Archive.Add))]
public static class ArchiveHarvest
{
    [HarmonyPostfix]
    public static void Postfix(IArchivable archivable)
    {
        try
        {
            if (archivable == null || Find.World == null) return;

            var letterDef = (archivable as Letter)?.def?.defName;
            if (!ArchiveClause.IsHarvested(letterDef)) return;

            var clause = ArchiveClause.For(archivable.ArchivedLabel);
            if (clause == null) return;

            var people = People(archivable);
            var subject = people.ElementAtOrDefault(0);
            var other = people.ElementAtOrDefault(1);

            var tick = GenTicks.TicksGame;
            var known = Witness.Around(people, subject);

            // A colony-wide event nobody was standing next to is still known by
            // everyone — a raid is not a secret. Only events with a named person keep
            // the proximity rule, because who saw WHAT HAPPENED TO SOMEBODY is the
            // distinction gossip turns on.
            if (subject == null) known = AllColonists();

            if (Chronicle.Record(tick, $"Letter_{letterDef}", ArchiveClause.DedupeKey(letterDef, clause),
                                 clause, subject?.thingIDNumber ?? 0, other?.thingIDNumber ?? 0, known))
                Logger.Debug($"Chronicle (archive): {clause}");
        }
        catch (System.Exception ex)
        {
            // Never let narrative bookkeeping break the game's own notifications.
            Logger.Warning($"Archive harvest failed: {ex.Message}");
        }
    }

    /// <summary>
    /// The colonists this letter is about, in the order the letter points at them.
    /// Only the player's own: a raider named in a threat letter is scenery, and
    /// writing them in as a gossip subject is #47 wearing a different hat.
    /// </summary>
    static List<Pawn> People(IArchivable archivable)
    {
        var targets = archivable.LookTargets;
        if (targets is not { Any: true }) return new List<Pawn>();

        return targets.targets
            .Select(t => t.Thing as Pawn)
            .Where(p => p != null && !p.Dead && (p.RaceProps?.Humanlike ?? false)
                        && (p.IsFreeColonist || p.IsSlaveOfColony || p.IsPrisonerOfColony))
            .Distinct()
            .Take(2)
            .ToList();
    }

    static List<int> AllColonists() =>
        Find.Maps?
            .SelectMany(m => m.mapPawns?.FreeColonistsSpawned ?? new List<Pawn>())
            .Where(p => p != null && !p.Dead)
            .Select(p => p.thingIDNumber)
            .Distinct()
            .ToList()
        ?? new List<int>();
}
