using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Narrative;

/// <summary>One thing that should be true, and whether it was.</summary>
public class Invariant
{
    public string Name;
    public bool Ok;
    public string Detail;

    /// <summary>
    /// True when there was not enough evidence to judge. Distinct from Ok on purpose:
    /// a check that quietly passes on an empty colony is how a broken feature gets
    /// reported as healthy for three sessions running.
    /// </summary>
    public bool Undecided;
}

/// <summary>
/// The things that must hold, checked in the running game. rim-universe S169.
///
/// JK asked whether a single debugging layer that could diagnose anything would help.
/// The honest answer is that data availability was never the bottleneck — Player.log
/// already held every bug found this session — and a layer that logs everything
/// mostly grows the haystack. What was missing each time was knowing which question
/// to ask.
///
/// So this is the generalisation that actually compounds: not "log everything" but
/// "state what must be true, and shout when it is not". Every bug found becomes one
/// permanent check, and the snapshot carries the verdicts, so the next run answers
/// last run's questions without anyone remembering to look.
///
/// Pure: game state arrives as counts and the judgements are testable.
/// </summary>
public static class Invariants
{
    /// <summary>
    /// Below this, a colony has not said enough for a share to mean anything, and a
    /// check that fires on three lines is a check that cries wolf.
    /// </summary>
    public const int MinLinesToJudge = 20;

    public static Invariant PairExchangesAreTwoPeople(IEnumerable<int> speakersPerExchange)
    {
        var counts = (speakersPerExchange ?? Enumerable.Empty<int>()).ToList();
        if (counts.Count == 0)
            return Undecided("pairExchangesAreTwoPeople", "no pair memory yet");

        var bad = counts.Count(c => c > 2);
        return new Invariant
        {
            Name = "pairExchangesAreTwoPeople",
            Ok = bad == 0,
            // S169: 10 of 14 pair blocks carried a third person's lines, because every
            // pair in a three-hander was handed the whole transcript.
            Detail = bad == 0
                ? $"{counts.Count} pair(s), none contaminated"
                : $"{bad} of {counts.Count} pair(s) remember a third person's lines",
        };
    }

    public static Invariant MostLinesAreAddressed(int monologue, int total)
    {
        if (total < MinLinesToJudge)
            return Undecided("mostLinesAreAddressed", $"only {total} line(s) so far");

        var pct = 100 * monologue / total;
        return new Invariant
        {
            Name = "mostLinesAreAddressed",
            Ok = pct < 50,
            // S169: `talk.GetTarget() ?? pawn` made every line without an explicit
            // target a monologue, so a real conversation logged as four soliloquies.
            Detail = $"{pct}% of {total} line(s) resolved to Monologue",
        };
    }

    public static Invariant SocialEffectsFire(int applied, int addressedLines)
    {
        if (addressedLines < MinLinesToJudge)
            return Undecided("socialEffectsFire", $"only {addressedLines} addressed line(s) so far");

        return new Invariant
        {
            Name = "socialEffectsFire",
            Ok = applied > 0,
            // They were dead for the mod's whole life behind `pawn != recipient`, and
            // nothing said so. Zero is a finding, not a resting state.
            Detail = $"{applied} applied across {addressedLines} addressed line(s)",
        };
    }

    public static Invariant GossipableEventsHaveWitnesses(int withSubject, int withNobodyKnowing)
    {
        if (withSubject == 0)
            return Undecided("gossipableEventsHaveWitnesses", "no events about a person yet");

        return new Invariant
        {
            Name = "gossipableEventsHaveWitnesses",
            Ok = withNobodyKnowing == 0,
            // An event nobody is recorded as knowing can never be gossiped about, so
            // the feature would look switched off rather than broken.
            Detail = withNobodyKnowing == 0
                ? $"all {withSubject} event(s) about a person have at least one knower"
                : $"{withNobodyKnowing} of {withSubject} event(s) about a person are known by nobody",
        };
    }

    static Invariant Undecided(string name, string why) =>
        new() { Name = name, Ok = true, Undecided = true, Detail = why };
}
