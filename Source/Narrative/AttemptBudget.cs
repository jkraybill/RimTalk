using System.Collections.Generic;

namespace RimTalk.Narrative;

/// <summary>
/// A per-pawn attempt budget for a generation that "retries" by simply staying on a
/// list of work to do. rim-universe S169.
///
/// Both narrative generations do that, and it has no natural end: one colonist took
/// 25 consecutive API calls and stored nothing, because a parser bug meant no reply
/// could ever be accepted. Every attempt is a paid request holding the single global
/// in-flight slot, so an endlessly-refusing generation does not merely make noise —
/// it starves ordinary conversation for as long as the colony runs.
///
/// No Verse types on purpose: keyed on thingIDNumber rather than Pawn, so the whole
/// thing is testable for real rather than inspected.
/// </summary>
public class AttemptBudget
{
    /// <summary>
    /// Three. A refusal is usually the model rather than the pawn, and two in a row
    /// is bad luck often enough to be worth one more.
    /// </summary>
    public const int MaxAttempts = 3;

    readonly Dictionary<int, int> _failures = new();

    public int Count(int id) => _failures.TryGetValue(id, out var n) ? n : 0;

    public bool Exhausted(int id) => Count(id) >= MaxAttempts;

    /// <summary>Record one unusable result and return the new count.</summary>
    public int Failed(int id)
    {
        var n = Count(id) + 1;
        _failures[id] = n;
        return n;
    }

    /// <summary>
    /// Forget this pawn's failures. Called on success, because the budget is meant to
    /// stop a stuck generation, not to ration a working one — a pawn who succeeds
    /// after two refusals starts clean the next time their topics go stale.
    /// </summary>
    public void Succeeded(int id) => _failures.Remove(id);

    /// <summary>
    /// Cleared on load and on new game. thingIDNumber is per-save, so a count carried
    /// across a load would silence a different colony's colonist, and a reload is
    /// meant to be the retry.
    /// </summary>
    public void Clear() => _failures.Clear();

    /// <summary>
    /// The tail of the log line. A give-up is never silent: a budget that stops asking
    /// without saying so reads exactly like a feature that was never wired up.
    /// </summary>
    public static string Verdict(int failures) =>
        failures >= MaxAttempts
            ? $"That was attempt {failures} of {MaxAttempts}; not asking again for this pawn until the game is reloaded."
            : $"Attempt {failures} of {MaxAttempts}. Will retry.";
}
