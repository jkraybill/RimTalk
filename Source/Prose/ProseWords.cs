using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>
/// The parts of prose assembly that are decidable without a running game.
///
/// Kept free of RimWorld types on purpose: this file is source-linked into the
/// test project, so the phrasing rules — which are where prose assembly actually
/// goes wrong — are verified rather than inspected.
/// </summary>
public static class ProseWords
{
    /// <summary>"Dawn", "Late afternoon" — a time a person would say, not "6am".</summary>
    public static string TimeOfDay(int hour24)
    {
        hour24 = ((hour24 % 24) + 24) % 24;
        if (hour24 < 4)  return "The middle of the night";
        if (hour24 < 7)  return "Dawn";
        if (hour24 < 11) return "Morning";
        if (hour24 < 14) return "Midday";
        if (hour24 < 17) return "Afternoon";
        if (hour24 < 20) return "Evening";
        return "Night";
    }

    /// <summary>Temperature as a person experiences it. RimWorld pawns freeze and cook.</summary>
    public static string Cold(int celsius)
    {
        if (celsius <= -20) return "murderously cold";
        if (celsius <= 0)   return "freezing";
        if (celsius <= 8)   return "cold";
        if (celsius <= 16)  return "cool";
        if (celsius <= 26)  return "mild";
        if (celsius <= 35)  return "hot";
        return "dangerously hot";
    }

    /// <summary>she / he / they — never guessed from a name.</summary>
    public static string Subject(string gender) => gender switch
    {
        "Female" => "she", "Male" => "he", _ => "they"
    };

    public static string Possessive(string gender) => gender switch
    {
        "Female" => "her", "Male" => "his", _ => "their"
    };

    /// <summary>her / him / them — the object form.</summary>
    public static string Object(string gender) => gender switch
    {
        "Female" => "her", "Male" => "him", _ => "them"
    };

    /// <summary>"has" vs "have" — they/them takes the plural verb.</summary>
    public static string Has(string gender) => gender == "Female" || gender == "Male" ? "has" : "have";

    public static string Is(string gender) => gender == "Female" || gender == "Male" ? "is" : "are";

    /// <summary>"a, b and c" — an Oxford-comma-free join, because this is prose.</summary>
    public static string Join(IEnumerable<string> items)
    {
        var l = items?.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToList();
        if (l == null || l.Count == 0) return "";
        if (l.Count == 1) return l[0];
        if (l.Count == 2) return $"{l[0]} and {l[1]}";
        return string.Join(", ", l.Take(l.Count - 1)) + " and " + l[^1];
    }

    /// <summary>
    /// Assemble sentences into a paragraph, dropping empties and making sure every
    /// one ends in a stop. A prose block with a dangling fragment reads as broken,
    /// and fragments are what happen when a field was empty.
    /// </summary>
    public static string Paragraph(params string[] sentences)
    {
        var kept = new List<string>();
        foreach (var s in sentences ?? System.Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(s)) continue;
            var t = s.Trim();
            if (!t.EndsWith(".") && !t.EndsWith("!") && !t.EndsWith("?")) t += ".";
            kept.Add(t);
        }
        return string.Join(" ", kept);
    }

    /// <summary>
    /// Lowercase a label about to sit mid-sentence — "Colony doctor" becomes
    /// "colony doctor" — without wrecking anything that is capitalised on purpose.
    ///
    /// The rule is about the whole first WORD, not its second character: a word
    /// carrying an internal capital (RimWorld, McKinley) or one that is all caps
    /// (UN, ISO) was capitalised deliberately and is left alone. Checking only
    /// label[1] turned "RimWorld" into "rimWorld".
    /// </summary>
    public static string Mid(string label)
    {
        if (string.IsNullOrWhiteSpace(label)) return "";
        if (!char.IsUpper(label[0])) return label;

        var firstWord = label.Split(' ')[0];
        if (firstWord.Skip(1).Any(char.IsUpper)) return label;   // RimWorld, McKinley, UN

        return char.ToLowerInvariant(label[0]) + label[1..];
    }

    /// <summary>
    /// "a" or "an", by sound rather than by spelling.
    ///
    /// Spelling alone gets "an heir" and "a university" both wrong. Checked against
    /// the game's own 1110 backstory titles: every u-initial one ("urchin",
    /// "urbworld drone", "undertaker") does take "an", and "heir" is the single
    /// silent-h case in vanilla — so the exception lists below exist mostly for
    /// modded content, which is exactly where an unchecked rule would break.
    /// </summary>
    public static string Article(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return "a";
        var w = word.TrimStart().ToLowerInvariant();

        // Silent h: the vowel sound wins.
        if (SilentH.Any(w.StartsWith)) return "an";

        // Written vowel, spoken consonant: "a university", "a one-eyed drifter".
        if (SoundsLikeConsonant.Any(w.StartsWith)) return "a";

        return "aeiou".IndexOf(w[0]) >= 0 ? "an" : "a";
    }

    static readonly string[] SilentH = { "hour", "honest", "honor", "honour", "heir" };

    static readonly string[] SoundsLikeConsonant =
    {
        "uni", "use", "usu", "util", "euro", "euph", "eul", "ewe", "one", "once", "ubiq", "ufo",
    };

    /// <summary>
    /// Uppercase the first character. Verse.GenText.CapitalizeFirst does this, but
    /// this file must stay free of RimWorld types so it can be source-linked into
    /// the test project, and so can the composers that call it.
    /// </summary>
    public static string Cap(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        if (char.IsUpper(s[0])) return s;
        return char.ToUpperInvariant(s[0]) + s[1..];
    }
}
