using System.Linq;
using RimWorld;
using Verse;

namespace RimTalk.Util;

/// <summary>
/// Reads game state and turns it into the two halves of the Scale Gap.
/// rim-universe #35.
///
/// Split from Describer on purpose: Describer has no RimWorld dependency and is
/// source-linked into the test project, so the band boundaries can be tested for
/// real. Everything here needs a live game and cannot be. Keeping the line between
/// them sharp is what keeps the testable part testable.
/// </summary>
public static class ScaleDescriber
{
    /// <summary>How big is what is happening.</summary>
    public static string Situation(Map map)
    {
        if (map == null) return null;

        var danger = map.dangerWatcher?.DangerRating ?? StoryDanger.None;
        int dangerLevel = danger == StoryDanger.High ? 2 : danger == StoryDanger.Low ? 1 : 0;

        int downed = 0;
        var colonists = map.mapPawns?.FreeColonistsSpawned;
        if (colonists != null)
            foreach (var c in colonists)
                if (c.Downed) downed++;

        int dead = map.mapPawns?.AllPawnsSpawned?.Count(p => p.Dead) ?? 0;

        return Describer.Situation(dangerLevel, downed, dead,
            map.wealthWatcher?.WealthTotal ?? 0f, map.IsPlayerHome);
    }

    /// <summary>
    /// How big are this pawn's concerns, by role, until Needs (#30) and Goals (#28)
    /// exist to answer it properly.
    ///
    /// Tolkien splits scale across characters: Elrond does geopolitics, Sam does
    /// potatoes, and neither does both. RimWorld hands us the roles for free.
    /// </summary>
    public static string ConcernFor(Pawn pawn)
    {
        if (pawn == null) return "nothing in particular";

        if (pawn.IsBaby() || (pawn.ageTracker?.AgeBiologicalYears ?? 99) < 13)
            return "one small thing, intensely and probably wrongly";
        if (pawn.IsPrisoner || pawn.IsSlave)
            return "the door, the guard, and getting through tomorrow";
        if (!string.IsNullOrWhiteSpace(pawn.GetTitle()))
            return "the colony, the faction, and how this will be remembered";

        var top = pawn.skills?.skills?.OrderByDescending(sk => sk.Level).FirstOrDefault();
        if (top != null && top.Level >= 10)
        {
            // skillLabel, not label. Vanilla SkillDefs carry <skillLabel> and no
            // <label>, so SkillDef.label is null for all twelve and this shipped
            // "whether the  work will hold" with a hole in it. Same bug as the profile
            // want line, second code path — found by grepping for the other one.
            var label = top.def.skillLabel ?? top.def.label ?? top.def.defName?.ToLower();
            if (!string.IsNullOrWhiteSpace(label))
                return $"whether the {label} work will hold";
        }

        return "the meal, the bed, and the weather";
    }
}
