using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using RimTalk.Narrative;
using Verse;

namespace RimTalk.Goals;

/// <summary>
/// Who wants what, and what they have already got. rim-universe #28.
///
/// Scribed on the world component, like everything else that has to outlive a save.
/// One active goal per pawn; the resolved ones stay, bounded, because they are what
/// the cooldown reads and the only record that anything was ever achieved.
/// </summary>
public static class GoalStore
{
    /// <summary>
    /// Two hundred. Roughly a goal a fortnight each for a dozen colonists over a
    /// decade — a lot of colony, and still nothing next to a save file.
    /// </summary>
    public const int MaxEntries = 200;

    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    static List<GoalEntry> All => Comp?.GoalEntries ?? new List<GoalEntry>();

    /// <summary>The goal this pawn is carrying, or null.</summary>
    public static GoalEntry Active(Pawn pawn) =>
        pawn == null ? null
        : All.FirstOrDefault(e => e != null && e.PawnId == pawn.thingIDNumber && e.State == GoalState.Active);

    /// <summary>
    /// Whether this pawn could take a new goal on.
    ///
    /// Deliberately does NOT ask whether anything is wrong with the colony — that
    /// costs a map read, and this runs inside the cache refresh over every pawn.
    /// GoalService does the expensive half once it knows there is a slot to fill.
    /// </summary>
    public static bool HasRoom(Pawn pawn) => pawn != null && Active(pawn) == null;

    /// <summary>
    /// Which of these kinds this pawn is not still cooling off from.
    ///
    /// A pawn who has just reached a week of food wanting two weeks is the needs-bar
    /// reading of a goal rather than the person reading, and it is what makes a
    /// feature like this feel mechanical within an hour of play.
    /// </summary>
    public static List<GoalKind> Allowed(Pawn pawn, IEnumerable<GoalKind> kinds, int now)
    {
        var candidates = (kinds ?? Enumerable.Empty<GoalKind>()).ToList();
        if (pawn == null || candidates.Count == 0) return candidates;

        var cooling = All
            .Where(e => e != null && e.PawnId == pawn.thingIDNumber && e.State != GoalState.Active)
            .Where(e => now - e.ResolvedTick < GoalMath.CooldownTicks)
            .Select(e => e.Kind)
            .ToHashSet();

        return candidates.Where(k => !cooling.Contains(k)).ToList();
    }

    public static void Record(GoalEntry entry)
    {
        var comp = Comp;
        if (comp == null || entry == null) return;

        comp.GoalEntries.Add(entry);
        NarrativeMath.Trim(comp.GoalEntries, MaxEntries);
    }

    /// <summary>Move a goal out of Active, once. Returns whether anything changed.</summary>
    public static bool Resolve(GoalEntry entry, GoalState state, int now)
    {
        if (entry == null || entry.State != GoalState.Active || state == GoalState.Active) return false;
        entry.State = state;
        entry.ResolvedTick = now;
        return true;
    }

    /// <summary>
    /// Every active goal, for the daily evaluation. A copy, because resolving walks it.
    /// </summary>
    public static List<GoalEntry> ActiveEntries() =>
        All.Where(e => e != null && e.State == GoalState.Active).ToList();
}
