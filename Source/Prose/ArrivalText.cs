using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace RimTalk.Prose;

/// <summary>
/// The arrival log: one generation, at the moment a pawn enters the colony's orbit,
/// that becomes their own canonical account of waking up here.
///
/// rim-universe #37. JK: "when a new Pawn spawns, I want us to use the AI generator
/// to create an initial Log entry for that person -- regarding their state of mind".
///
/// It is worth more than a flourish because of what it does to R8. A pawn never
/// knows how they arrived and never will, and that constraint fights the model's
/// strongest instinct: asked to play someone who woke up somewhere, a model invents
/// a shipwreck. Once that is in chat history the colony has a false canon. Writing
/// the entry ONCE, under supervision, turns an ongoing fight into a single
/// well-checked generation — and everything afterwards refers back to it instead of
/// re-inventing.
///
/// Pure: the prompt, the cleaning and the R8 check are all here and all executed in
/// the test project. The call itself is next door in ArrivalService.
/// </summary>
public static class ArrivalText
{
    /// <summary>
    /// What to ask for. Second person, present tense, and the amnesia stated as the
    /// pawn's own situation rather than as a rule they must obey — a rule invites
    /// compliance language ("I do not know how I got here"), a situation invites a
    /// character.
    /// </summary>
    public static string Prompt(string profile, string place)
    {
        var where = string.IsNullOrWhiteSpace(place) ? "somewhere you have never been" : place.Trim();

        return
            "You are writing one short log entry, in first person, for the moment this " +
            "person came to.\n\n" +
            $"They have just woken up in {where}. They do not know how they got here and " +
            "they never will. They may wonder. They may guess from what they remember of " +
            "their own life. They may not decide, and they may not say what happened.\n\n" +
            "Two or three sentences. Their voice, not a narrator's. What they notice " +
            "first, and what they are going to do about it.\n\n" +
            "Reply with JSON and nothing else: {\"log\": \"...\"}\n\n" +
            "[Who they are]\n" + (profile ?? "").Trim();
    }

    /// <summary>
    /// A claim about how they got here, which R8 forbids. Wondering is the rule
    /// working; naming the machinery is the rule breaking.
    ///
    /// One regex, used by both the mod and the prompt lab, because two copies of a
    /// rule drift and then disagree about whether it was ever broken.
    /// </summary>
    static readonly Regex ArrivalClaim = new(
        @"\b(crash[- ]?land\w*|crashed|escape pod|drop ?pod|cryptosleep|casket|" +
        @"(our|the) ship|shuttle|the wreck|when we landed|we landed here)\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool ClaimsArrival(string text) => ArrivalClaim.IsMatch(text ?? "");

    /// <summary>
    /// Clean a generated entry, or reject it. Null means "do not keep this" — better
    /// no arrival log than a false one, because everything downstream treats it as
    /// canon and a colony only gets one.
    /// </summary>
    public static string Accept(string generated)
    {
        if (string.IsNullOrWhiteSpace(generated)) return null;

        var text = generated.Replace("**", "").Replace("\r", " ").Replace("\n", " ").Trim();
        text = Regex.Replace(text, @"\s{2,}", " ");
        text = text.Trim('"', '“', '”').Trim();

        if (text.Length == 0) return null;
        if (ClaimsArrival(text)) return null;             // R8
        if (text.Length > MaxLength) return null;         // a paragraph is not a log entry
        return text;
    }

    /// <summary>
    /// Long enough for three sentences, short enough that it cannot crowd the profile
    /// it will be quoted in for the rest of the pawn's life.
    /// </summary>
    public const int MaxLength = 400;
}
