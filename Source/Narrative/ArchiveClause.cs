using System;
using System.Linq;

namespace RimTalk.Narrative;

/// <summary>
/// The second event source. rim-universe #22 / S169.
///
/// The Tale harvest is built on what RimWorld thinks is worth carving into a
/// sculpture, and the bar is far higher than "worth mentioning over dinner": a real
/// ten-day colony recorded FIVE tales, three of them the drop pods everyone arrived
/// in. So `Lately:`, `Since then:` and the whole of #22 had one clause between them —
/// "the colony was founded here" — in exactly the early game #27 says matters most.
///
/// The Archive is the other list, and it is a better one for this purpose because it
/// is literally what the game already decided to TELL THE PLAYER happened. Raids,
/// illnesses, joins, births, breakdowns.
///
/// The wording problem is different from TaleClause's, though. A Tale is structured
/// arguments and no prose; an archived letter is prose already, written for a
/// notification box. So this file's job is not to compose a sentence but to decide
/// whether a label is safe to repeat, and to trim it — a UI string going into a
/// prompt unchecked is how a model ends up saying "Click to jump to location".
/// </summary>
public static class ArchiveClause
{
    /// <summary>
    /// Twelve words. Archived labels are short by design; anything much longer is a
    /// tooltip that has leaked into the label field, and a paragraph in the chronicle
    /// would crowd out four real events.
    /// </summary>
    public const int MaxWords = 12;

    /// <summary>
    /// Which letter categories become history.
    ///
    /// NeutralEvent is the loud one and is excluded on purpose: it carries the
    /// research-finished and area-unlocked chatter that fires constantly, and a
    /// chronicle full of it is the bounded store filling with noise. The categories
    /// kept are the ones a colonist would still be talking about at supper.
    /// </summary>
    public static bool IsHarvested(string letterDefName) => (letterDefName ?? "").Trim() switch
    {
        "ThreatBig" or "ThreatSmall" or "NegativeEvent" or "PositiveEvent" or "Death"
            or "AcceptJoiner" or "AcceptCreepJoiner" or "BabyBirth" or "Bossgroup"
            or "EntityDiscovered" or "RitualOutcomeNegative" or "RitualOutcomePositive" => true,
        _ => false,
    };

    /// <summary>
    /// How the event reflects on the people in it, for GossipOpinion. Mirrors
    /// GossipOpinion.Valence, which keys on Tale defNames and would not recognise
    /// these.
    /// </summary>
    public static int Valence(string letterDefName) => (letterDefName ?? "").Trim() switch
    {
        "ThreatBig" or "ThreatSmall" or "NegativeEvent" or "Death"
            or "Bossgroup" or "RitualOutcomeNegative" => -1,
        "PositiveEvent" or "AcceptJoiner" or "AcceptCreepJoiner" or "BabyBirth"
            or "RitualOutcomePositive" => 1,
        _ => 0,
    };

    /// <summary>
    /// The label as a clause, or null when it is not fit to repeat.
    ///
    /// No trailing stop, because these are joined into the middle of a sentence
    /// exactly like TaleClause's output. The case is left alone — see below.
    /// </summary>
    public static string For(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return null;

        var t = label.Replace('\n', ' ').Replace('\r', ' ').Trim();

        // A colon is how RimWorld attaches a subtitle — "Raid: Barva", "Quest: Ancient
        // danger". The head is the event; the tail is bookkeeping.
        var colon = t.IndexOf(':');
        if (colon > 0) t = t.Substring(0, colon).Trim();

        var words = t.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0) return null;
        if (words.Length > MaxWords) words = words.Take(MaxWords).ToArray();

        t = string.Join(" ", words).Trim('.', '!', '?', ',', ':', ';', '-', ' ');

        // Something has to be left that is actually a word. "..." and ":" both survive
        // every step above and would go into a prompt as themselves.
        if (!t.Any(char.IsLetterOrDigit)) return null;

        // The case is left exactly as RimWorld wrote it, and that is a concession
        // rather than a preference. A leading capital mid-sentence reads slightly
        // headline-ish; lower-casing it turns "Bren joined the colony" into "bren",
        // and there is no reliable way to tell a colonist's name from a sentence
        // start. Getting somebody's name wrong is the worse error by a distance.
        return t;
    }

    /// <summary>
    /// Identity for the dedupe. RimWorld sends the same letter type repeatedly in a
    /// bad quadrum and the store is bounded.
    /// </summary>
    public static string DedupeKey(string letterDefName, string clause) =>
        $"archive|{(letterDefName ?? "").Trim()}|{(clause ?? "").Trim().ToLowerInvariant()}";
}
