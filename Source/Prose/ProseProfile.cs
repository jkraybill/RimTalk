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
/// This file reads the game and nothing else. Every phrasing decision lives in
/// <see cref="ProseProfileText"/>, which has no RimWorld dependency and is executed
/// by the test suite. Keep it that way: a sentence assembled here is a sentence
/// nothing can run.
///
/// #37's arrival log will generate a better first paragraph per pawn and cache it;
/// when that exists it replaces <see cref="PawnFacts.AdulthoodTitle"/>'s sentence.
/// </summary>
public static class ProseProfile
{
    public static string Build(Pawn pawn, PromptService.InfoLevel level)
    {
        var facts = Gather(pawn, level);
        return facts == null ? "" : ProseProfileText.Compose(facts);
    }

    /// <summary>Reads the game. Internal so the mod's own dev tooling can dump it.</summary>
    public static PawnFacts Gather(Pawn pawn, PromptService.InfoLevel level)
    {
        if (pawn == null) return null;

        var f = new PawnFacts
        {
            Name = pawn.LabelShort,
            Gender = pawn.gender.ToString(),
            Age = pawn.ageTracker?.AgeBiologicalYears ?? 0,
            AdulthoodTitle = pawn.story?.Adulthood?.TitleFor(pawn.gender)
                             ?? pawn.story?.Childhood?.TitleFor(pawn.gender),
            Role = pawn.GetRole(),
            IsPrisoner = pawn.IsPrisoner,
            IsSlave = pawn.IsSlave,
            OtherColonists = pawn.Map?.mapPawns?.FreeColonistsSpawned?.Count(c => c != pawn && !c.Dead) ?? 0,
            InMentalState = pawn.InMentalState,
            Downed = pawn.Downed,
            IsBaby = pawn.IsBaby(),
            MoodPercent = pawn.needs?.mood?.CurLevelPercentage,
        };

        f.TraitLabels = (pawn.story?.traits?.TraitsSorted ?? Enumerable.Empty<Trait>())
            .Select(t => GenCollection.FirstOrDefault(t.def.degreeDatas, d => d.degree == t.Degree)?.label)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .ToList();

        f.Body = (pawn.health?.hediffSet?.hediffs ?? Enumerable.Empty<Hediff>())
            .Where(h => h.Visible && h.def?.label != null)
            .Select(h => new BodyNote { Label = h.def.label, Part = h.Part?.Label, Severity = h.Severity })
            .ToList();

        f.Thoughts = ContextHelper.GetThoughts(pawn).Keys
            .Select(t => CommonUtil.Sanitize(t.LabelCap ?? t.def?.defName))
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        var top = pawn.skills?.skills?.OrderByDescending(s => s.Level).FirstOrDefault();
        if (top != null)
        {
            // defName, not label: vanilla SkillDefs carry `skillLabel`, so `label` is
            // null for all twelve and the old line shipped "for the  work to hold".
            f.TopSkillDefName = top.def.defName;
            f.TopSkillLabel = top.def.label ?? top.def.skillLabel;
            f.TopSkillLevel = top.Level;
        }

        return f;
    }
}
