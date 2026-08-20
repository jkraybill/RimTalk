using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>One visible affliction, as the profile needs it.</summary>
public class BodyNote
{
    public string Label;
    public string Part;      // null when the hediff is not on a specific part
    public float Severity;
}

/// <summary>
/// Everything the pawn profile says, reduced to primitives.
///
/// This exists so the prose can be *executed*. `Pawn` cannot be constructed outside
/// a running game, which meant every phrasing decision in the profile — the whole
/// point of the S166 rewrite — was verified by reading it. The prompt lab measured
/// hand-written prompts, so it proved the model prefers prose and proved nothing
/// about what this code emits.
/// </summary>
public class PawnFacts
{
    public string Name = "";
    public string Gender = "";          // "Male" | "Female" | anything else -> they
    public int Age;
    public string AdulthoodTitle;       // null when the pawn has neither adulthood nor childhood
    public string Role;                 // null when the pawn holds no colony role
    public List<string> TraitLabels = new();
    public List<BodyNote> Body = new();
    public bool IsPrisoner;
    public bool IsSlave;
    public int OtherColonists;
    public bool InMentalState;
    public bool Downed;
    public bool IsBaby;
    public float? MoodPercent;          // null when the pawn has no mood need
    public List<string> Thoughts = new();
    public string TopSkillDefName;      // SkillDef.defName — the key the want table uses
    public string TopSkillLabel;        // SkillDef.label, which is null for every vanilla skill
    public int TopSkillLevel;

    /// <summary>
    /// What this pawn wrote the day they came to here, if they have one. #37.
    /// Their own canonical account, in their own voice, checked once against R8.
    /// </summary>
    public string ArrivalLog;
}

/// <summary>
/// The pawn profile as sentences. No RimWorld types: source-linked into the test
/// project and run for real.
///
/// The rule this file follows: a def label is a UI fragment, never a sentence part.
/// Every label passes through <see cref="ProseLexicon"/> or an explicit frame before
/// it reaches the reader. That is the difference between "He is fast walker" and
/// "He is nervous, and a fast walker".
/// </summary>
public static class ProseProfileText
{
    public static string Compose(PawnFacts f)
    {
        if (f == null) return "";
        var g = new Gram(f.Gender);

        var paras = new List<string>();

        // 1. Who they are, and who they were.
        paras.Add(ProseWords.Paragraph(
            WhoTheyAre(f, g),
            Describes(f, g),
            Possesses(f, g),
            Body(f, g)));

        // 2. R8, stated once. Their own account when they have written one — it says
        //    the same thing better, in their voice, and having it here is what stops
        //    the model re-inventing an arrival every time it is asked. #37.
        paras.Add(string.IsNullOrWhiteSpace(f.ArrivalLog)
            ? $"{g.Subj} woke here with no memory of arriving, and never will."
            : $"What {f.Name} wrote the day {g.subj} woke here: {f.ArrivalLog.Trim()}");

        // 3. Where they stand today.
        paras.Add(ProseWords.Paragraph(
            Standing(f, g),
            Feeling(f, g),
            Carrying(f, g)));

        // 4. Something unresolved. Replaced by a real Need (#30) or Goal (#28)
        //    when those exist; until then, derived from what they are good at.
        var want = Want(f, g);
        if (want != null) paras.Add(want);

        return string.Join("\n\n", paras.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    /// <summary>The pronouns and verbs for one pawn, resolved once.</summary>
    sealed class Gram
    {
        public readonly string subj, Subj, poss, Poss, obj, Is, Has;
        public Gram(string gender)
        {
            subj = ProseWords.Subject(gender);
            Subj = ProseWords.Cap(subj);
            poss = ProseWords.Possessive(gender);
            Poss = ProseWords.Cap(poss);
            obj = ProseWords.Object(gender);
            Is = ProseWords.Is(gender);
            Has = ProseWords.Has(gender);
        }
    }

    // ---- paragraph 1 ------------------------------------------------------------

    static string WhoTheyAre(PawnFacts f, Gram g)
    {
        // Standing, not role. `GetRole()` answers "Colonist" for every free colonist,
        // which is true of almost everyone and therefore says nothing; and "Prisoner"
        // and "Slave" are already said, better, by Standing(). What is left is the
        // handful of cases where the pawn is not simply one of the household.
        var standing = ProseLexicon.StandingPhrase(f.Role);

        if (!string.IsNullOrWhiteSpace(f.AdulthoodTitle))
        {
            var was = ProseWords.Mid(f.AdulthoodTitle);
            var line = $"{f.Name} {g.Is} {f.Age}. {g.Subj} was {ProseWords.Article(was)} {was} before this";
            return standing != null ? $"{line}, and {standing}" : line;
        }
        return standing != null ? $"{f.Name} {g.Is} {f.Age}, and {standing}"
                                : $"{f.Name} {g.Is} {f.Age}";
    }

    /// <summary>Traits that describe: "He is kind and nervous, and a fast walker."</summary>
    static string Describes(PawnFacts f, Gram g)
    {
        var traits = Traits(f);
        var adjectives = traits.Where(t => ProseLexicon.FormOf(t) == TraitForm.Adjective)
                               .Select(ProseWords.Mid).ToList();
        var nouns = traits.Where(t => ProseLexicon.FormOf(t) == TraitForm.Noun)
                          .Select(t => $"{ProseWords.Article(t)} {ProseWords.Mid(t)}").ToList();

        if (adjectives.Count == 0 && nouns.Count == 0) return null;
        if (nouns.Count == 0) return $"{g.Subj} {g.Is} {ProseWords.Join(adjectives)}";
        if (adjectives.Count == 0) return $"{g.Subj} {g.Is} {ProseWords.Join(nouns)}";
        // The comma earns its place only when the adjectives are themselves a list;
        // "too smart, and a night owl" is a stutter, "kind and nervous, and a fast
        // walker" is a sentence.
        var sep = adjectives.Count > 1 ? ", and " : " and ";
        return $"{g.Subj} {g.Is} {ProseWords.Join(adjectives)}{sep}{ProseWords.Join(nouns)}";
    }

    /// <summary>Traits that are had rather than been: "He has a great memory."</summary>
    static string Possesses(PawnFacts f, Gram g)
    {
        var had = Traits(f).Where(t => ProseLexicon.FormOf(t) == TraitForm.Have)
                           .Select(t => Countable(t) ? $"{ProseWords.Article(t)} {ProseWords.Mid(t)}"
                                                     : ProseWords.Mid(t))
                           .ToList();
        return had.Count == 0 ? null : $"{g.Subj} {g.Has} {ProseWords.Join(had)}";
    }

    /// <summary>"a great memory" but not "a bloodlust" — mass nouns take no article.</summary>
    static bool Countable(string label) =>
        label != "bloodlust" && label != "body mastery" && label != "creepy breathing";

    static List<string> Traits(PawnFacts f) =>
        (f.TraitLabels ?? new List<string>())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => l.Trim().ToLowerInvariant())
            .Distinct()
            .Take(4)
            .ToList();

    static string Body(PawnFacts f, Gram g)
    {
        var worst = (f.Body ?? new List<BodyNote>())
            .Where(h => h != null && !string.IsNullOrWhiteSpace(h.Label))
            .OrderByDescending(h => h.Severity)
            .Take(2).ToList();
        if (worst.Count == 0) return null;

        var gone = new List<string>();      // "his left leg"
        var carried = new List<string>();   // "a cataract in his left eye", or "flu"

        foreach (var h in worst)
        {
            var label = ProseWords.Mid(h.Label);
            // An amputation is not something you have *in* a limb; the limb is the
            // thing. MissingBodyPart's label is literally "missing body part", so the
            // ordinary frame yields "a missing body part in his left leg".
            if (ProseLexicon.AmputationLabels.Contains(label) && h.Part != null)
                gone.Add($"{g.poss} {ProseWords.Mid(h.Part)}");
            else if (h.Part != null)
                carried.Add(ProseLexicon.TakesArticle(label)
                    ? $"{ProseWords.Article(label)} {label} in {g.poss} {ProseWords.Mid(h.Part)}"
                    : $"{label} in {g.poss} {ProseWords.Mid(h.Part)}");   // "frostbite in her nose"
            else
                carried.Add(label);         // "flu", never "a flu"
        }

        var isAre = gone.Count == 1 ? "is" : "are";
        var mentions = g.Has == "has" ? "mentions" : "mention";
        var doesNot = g.Has == "has" ? "does not" : "do not";

        if (gone.Count == 0)
            return $"{g.Subj} {g.Has} {ProseWords.Join(carried)}, " +
                   $"which {(carried.Count == 1 ? "goes" : "go")} unmentioned";
        if (carried.Count == 0)
            return $"{ProseWords.Cap(ProseWords.Join(gone))} {isAre} gone, and {g.subj} {doesNot} mention it";
        return $"{ProseWords.Cap(ProseWords.Join(gone))} {isAre} gone and {g.subj} {g.Has} " +
               $"{ProseWords.Join(carried)}, none of which {g.subj} {mentions}";
    }

    // ---- paragraph 3 ------------------------------------------------------------

    static string Standing(PawnFacts f, Gram g)
    {
        if (f.IsPrisoner) return $"{g.Subj} {g.Is} held here against {g.poss} will";
        if (f.IsSlave)    return $"{g.Subj} {g.Is} owned";

        if (f.OtherColonists == 0) return $"{g.Subj} {g.Is} the only living person on this rock";
        if (f.OtherColonists == 1) return $"There {g.Is} one other person here";
        return $"There are {f.OtherColonists} others here";
    }

    static string Feeling(PawnFacts f, Gram g)
    {
        if (f.InMentalState) return $"{g.Subj} {g.Is} not in control of {g.poss} own head right now";
        if (f.Downed && !f.IsBaby) return $"{g.Subj} {g.Is} down and in real pain";

        if (f.MoodPercent == null) return null;
        var pct = f.MoodPercent.Value;
        if (pct < 0.25f) return $"{g.Subj} {g.Is} close to breaking";
        if (pct < 0.45f) return $"{g.Subj} {g.Is} worn down";
        if (pct > 0.80f) return $"{g.Subj} {g.Is} in genuinely good spirits";
        return null;   // an unremarkable mood earns no sentence
    }

    static string Carrying(PawnFacts f, Gram g)
    {
        var thoughts = (f.Thoughts ?? new List<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(ProseWords.Mid)
            .Take(3).ToList();
        return thoughts.Count == 0 ? null : $"On {g.poss} mind: {ProseWords.Join(thoughts)}";
    }

    // ---- paragraph 4 ------------------------------------------------------------

    static string Want(PawnFacts f, Gram g)
    {
        if (f.TopSkillLevel < 6) return null;
        var want = ProseLexicon.Want(f.TopSkillDefName, f.TopSkillLabel, g.subj, g.poss);
        return want == null ? null : $"What {f.Name} wants right now: {want}.";
    }
}
