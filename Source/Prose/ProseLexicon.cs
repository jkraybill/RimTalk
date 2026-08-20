using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>How a trait label attaches to a person in a sentence.</summary>
public enum TraitForm
{
    /// <summary>"He is <b>kind</b>." — a bare adjective.</summary>
    Adjective,
    /// <summary>"He is <b>a night owl</b>." — a countable noun, needs an article.</summary>
    Noun,
    /// <summary>"He has <b>a great memory</b>." — a possession, not a description.</summary>
    Have,
}

/// <summary>
/// The words the game gives us, and how each one behaves in a sentence.
///
/// RimWorld def labels are UI fragments, not English. Dropping them straight into a
/// sentence slot is what produced "He is fast walker" and "She is brawler". Which
/// slot a label fits is not derivable from the string — "kind" and "cannibal" look
/// identical to any heuristic — so the vanilla set is enumerated, harvested from
/// the game's own TraitDefs rather than guessed. <c>ProseLexiconTests</c> reads
/// those same files and fails if the game knows a trait this table does not.
///
/// Modded traits fall back to <see cref="Guess"/>, which is a safety net and not a
/// claim to be right. Coverage of vanilla is proved so that vanilla never reaches it.
/// </summary>
public static class ProseLexicon
{
    // 73 labels, every degreeData in Core + Royalty + Ideology + Biotech + Anomaly.
    static readonly Dictionary<string, TraitForm> TraitForms = new()
    {
        ["abrasive"] = TraitForm.Adjective,
        ["annoying voice"] = TraitForm.Have,
        ["ascetic"] = TraitForm.Adjective,
        ["asexual"] = TraitForm.Adjective,
        ["beautiful"] = TraitForm.Adjective,
        ["bisexual"] = TraitForm.Adjective,
        ["bloodlust"] = TraitForm.Have,
        ["body mastery"] = TraitForm.Have,
        ["body modder"] = TraitForm.Noun,
        ["body purist"] = TraitForm.Noun,
        ["brawler"] = TraitForm.Noun,
        ["cannibal"] = TraitForm.Noun,
        ["careful shooter"] = TraitForm.Noun,
        ["chemical fascination"] = TraitForm.Have,
        ["chemical interest"] = TraitForm.Have,
        ["creepy breathing"] = TraitForm.Have,
        ["delicate"] = TraitForm.Adjective,
        ["depressive"] = TraitForm.Adjective,
        ["disturbing"] = TraitForm.Adjective,
        ["fast learner"] = TraitForm.Noun,
        ["fast walker"] = TraitForm.Noun,
        ["gay"] = TraitForm.Adjective,
        ["gourmand"] = TraitForm.Noun,
        ["great memory"] = TraitForm.Have,
        ["greedy"] = TraitForm.Adjective,
        ["hard worker"] = TraitForm.Noun,
        ["industrious"] = TraitForm.Adjective,
        ["iron-willed"] = TraitForm.Adjective,
        ["jealous"] = TraitForm.Adjective,
        ["jogger"] = TraitForm.Noun,
        ["joyous"] = TraitForm.Adjective,
        ["kind"] = TraitForm.Adjective,
        ["lazy"] = TraitForm.Adjective,
        ["masochist"] = TraitForm.Noun,
        ["misandrist"] = TraitForm.Noun,
        ["misogynist"] = TraitForm.Noun,
        ["nervous"] = TraitForm.Adjective,
        ["neurotic"] = TraitForm.Adjective,
        ["night owl"] = TraitForm.Noun,
        ["nimble"] = TraitForm.Adjective,
        ["nudist"] = TraitForm.Noun,
        ["occultist"] = TraitForm.Noun,
        ["optimist"] = TraitForm.Noun,
        ["perfect memory"] = TraitForm.Have,
        ["pessimist"] = TraitForm.Noun,
        ["pretty"] = TraitForm.Adjective,
        ["psychically deaf"] = TraitForm.Adjective,
        ["psychically dull"] = TraitForm.Adjective,
        ["psychically hypersensitive"] = TraitForm.Adjective,
        ["psychically sensitive"] = TraitForm.Adjective,
        ["psychopath"] = TraitForm.Noun,
        ["pyromaniac"] = TraitForm.Noun,
        ["quick sleeper"] = TraitForm.Noun,
        ["recluse"] = TraitForm.Noun,
        ["sanguine"] = TraitForm.Adjective,
        ["sickly"] = TraitForm.Adjective,
        ["slothful"] = TraitForm.Adjective,
        ["slow learner"] = TraitForm.Noun,
        ["slowpoke"] = TraitForm.Noun,
        ["staggeringly ugly"] = TraitForm.Adjective,
        ["steadfast"] = TraitForm.Adjective,
        ["super-immune"] = TraitForm.Adjective,
        ["teetotaler"] = TraitForm.Noun,
        ["too smart"] = TraitForm.Adjective,
        ["tortured artist"] = TraitForm.Noun,
        ["tough"] = TraitForm.Adjective,
        ["trigger-happy"] = TraitForm.Adjective,
        ["ugly"] = TraitForm.Adjective,
        ["undergrounder"] = TraitForm.Noun,
        ["very neurotic"] = TraitForm.Adjective,
        ["void fascination"] = TraitForm.Have,
        ["volatile"] = TraitForm.Adjective,
        ["wimp"] = TraitForm.Noun,
    };

    public static TraitForm FormOf(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return TraitForm.Adjective;
        var key = label.Trim().ToLowerInvariant();
        return TraitForms.TryGetValue(key, out var form) ? form : Guess(key);
    }

    /// <summary>True when the table knows this label outright. For the coverage test.</summary>
    public static bool Knows(string label) =>
        !string.IsNullOrWhiteSpace(label) && TraitForms.ContainsKey(label.Trim().ToLowerInvariant());

    public static IEnumerable<string> KnownTraits => TraitForms.Keys;

    /// <summary>
    /// The safety net for modded traits. Adjectives in English overwhelmingly end in
    /// a small set of suffixes; a label that ends in none of them is far more likely
    /// to be a noun ("shadowdancer", "voidtouched cultist") than an adjective, and
    /// "is a shadowdancer" degrades better than "is shadowdancer".
    /// </summary>
    static TraitForm Guess(string label)
    {
        var last = label.Split(' ', '-').Last();
        return AdjectiveEndings.Any(last.EndsWith) ? TraitForm.Adjective : TraitForm.Noun;
    }

    static readonly string[] AdjectiveEndings =
    {
        "ous", "ful", "ive", "able", "ible", "less", "ish", "like", "proof", "worthy",
        "ic", "al", "ant", "ent", "ary", "ile", "ose", "y", "ed", "ing",
    };

    /// <summary>
    /// What a pawn wants, keyed on SkillDef.defName.
    ///
    /// Keyed on defName and not on the label for a reason found while writing this:
    /// vanilla SkillDefs carry <c>skillLabel</c>, not <c>label</c>, so
    /// <c>SkillDef.label</c> is null for all twelve. The previous line —
    /// "for the {label} work to hold" — was shipping "for the  work to hold" with a
    /// hole in it, in every profile, for every pawn.
    ///
    /// {0} is the subject pronoun, {1} the possessive.
    /// </summary>
    static readonly Dictionary<string, string> Wants = new()
    {
        ["Shooting"]     = "for {1} hands to be steady the next time it matters",
        ["Melee"]        = "for the next one to go down before {0} does",
        ["Construction"] = "for the roof to hold through the next storm",
        ["Mining"]       = "to find the seam {0} can hear behind the rock",
        ["Cooking"]      = "one meal that nobody eats standing up",
        ["Plants"]       = "for the crop to come in ahead of the frost",
        ["Animals"]      = "for the herd to make it through the season",
        ["Crafting"]     = "one bench, properly lit, and an afternoon nobody interrupts",
        ["Artistic"]     = "for somebody to stop and actually look at it",
        ["Medicine"]     = "for nobody else to come through that door bleeding",
        ["Social"]       = "one conversation that is not about work",
        ["Intellectual"] = "to finish the thing {0} started before all this",
    };

    /// <summary>Null when the skill is unknown and has no usable label to fall back on.</summary>
    public static string Want(string skillDefName, string skillLabel, string subject, string possessive)
    {
        if (!string.IsNullOrWhiteSpace(skillDefName) && Wants.TryGetValue(skillDefName, out var want))
            return want.Replace("{0}", subject).Replace("{1}", possessive);
        if (!string.IsNullOrWhiteSpace(skillLabel))
            return $"for the {ProseWords.Mid(skillLabel)} work to hold";
        return null;
    }

    public static IEnumerable<string> KnownSkills => Wants.Keys;

    /// <summary>
    /// Hediff labels that break the "a {label} in {possessive} {part}" frame.
    /// MissingBodyPart's label is literally "missing body part", so the frame yields
    /// "a missing body part in his left leg".
    /// </summary>
    public static readonly HashSet<string> AmputationLabels = new()
    {
        "missing body part", "missing",
    };

    /// <summary>
    /// The pawn's standing in the colony, when it is worth a clause.
    ///
    /// <c>PawnUtil.GetRole()</c> returns one of eight strings. "Colonist" applies to
    /// nearly everyone in the prompt and carries no information; "Prisoner" and
    /// "Slave" are said again, and better, by the Standing sentence. Only the rest
    /// change how a reader should hear the character.
    /// </summary>
    public static string StandingPhrase(string role) => role switch
    {
        "Visitor" => "here as a visitor",
        "Lodger" => "here as a lodger",
        "Enemy" => "here to kill everyone in this colony",
        "Enemy Defender" => "defending this place against the colony",
        "Invader" => "here to take this place",
        _ => null,      // Colonist, Prisoner, Slave, null
    };

    /// <summary>
    /// Whether an affliction takes an article: "a gunshot in her left arm", but
    /// "frostbite in her nose".
    ///
    /// The game ships 319 hediff labels and most are never visible on a pawn, so
    /// enumerating them is disproportionate and would rot. The default is therefore
    /// *no article*, because omitting one is a much smaller error than inventing one:
    /// "frostbite in her nose" merely reads plainly, while "a frostbite", "a
    /// blindness" and "a malaria" are all wrong. Countability is claimed only for the
    /// injuries, which are a small stable set and compound predictably — "acid burn",
    /// "chemical burn" and "surgical cut" all fall out of the endings below.
    /// </summary>
    public static bool TakesArticle(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return false;
        var l = label.Trim().ToLowerInvariant();
        return CountableSingles.Contains(l) || CountableEndings.Any(l.EndsWith);
    }

    static readonly string[] CountableEndings =
    {
        "burn", "cut", "bruise", "gunshot", "scratch", "bite", "stab", "crush",
        "crack", "wound", "quill", "blade", "spike", "talon", "fang",
    };

    static readonly HashSet<string> CountableSingles = new()
    {
        "cataract", "carcinoma", "heart attack", "drug overdose", "decayed organ",
        "artery blockage",
    };
}
