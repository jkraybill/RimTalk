using System.Collections.Generic;
using System.Linq;
using RimTalk.Narrative;

namespace RimTalk.Prose;

/// <summary>What these two remember about each other, reduced to primitives.</summary>
public class PairFacts
{
    public string AName = "";
    public string BName = "";

    /// <summary>How many separate occasions these two have talked. #30's title.</summary>
    public int TimesMet;

    /// <summary>Already worded by <see cref="NarrativeMath.Elapsed"/> — "4 days ago".</summary>
    public string LastSpokeAgo;

    /// <summary>Their last exchange, oldest first, already "Name: line".</summary>
    public List<string> LastExchange = new();

    /// <summary>
    /// What has happened to the colony since. JK's example is not a callback, it is a
    /// callback plus a delta: they last complained about the rice, a pig has been
    /// killed since, and the line only works because both halves are present.
    /// </summary>
    public List<string> Since = new();
}

/// <summary>
/// rim-universe #30, the render.
///
/// Two blocks, and they are not interchangeable. The prose block is a scene fact and
/// #23 and #34 both measured what happens to those on their own: mentioned 0-18% of
/// the time, because a fact stated in the scene reads as furniture. The clause is
/// appended to the instruction, which is the sentence the model is actually obeying,
/// and that is where 90% came from. Each thing is said in exactly one place — saying
/// it in both reached the same rate and cost a third of the variety.
/// </summary>
public static class PairText
{
    /// <summary>
    /// Four lines. Enough for an exchange to have a shape — a remark and an answer,
    /// twice — and short enough that a pair callback cannot crowd out the scene.
    /// </summary>
    public const int MaxExchangeLines = 4;

    /// <summary>
    /// Three. The delta is a reminder, not a newspaper: past three items the model
    /// starts summarising the list instead of talking about one thing in it.
    /// </summary>
    public const int MaxSinceItems = 3;

    /// <summary>The scene block. Null when these two have nothing between them yet.</summary>
    public static string Compose(PairFacts f)
    {
        if (f == null) return null;

        var exchange = Clean(f.LastExchange).TakeLast(MaxExchangeLines).ToList();
        var since = Clean(f.Since).Take(MaxSinceItems).ToList();
        if (exchange.Count == 0 && since.Count == 0) return null;

        var parts = new List<string>();
        var who = $"{f.AName} and {f.BName}".Trim();

        // Set off by commas so it is an aside about the pair rather than a clause the
        // sentence has to be rebuilt around.
        var often = f.TimesMet >= PairMath.ManyTimes ? ", who have talked many times before," : "";
        var ago = string.IsNullOrWhiteSpace(f.LastSpokeAgo) ? null : f.LastSpokeAgo.Trim();

        if (exchange.Count > 0)
        {
            var when = ago == null ? "last spoke" : $"last spoke {ago}";
            parts.Add($"{who}{often} {when}:\n" + string.Join("\n", exchange.Select(l => "  " + l)));
        }
        else if (ago != null)
        {
            // No remembered words, but we know when. Saying "last spoke" here would
            // promise an exchange the prompt cannot show, and the model will invent
            // one to fill the gap.
            parts.Add($"{who}{often} last met {ago}.");
        }
        else if (often.Length > 0)
        {
            parts.Add($"{who} have talked many times before.");
        }

        if (since.Count > 0)
            parts.Add($"Since then: {ProseWords.Join(since)}.");

        return parts.Count == 0 ? null : string.Join("\n", parts);
    }

    /// <summary>
    /// The instruction clause. No leading space — the caller joins it, the same way
    /// every other conditional clause on the instruction is joined.
    /// </summary>
    public static string Clause(PairFacts f)
    {
        if (f == null) return null;

        var hasExchange = Clean(f.LastExchange).Any();
        var hasSince = Clean(f.Since).Any();

        if (hasExchange && hasSince)
            return "Pick up from what they last said to each other, and what has happened since.";
        if (hasSince)
            return "What has happened to this place since they last met comes into it.";
        if (hasExchange)
            return "Pick up from what they last said to each other.";
        return null;
    }

    static IEnumerable<string> Clean(IEnumerable<string> items) =>
        (items ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(System.StringComparer.OrdinalIgnoreCase);
}
