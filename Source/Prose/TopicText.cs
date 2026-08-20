using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>
/// The things a pawn brings up when nothing in particular is happening.
///
/// rim-universe #44. Ordinary conversation was seeded by RimWorld's random noun
/// generator: BubblePatch hands the vanilla sentence — "Jackalope said something
/// about crags to Jesse" — to the model verbatim as the subject. That is the ONLY
/// topic source unprompted dialogue has; everything else in the prompt is context.
/// So every colonist conversation in the game was about hedgehogs, or crags, or
/// vomiting, with no connection to the pawn, the other pawn, or anything that had
/// happened.
///
/// S167 measured the axis this sits on, on a different question: one paragraph of
/// SPECIFIC character material took repeated lines from 25% to 9.8% and prompt
/// lifting from 17.6% to 3.9%. A random noun is specific in form and empty in
/// substance, which is the worst of both.
///
/// Pure: the prompt, the cleaning and the gate all run in the test project.
/// </summary>
public static class TopicText
{
    /// <summary>
    /// Five. The same number #9's memory work landed on, for the same reason: enough
    /// that a pawn does not visibly cycle, few enough that the list stays theirs.
    /// </summary>
    public const int Count = 5;

    /// <summary>
    /// Short. These are dropped into a sentence where a single noun used to sit, and
    /// they have to survive being read aloud in a speech bubble.
    /// </summary>
    public const int MaxLength = 90;

    public static string Prompt(string profile, int count = Count)
    {
        return
            $"Give {count} things this person brings up when there is nothing much " +
            "happening.\n\n" +
            "Not topics in the abstract — the specific things THEY would raise. A " +
            "grudge, a habit, something they miss, an opinion they hold too strongly, " +
            "a piece of their own history. Each one a short phrase that could finish " +
            "the sentence \"they said something about ___\".\n\n" +
            "They do not know how they came to be on this planet and never will, so " +
            "nothing about arriving, ships, or where they came from most recently.\n\n" +
            "Reply with JSON and nothing else: {\"topics\": [\"...\", \"...\"]}\n\n" +
            "[Who they are]\n" + (profile ?? "").Trim();
    }

    /// <summary>
    /// Clean a generated list, dropping anything unusable. Returns an empty list
    /// rather than null: the caller falls back to the vanilla string, which is a
    /// working behaviour and not an error.
    /// </summary>
    public static List<string> Accept(IEnumerable<string> generated)
    {
        var kept = new List<string>();
        foreach (var raw in generated ?? Enumerable.Empty<string>())
        {
            var t = Clean(raw);
            if (t == null) continue;
            if (kept.Any(k => k.Equals(t, System.StringComparison.OrdinalIgnoreCase))) continue;
            kept.Add(t);
            if (kept.Count >= Count) break;
        }
        return kept;
    }

    static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var t = raw.Replace("**", "").Replace("\r", " ").Replace("\n", " ").Trim();
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s{2,}", " ");
        t = t.Trim('"', '“', '”', '-', '*', '.', ' ');

        if (t.Length == 0 || t.Length > MaxLength) return null;

        // R8. The arrival check has one home; a topic about the wreck is the same
        // false canon as an arrival log about it.
        if (ArrivalText.ClaimsArrival(t)) return null;

        return t;
    }

    /// <summary>
    /// The sentence that replaces the vanilla one, so the social log reads as a real
    /// exchange rather than a dictionary word. Same shape as RimWorld's own phrasing,
    /// because the row sits among vanilla rows.
    /// </summary>
    public static string LogLine(string speaker, string listener, string topic)
    {
        if (string.IsNullOrWhiteSpace(speaker) || string.IsNullOrWhiteSpace(topic)) return null;
        return string.IsNullOrWhiteSpace(listener) || listener == speaker
            ? $"{speaker} said something about {topic}."
            : $"{speaker} said something about {topic} to {listener}.";
    }
}
