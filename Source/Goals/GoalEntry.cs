using Verse;

namespace RimTalk.Goals;

/// <summary>
/// One goal, as it is stored. rim-universe #28.
///
/// Resolved entries are KEPT rather than deleted. They are what the cooldown reads —
/// a pawn who just got a week of food should not immediately want two — and they are
/// the only record that a goal was ever met, which is the half of this feature that
/// is worth anything to a player.
///
/// The name goes in beside the id, for the same reason the arrival log carries one:
/// this outlives the person in it.
/// </summary>
public class GoalEntry : IExposable
{
    public int PawnId;
    public string PawnName;

    public GoalKind Kind;
    public GoalState State = GoalState.Active;

    /// <summary>The model's prose. Shown and spoken; never the source of truth.</summary>
    public string Statement;

    /// <summary>The number the predicate tests against, set by the game at birth.</summary>
    public float Target;

    public int SetTick;
    public int ExpiryTick;

    /// <summary>When it stopped being Active. 0 while it still is.</summary>
    public int ResolvedTick;

    /// <summary>
    /// Whether the mood has already been granted. A goal is evaluated on a schedule
    /// and Met is sticky, so without this the daily pass would hand out the thought
    /// again every day for the rest of the save.
    /// </summary>
    public bool Rewarded;

    public GoalEntry() { }

    public GoalEntry(Pawn pawn, GoalKind kind, string statement, float target, int now, int span)
    {
        PawnId = pawn?.thingIDNumber ?? 0;
        PawnName = pawn?.LabelShort;
        Kind = kind;
        Statement = statement;
        Target = target;
        SetTick = now;
        ExpiryTick = now + span;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref PawnId, "pawnId");
        Scribe_Values.Look(ref PawnName, "pawnName");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref State, "state", GoalState.Active);
        Scribe_Values.Look(ref Statement, "statement");
        Scribe_Values.Look(ref Target, "target");
        Scribe_Values.Look(ref SetTick, "setTick");
        Scribe_Values.Look(ref ExpiryTick, "expiryTick");
        Scribe_Values.Look(ref ResolvedTick, "resolvedTick");
        Scribe_Values.Look(ref Rewarded, "rewarded");
    }
}
