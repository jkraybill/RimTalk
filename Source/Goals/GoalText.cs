using System.Collections.Generic;
using System.Linq;
using RimTalk.Prose;

namespace RimTalk.Goals;

/// <summary>A goal the model proposed and the shortlist allowed.</summary>
public class GoalProposal
{
    public GoalKind Kind;
    public string Statement;
}

/// <summary>
/// rim-universe #28, the prose surface.
///
/// The split is the whole design: **the model proposes and the game evaluates.**
/// Prose is never the source of truth, so this file can be as loose as it likes
/// about wording and as strict as it needs to be about the one field that matters.
///
/// The shortlist is passed IN rather than validated against afterwards. A closed
/// vocabulary enforced at the door is a rejection loop; a closed vocabulary the
/// model is only ever shown three of is a choice, and the difference is whether a
/// refusal costs a second API call.
/// </summary>
public static class GoalText
{
    /// <summary>
    /// Short. This is spoken out loud in a bubble and shown on the bio tab, and a
    /// goal that needs two lines is an arc (#13) wearing a goal's clothes.
    /// </summary>
    public const int MaxLength = 110;

    /// <summary>
    /// What the colony is short of, and what would a person do about it.
    ///
    /// Null when there is nothing wrong. That is a real answer and it must stay
    /// cheap: generating a goal for a comfortable colony is the "nag" failure, and
    /// #28's own mitigation is that goals come from actual deficiencies.
    /// </summary>
    public static string Prompt(string profile, string colony, IReadOnlyList<GoalKind> candidates)
    {
        if (candidates == null || candidates.Count == 0) return null;

        var list = string.Join("\n", candidates.Select(k => $"  {k}  — {Describe(k)}"));

        return
            "This person has decided what they want to see happen here next.\n\n" +
            "Pick ONE of these, and only these:\n\n" + list + "\n\n" +
            "Then write it as one short sentence in their own words — what THEY want, " +
            "in the register they would say it, not a task description. No numbers, no " +
            "deadline, no plan for how.\n\n" +
            "They do not know how they came to be on this planet and never will, so " +
            "nothing about leaving, ships, or getting back.\n\n" +
            "Reply with JSON and nothing else: {\"kind\": \"...\", \"goal\": \"...\"}\n\n" +
            "[Who they are]\n" + (profile ?? "").Trim() + "\n\n" +
            "[Where they are]\n" + (colony ?? "").Trim();
    }

    /// <summary>What each kind means, in the words the pawn would think it in.</summary>
    static string Describe(GoalKind kind) => kind switch
    {
        GoalKind.FoodSecurity  => "there is not enough food put by",
        GoalKind.Medicine      => "there is not enough medicine",
        GoalKind.Shelter       => "somebody here has nowhere proper to sleep",
        GoalKind.Power         => "the power is out",
        GoalKind.BaseDefence   => "this place could not hold off much",
        GoalKind.Companionship => "there are too few people here",
        _ => "",
    };

    /// <summary>
    /// Take the model's answer, or refuse it. Null rather than a repair: a goal is
    /// canon for up to ten days and the pawn will keep saying it, so a half-understood
    /// one is worse than waiting for the next refresh.
    /// </summary>
    public static GoalProposal Accept(string rawKind, string rawStatement,
                                      IReadOnlyList<GoalKind> allowed)
    {
        if (allowed == null || allowed.Count == 0) return null;

        var kind = Match(rawKind, allowed);
        if (kind == null) return null;

        var statement = Clean(rawStatement);
        if (statement == null) return null;

        return new GoalProposal { Kind = kind.Value, Statement = statement };
    }

    static GoalKind? Match(string raw, IReadOnlyList<GoalKind> allowed)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var want = raw.Trim();
        foreach (var k in allowed)
            if (string.Equals(k.ToString(), want, System.StringComparison.OrdinalIgnoreCase))
                return k;
        return null;
    }

    static string Clean(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var t = raw.Replace("**", "").Replace("\r", " ").Replace("\n", " ").Trim();
        t = System.Text.RegularExpressions.Regex.Replace(t, @"\s{2,}", " ");
        t = t.Trim('"', '“', '”', '*', ' ');

        if (t.Length == 0 || t.Length > MaxLength) return null;

        // R8 has one home. A goal about getting back to the ship is the same false
        // canon as an arrival log about it.
        if (ArrivalText.ClaimsArrival(t)) return null;

        return t;
    }

    /// <summary>
    /// The scene block, and there is deliberately NO instruction clause to go with it.
    ///
    /// Measured, on the colony chronicle, three arms: a concrete thing with a noun in
    /// it reached 100% uptake stated in the scene alone, and adding a clause pointing
    /// at it bought nothing while taking distinct trigrams from .95 to .63. A goal is
    /// that category — an intention with an object — not a status. See ProseSceneText.
    /// </summary>
    public static string Block(string pawnName, string statement)
    {
        if (string.IsNullOrWhiteSpace(pawnName) || string.IsNullOrWhiteSpace(statement)) return null;
        var s = statement.Trim();
        return $"{pawnName.Trim()} wants: {ProseWords.Mid(s)}.";
    }
}
