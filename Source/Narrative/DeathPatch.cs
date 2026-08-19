using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimTalk.Service;
using RimWorld;
using Verse;
using Logger = RimTalk.Util.Logger;

namespace RimTalk.Narrative;

/// <summary>
/// Harvests a colonist death into the narrative store. rim-universe #21 slice 1.
///
/// Witnesses have to be collected in the PREFIX: by the time Kill returns the pawn
/// is dead, usually despawned, and Map is gone — so a postfix-only patch records an
/// event nobody saw.
/// </summary>
[HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
public static class Pawn_Kill_Narrative
{
    // Keyed by the dying pawn so nested or simultaneous deaths cannot cross-talk.
    static readonly Dictionary<int, List<Pawn>> PendingWitnesses = new();
    static readonly Dictionary<int, string> PendingDetail = new();

    [HarmonyPrefix]
    public static void Prefix(Pawn __instance)
    {
        try
        {
            if (!IsWorthRemembering(__instance)) return;

            PendingWitnesses[__instance.thingIDNumber] =
                PawnSelector.GetAllNearByPawns(__instance)
                    .Where(p => p != null && !p.Dead && p.RaceProps.Humanlike)
                    .ToList();

            PendingDetail[__instance.thingIDNumber] = DescribeCause(__instance);
        }
        catch (System.Exception ex)
        {
            // Never let narrative bookkeeping break a death.
            Logger.Warning($"Narrative death prefix failed for {__instance?.LabelShort}: {ex.Message}");
        }
    }

    [HarmonyPostfix]
    public static void Postfix(Pawn __instance)
    {
        try
        {
            if (__instance == null) return;
            var id = __instance.thingIDNumber;
            if (!PendingWitnesses.TryGetValue(id, out var witnesses)) return;

            PendingWitnesses.Remove(id);
            PendingDetail.TryGetValue(id, out var detail);
            PendingDetail.Remove(id);

            var e = new NarrativeEvent(GenTicks.TicksGame, "death", __instance, detail);
            foreach (var w in witnesses) e.AddWitness(w);

            NarrativeStore.Record(e);
            NarrativeLetters.AnnounceDeath(__instance, e);

            Logger.Debug($"Narrative: recorded death of {e.Subject}, {e.Witnesses.Count} witness(es)");
        }
        catch (System.Exception ex)
        {
            Logger.Warning($"Narrative death postfix failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Colony-relevant humanlikes only. A raider dying in a firefight is not a thing
    /// the colony remembers for years, and recording every kill would fill a bounded
    /// store with noise in a single raid.
    /// </summary>
    static bool IsWorthRemembering(Pawn p)
    {
        if (p == null || p.Dead || !p.RaceProps.Humanlike) return false;
        if (p.Map == null) return false;
        return p.IsFreeColonist || p.IsSlaveOfColony || p.IsPrisonerOfColony
               || (p.Faction != null && p.Faction.IsPlayer);
    }

    static string DescribeCause(Pawn p)
    {
        if (p.health?.hediffSet == null) return null;
        var worst = p.health.hediffSet.hediffs
            .Where(h => h.Visible && h.def != null)
            .OrderByDescending(h => h.Severity)
            .FirstOrDefault();
        return worst?.def?.label;
    }
}
