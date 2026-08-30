using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>
/// The gossip block. rim-universe #22.
///
/// Two things the speaker knows about people who are not here, and one line saying
/// who those people are, because the joke JK asked for turns on knowing that both
/// absent parties are miners:
///
///   Not here, but talked about:
///     Adrian and Starling came to blows this morning.
///     Adrian is a miner. Starling is a miner.
///
/// The second line exists because the prompt's Nearby list carries age, gender and
/// role for people in the room and nothing at all for people who are not. Without it
/// the model can repeat the event and cannot say anything about it.
///
/// No instruction pointing at the block. S168 measured that for the chronicle across
/// three arms: a clause telling the model to use a scene fact bought no uptake the
/// block did not already have and took distinct trigrams from .95 to .63. The rule
/// it established was that pointing helps for STATUSES and not for events, and
/// gossip is an event.
/// </summary>
public static class GossipText
{
    /// <summary>The heading. Says both halves: absent, and fair game.</summary>
    public const string Header = "Not here, but talked about:";

    /// <summary>
    /// Six words of description per absent person, which is enough for "a miner" or
    /// "the colony's only doctor" and not enough for a CV. They are scenery in
    /// somebody else's conversation.
    /// </summary>
    public const int MaxWordsPerPerson = 6;

    /// <summary>
    /// Compose, or null when there is nothing to say.
    ///
    /// Null rather than an empty header, because an empty block is worse than no
    /// block: it spends context and tells the model there was something it failed to
    /// see.
    /// </summary>
    public static string Compose(IEnumerable<string> clauses, IEnumerable<string> whoTheyAre)
    {
        var lines = Clean(clauses);
        if (lines.Count == 0) return null;

        var people = Clean(whoTheyAre);

        var block = new List<string> { Header };
        block.AddRange(lines.Select(l => "  " + Stop(l)));
        if (people.Count > 0) block.Add("  " + string.Join(" ", people.Select(Stop)));

        return string.Join("\n", block);
    }

    /// <summary>
    /// "Adrian is a miner" — who an absent person is, in the terms the conversation
    /// would use. Null when there is nothing worth saying, so a colonist with no
    /// standout skill simply does not get a line rather than getting an empty one.
    /// </summary>
    public static string Describe(string name, string what)
    {
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(what)) return null;

        var trimmed = Words(what, MaxWordsPerPerson);
        return trimmed == null ? null : $"{name.Trim()} is {trimmed}";
    }

    static List<string> Clean(IEnumerable<string> items) =>
        (items ?? Enumerable.Empty<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Distinct(System.StringComparer.OrdinalIgnoreCase)
            .ToList();

    static string Stop(string s) =>
        s.EndsWith(".") || s.EndsWith("!") || s.EndsWith("?") ? s : s + ".";

    static string Words(string s, int max)
    {
        var parts = (s ?? "").Split(new[] { ' ', '\t', '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? null : string.Join(" ", parts.Take(max));
    }

    /// <summary>
    /// What a skill makes somebody, in the word a colonist would use. rim-universe
    /// #22: JK's punchline needs both absent parties identifiable as miners, and
    /// "Mining 14" is a stat block rather than something anyone says.
    ///
    /// Null for a skill with no natural noun, so the person simply gets no line
    /// instead of being called "an intellectual".
    /// </summary>
    public static string SkillNoun(string skillDefName) => (skillDefName ?? "").Trim() switch
    {
        "Mining" => "a miner",
        "Medicine" => "a doctor",
        "Cooking" => "a cook",
        "Construction" => "a builder",
        "Plants" => "a grower",
        "Animals" => "an animal handler",
        "Crafting" => "a crafter",
        "Artistic" => "an artist",
        "Intellectual" => "a researcher",
        "Shooting" => "a good shot",
        "Melee" => "a brawler",
        "Social" => "the one who does the talking",
        _ => null,
    };
}
