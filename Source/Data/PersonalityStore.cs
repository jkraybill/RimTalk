using System.Collections.Generic;
using System.Linq;
using RimWorld.Planet;
using Verse;

namespace RimTalk.Data;

/// <summary>
/// Storage and access for expanded pawn personalities. rim-universe #51.
///
/// Same pattern as GoalStore: lives on the world component, keyed by pawnId,
/// and the static accessor handles the null-propagation.
/// </summary>
public static class PersonalityStore
{
    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    static List<PawnPersonality> All => Comp?.PersonalityEntries ?? new List<PawnPersonality>();

    /// <summary>Get the personality for this pawn, or null if not yet generated.</summary>
    public static PawnPersonality Get(Pawn pawn) =>
        pawn == null ? null : All.FirstOrDefault(p => p?.PawnId == pawn.thingIDNumber);

    /// <summary>Whether this pawn has a personality expansion yet.</summary>
    public static bool Has(Pawn pawn) => Get(pawn) != null;

    /// <summary>Record a newly generated personality.</summary>
    public static void Record(PawnPersonality personality)
    {
        var comp = Comp;
        if (comp == null || personality == null) return;

        // Remove any existing entry for this pawn (shouldn't happen, but defensive)
        comp.PersonalityEntries.RemoveAll(p => p?.PawnId == personality.PawnId);
        comp.PersonalityEntries.Add(personality);
    }
}
