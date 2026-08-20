using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Data;

/// <summary>What survived the budget, and what had to be compressed to make room.</summary>
public class FittedHistory
{
    /// <summary>
    /// Everything older than the budget allowed, as one line. Null when nothing was
    /// collapsed. The caller prepends it to the current user message — which is where
    /// "earlier" belongs anyway, and which keeps strict user/AI alternation intact.
    /// </summary>
    public string Digest;

    /// <summary>
    /// Starts with a user turn, alternates, ends with an AI turn, and fits the budget.
    /// </summary>
    public List<(Role, string)> Turns = new();

    /// <summary>
    /// The whole thing as messages to send: the digest folded into the front of the
    /// first user turn, so nothing is lost and nothing leads with an assistant turn.
    ///
    /// Folded rather than prepended as its own message because PromptManager forces a
    /// break at the history boundary, so a separate leading turn would not merge and
    /// the model would see two user messages in a row across that seam.
    /// </summary>
    public List<(Role, string)> AsMessages()
    {
        var messages = new List<(Role, string)>(Turns);
        if (string.IsNullOrWhiteSpace(Digest)) return messages;

        if (messages.Count == 0) return new List<(Role, string)> { (Role.User, Digest) };

        messages[0] = (messages[0].Item1, Digest + "\n\n" + messages[0].Item2);
        return messages;
    }
}

/// <summary>
/// How much of a pawn's history to send, and what to do with the rest.
///
/// rim-universe #9. History was capped by message COUNT, at one exchange, and every
/// message over the cap was removed outright. Two problems in one line of code: a
/// long generated reply cost the same as a short one, and what fell off the end was
/// gone.
///
/// The rule here is *collapse, never delete*, which four S166 roundtable reviewers
/// recommended and which Still Life's memory architecture independently arrived at
/// from the opposite direction — a 3B model with an 8K window. Two designs converging
/// is the strongest signal available that it is right.
///
/// The compression is deterministic and needs no second model call: an envelope is
/// two hundred tokens of scene the game can regenerate at will, and a reply is ten
/// tokens that exist nowhere else. So the envelopes go and the speech stays.
///
/// Pure: source-linked into the test project and run.
/// </summary>
public static class TalkMemory
{
    /// <summary>
    /// Four characters to a token. Crude, provider-independent, and biased slightly
    /// high on English prose, which is the safe direction for a budget.
    /// </summary>
    public const int CharsPerToken = 4;

    public static int Tokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        return (text.Length + CharsPerToken - 1) / CharsPerToken;   // anything costs 1
    }

    public static int Tokens(IEnumerable<(Role, string)> turns) =>
        (turns ?? Enumerable.Empty<(Role, string)>()).Sum(t => Tokens(t.Item2));

    /// <summary>
    /// Newest exchanges verbatim while they fit; everything older compressed into
    /// <see cref="FittedHistory.Digest"/>.
    /// </summary>
    public static FittedHistory Fit(IReadOnlyList<(Role, string)> history, int tokenBudget)
    {
        var result = new FittedHistory();
        var all = (history ?? new List<(Role, string)>())
            .Where(t => !string.IsNullOrWhiteSpace(t.Item2)).ToList();
        if (all.Count == 0) return result;

        // Walk backwards in whole exchanges. A lone trailing user turn is an envelope
        // whose reply never arrived; it is scenery and the caller is about to send a
        // fresher one, so it never leads the kept block.
        // Marked rather than sliced. What is kept is NOT always a contiguous run off the
        // end: a stray turn in the middle is skipped, and treating the digest as "the
        // first N" would drop it — deleting exactly what this class exists not to
        // delete. Found by sabotaging the pairing check and discovering no test failed.
        var keep = new bool[all.Count];
        var used = 0;
        var i = all.Count - 1;

        while (i >= 1)
        {
            // An exchange is a user turn and the AI turn that answered it.
            if (all[i].Item1 != Role.AI || all[i - 1].Item1 != Role.User) { i--; continue; }

            var cost = Tokens(all[i].Item2) + Tokens(all[i - 1].Item2);
            if (used + cost > tokenBudget) break;

            keep[i - 1] = keep[i] = true;
            used += cost;
            i -= 2;
        }

        for (var k = 0; k < all.Count; k++)
            if (keep[k]) result.Turns.Add(all[k]);

        // Everything else. Not dropped — this is the whole point.
        result.Digest = Digest(all.Where((_, k) => !keep[k]));
        return result;
    }

    /// <summary>
    /// The spoken half of some exchanges, as one line. Null when there was no speech
    /// in them — a run of envelopes with no replies compresses to nothing, correctly.
    /// </summary>
    public static string Digest(IEnumerable<(Role, string)> turns)
    {
        var said = (turns ?? Enumerable.Empty<(Role, string)>())
            .Where(t => t.Item1 == Role.AI && !string.IsNullOrWhiteSpace(t.Item2))
            .Select(t => t.Item2.Trim())
            .ToList();
        if (said.Count == 0) return null;

        return "Earlier, these were said:\n" + string.Join("\n", said.Select(s => "  " + s));
    }
}
