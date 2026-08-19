using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using Verse;

namespace RimTalk.Prose;

/// <summary>
/// A pawn as prose rather than a form.
///
/// The field-dump profile — "Traits: Kind,Night owl,Too smart" over eleven labelled
/// rows — reliably produced a model playing a spreadsheet row. Measured in the
/// prompt lab across three scenarios, the same data written as sentences produced
/// six to seven conversational turns where the form produced one, and turned "The
/// soil is cool and damp, these seeds need to be in before the rain picks up" into
/// "Potatoes should hold."
///
/// This is the deterministic version, free and available today. #37's arrival log
/// will generate a better one per pawn and cache it; when that exists it simply
/// replaces the first paragraph here.
/// </summary>
public static class ProseProfile
{
    public static string Build(Pawn pawn, PromptService.InfoLevel level)
    {
        if (pawn == null) return "";
        var g = pawn.gender.ToString();
        var subj = ProseWords.Subject(g);
        var Subj = char.ToUpperInvariant(subj[0]) + subj[1..];
        var has = ProseWords.Has(g);
        var isv = ProseWords.Is(g);
        var name = pawn.LabelShort;

        var paras = new List<string>();

        // 1. Who they are, and who they were.
        paras.Add(ProseWords.Paragraph(
            WhoTheyAre(pawn, name, isv),
            Traits(pawn, Subj, isv),
            Body(pawn, Subj, has)));

        // 2. The standing rule, stated once, in their own frame. R8.
        paras.Add($"{Subj} woke here with no memory of arriving, and never will.");

        // 3. Where they stand today.
        paras.Add(ProseWords.Paragraph(
            Standing(pawn, Subj, isv),
            Feeling(pawn, Subj, isv),
            Carrying(pawn, Subj, isv)));

        // 4. Something unresolved. Replaced by a real Need (#30) or Goal (#28)
        //    when those exist; until then, derived from what they are good at.
        var want = Want(pawn, name);
        if (want != null) paras.Add(want);

        return string.Join("\n\n", paras.Where(p => !string.IsNullOrWhiteSpace(p)));
    }

    static string WhoTheyAre(Pawn p, string name, string isv)
    {
        var age = p.ageTracker?.AgeBiologicalYears ?? 0;
        var adult = p.story?.Adulthood?.TitleFor(p.gender) ?? p.story?.Childhood?.TitleFor(p.gender);
        var role = p.GetRole();

        if (!string.IsNullOrWhiteSpace(adult))
            return $"{name} {isv} {age}. {ProseWords.Subject(p.gender.ToString()).CapitalizeFirst()} was " +
                   $"{Article(adult)} {ProseWords.Mid(adult)} before this";
        return role != null ? $"{name} {isv} {age}, and {ProseWords.Mid(role)} here"
                            : $"{name} {isv} {age}";
    }

    static string Traits(Pawn p, string Subj, string isv)
    {
        var labels = (p.story?.traits?.TraitsSorted ?? Enumerable.Empty<Trait>())
            .Select(t => GenCollection.FirstOrDefault(t.def.degreeDatas, d => d.degree == t.Degree)?.label)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(ProseWords.Mid)
            .Take(4).ToList();
        return labels.Count == 0 ? null : $"{Subj} {isv} {ProseWords.Join(labels)}";
    }

    static string Body(Pawn p, string Subj, string has)
    {
        var visible = p.health?.hediffSet?.hediffs?
            .Where(h => h.Visible && h.def?.label != null)
            .OrderByDescending(h => h.Severity)
            .Select(h => h.Part != null ? $"{ProseWords.Mid(h.def.label)} in the {h.Part.Label}"
                                        : ProseWords.Mid(h.def.label))
            .Take(2).ToList();
        return visible == null || visible.Count == 0
            ? null
            : $"{Subj} {has} {ProseWords.Join(visible)}, which {(visible.Count == 1 ? "goes" : "go")} unmentioned";
    }

    static string Standing(Pawn p, string Subj, string isv)
    {
        if (p.IsPrisoner) return $"{Subj} {isv} held here against {ProseWords.Possessive(p.gender.ToString())} will";
        if (p.IsSlave)    return $"{Subj} {isv} owned";

        var others = p.Map?.mapPawns?.FreeColonistsSpawned?.Count(c => c != p && !c.Dead) ?? 0;
        if (others == 0) return $"{Subj} {isv} the only living person on this rock";
        if (others == 1) return $"There {isv} one other person here";
        return $"There are {others} others here";
    }

    static string Feeling(Pawn p, string Subj, string isv)
    {
        if (p.InMentalState) return $"{Subj} {isv} not in control of {ProseWords.Possessive(p.gender.ToString())} own head right now";
        if (p.Downed && !p.IsBaby()) return $"{Subj} {isv} down and in real pain";

        var m = p.needs?.mood;
        if (m == null) return null;
        var pct = m.CurLevelPercentage;
        if (pct < 0.25f) return $"{Subj} {isv} close to breaking";
        if (pct < 0.45f) return $"{Subj} {isv} worn down";
        if (pct > 0.80f) return $"{Subj} {isv} in genuinely good spirits";
        return null;   // an unremarkable mood earns no sentence
    }

    static string Carrying(Pawn p, string Subj, string isv)
    {
        var thoughts = ContextHelper.GetThoughts(p).Keys
            .Select(t => CommonUtil.Sanitize(t.LabelCap ?? t.def?.defName))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(ProseWords.Mid)
            .Take(3).ToList();
        return thoughts.Count == 0 ? null : $"On {ProseWords.Possessive(p.gender.ToString())} mind: {ProseWords.Join(thoughts)}";
    }

    static string Want(Pawn p, string name)
    {
        var top = p.skills?.skills?.OrderByDescending(s => s.Level).FirstOrDefault();
        if (top == null || top.Level < 6) return null;
        return $"What {name} wants right now: for the {ProseWords.Mid(top.def.label)} work to hold.";
    }

    static string Article(string s) =>
        !string.IsNullOrEmpty(s) && "aeiouAEIOU".IndexOf(s[0]) >= 0 ? "an" : "a";
}
