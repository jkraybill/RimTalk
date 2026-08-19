using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using HarmonyLib;
using RimTalk.API;
using RimTalk.Data;
using RimTalk.Source.Data;
using RimTalk.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Service;

public static class ContextBuilder
{
    private static readonly MethodInfo VisibleHediffsMethod =
        AccessTools.Method(typeof(HealthCardUtility), "VisibleHediffs");

    public static string GetRaceContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeRace || !ModsConfig.BiotechActive || pawn.genes?.Xenotype == null)
            return null;
        return $"Race: {pawn.genes.XenotypeLabel}";
    }

    public static string GetNotableGenesContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeNotableGenes || !ModsConfig.BiotechActive ||
            pawn.genes?.GenesListForReading == null)
            return null;

        var notableGenes = pawn.genes.GenesListForReading
            .Where(g => g.def.biostatMet != 0 || g.def.biostatCpx != 0)
            .Select(g => g.def.LabelCap);

        // For Short level, limit to top 3 most impactful genes
        if (infoLevel == PromptService.InfoLevel.Short)
        {
            notableGenes = pawn.genes.GenesListForReading
                .Where(g => g.def.biostatMet != 0 || g.def.biostatCpx != 0)
                .OrderByDescending(g => Mathf.Abs(g.def.biostatMet) + g.def.biostatCpx)
                .Take(3)
                .Select(g => g.def.LabelCap);
        }

        if (notableGenes.Any())
            return $"Notable Genes: {string.Join(", ", notableGenes)}";
        return null;
    }

    public static string GetAllGenesContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeNotableGenes || !ModsConfig.BiotechActive ||
            pawn.genes?.GenesListForReading == null)
            return null;

        var genes = pawn.genes.GenesListForReading
            .Select(g => g.def?.LabelCap.ToString())
            .Where(label => !string.IsNullOrEmpty(label));

        if (genes.Any())
            return $"Genes: {string.Join(", ", genes)}";
        return null;
    }

    public static string GetIdeologyContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeIdeology || !ModsConfig.IdeologyActive || pawn.ideo?.Ideo == null)
            return null;

        var sb = new StringBuilder();
        var ideo = pawn.ideo.Ideo;

        // For Short level, skip ideology name and only show top 3 memes
        if (infoLevel == PromptService.InfoLevel.Short)
        {
            var memes = ideo.memes?
                .Where(m => m != null)
                .Take(3)
                .Select(m => m.LabelCap.Resolve())
                .Where(label => !string.IsNullOrEmpty(label));

            if (memes?.Any() == true)
                return $"Memes: {string.Join(", ", memes)}";
        }
        else
        {
            sb.Append($"Ideology: {ideo.name}");

            var memes = ideo.memes?
                .Where(m => m != null)
                .Select(m => m.LabelCap.Resolve())
                .Where(label => !string.IsNullOrEmpty(label));

            if (memes?.Any() == true)
                sb.Append($"\nMemes: {string.Join(", ", memes)}");

            return sb.ToString();
        }

        return null;
    }

    public static string GetBackstoryContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeBackstory)
            return null;

        var sb = new StringBuilder();

        // For Short level, only include childhood title
        if (infoLevel == PromptService.InfoLevel.Short)
        {
            if (pawn.story?.Adulthood != null)
                return $"Background: {pawn.story.Adulthood.TitleCapFor(pawn.gender)}";
        }
        else
        {
            if (pawn.story?.Childhood != null)
                sb.Append(ContextHelper.FormatBackstory("Childhood", pawn.story.Childhood, pawn, infoLevel));

            if (pawn.story?.Adulthood != null)
            {
                if (sb.Length > 0) sb.Append("\n");
                sb.Append(ContextHelper.FormatBackstory("Adulthood", pawn.story.Adulthood, pawn, infoLevel));
            }
        }

        return sb.Length > 0 ? sb.ToString() : null;
    }

    public static string GetTraitsContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeTraits)
            return null;

        var traits = new List<string>();
        foreach (var trait in pawn.story?.traits?.TraitsSorted ?? Enumerable.Empty<Trait>())
        {
            var degreeData = GenCollection.FirstOrDefault(trait.def.degreeDatas, d => d.degree == trait.Degree);
            if (degreeData != null)
            {
                var traitText = infoLevel == PromptService.InfoLevel.Full
                    ? $"{degreeData.label}:{CommonUtil.Sanitize(degreeData.description, pawn)}"
                    : degreeData.label;
                traits.Add(traitText);
            }
        }

        // For Short level, limit to top 3 traits
        if (infoLevel == PromptService.InfoLevel.Short && traits.Count > 3)
            traits = traits.Take(3).ToList();

        if (traits.Any())
        {
            var separator = infoLevel == PromptService.InfoLevel.Full ? "\n" : ",";
            var line = $"Traits: {string.Join(separator, traits)}";

            // One trait wins today. Without this the model averages the list into a
            // composite nobody, and averages it the same way tomorrow. #35.
            var dominant = GetDominantTrait(pawn);
            if (dominant != null)
                line += $"\nRight now: {dominant} is winning.";

            return line;
        }

        return null;
    }

    public static string GetSkillsContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeSkills)
            return null;

        var skills = pawn.skills?.skills?.Select(s => $"{s.def.label}: {s.Level}");
        if (skills?.Any() == true)
            return $"Skills: {string.Join(", ", skills)}";
        return null;
    }

    public static string GetHealthContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeHealth)
            return null;

        var hediffs = (IEnumerable<Hediff>)VisibleHediffsMethod.Invoke(null, [pawn, false]);

        // For Short level, only show top 3 most recent/severe hediffs
        if (infoLevel == PromptService.InfoLevel.Short)
        {
            hediffs = hediffs
                .OrderByDescending(h => h.Visible ? 1 : 0)
                .ThenByDescending(h => h.Severity)
                .ThenByDescending(h => h.ageTicks)
                .Take(3);
        }

        var healthInfo = string.Join(",", hediffs
            .GroupBy(h => h.def)
            .Select(g => $"{g.Key.label}({string.Join(",", g.Select(h => h.Part?.Label ?? ""))})"));

        if (!string.IsNullOrEmpty(healthInfo))
            return $"Health: {healthInfo}";
        return null;
    }

    public static string GetMoodContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeMood)
            return null;

        var m = pawn.needs?.mood;
        if (m?.MoodString != null)
        {
            string mood = pawn.Downed && !pawn.IsBaby()
                ? "Critical: Downed (in pain/distress)"
                : pawn.InMentalState
                    ? $"Mood: {pawn.MentalState?.InspectLine} (in mental break)"
                    : $"Mood: {m.MoodString} ({(int)(m.CurLevelPercentage * 100)}%)";
            return mood;
        }

        return null;
    }

    public static string GetThoughtsContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeThoughts)
            return null;

        var allThoughts = ContextHelper.GetThoughts(pawn);

        // For Short level, only include latest 3 thoughts
        var thoughts = infoLevel == PromptService.InfoLevel.Short
            ? allThoughts.Keys.Take(3).Select(t => CommonUtil.Sanitize(t.LabelCap ?? t.def?.defName ?? "UnknownThought"))
            : allThoughts.Keys.Select(t => CommonUtil.Sanitize(t.LabelCap ?? t.def?.defName ?? "UnknownThought"));

        if (thoughts.Any())
            return $"Memory: {string.Join(", ", thoughts)}";
        return null;
    }

    public static string GetAllThoughtsContext(Pawn pawn)
    {
        if (pawn?.needs?.mood?.thoughts == null)
            return null;

        var allThoughts = ContextHelper.GetThoughts(pawn);
        if (allThoughts.Count == 0)
            return null;

        var thoughts = allThoughts
            .OrderBy(kvp => kvp.Key.LabelCap)
            .Select(kvp =>
                $"{CommonUtil.Sanitize(kvp.Key.LabelCap ?? kvp.Key.def?.defName ?? "UnknownThought")}({kvp.Value.ToStringWithSign()})");

        return $"Thoughts: {string.Join(", ", thoughts)}";
    }

    public static string GetPrisonerSlaveContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludePrisonerSlaveStatus || (!pawn.IsSlave && !pawn.IsPrisoner))
            return null;

        return pawn.GetPrisonerSlaveStatus();
    }

    public static string GetRelationsContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeRelations)
            return null;

        return RelationsService.GetRelationsString(pawn);
    }

    public static string GetEquipmentContext(Pawn pawn, PromptService.InfoLevel infoLevel)
    {
        var contextSettings = Settings.Get().Context;
        if (!contextSettings.IncludeEquipment)
            return null;

        var equipment = new List<string>();
        if (pawn.equipment?.Primary != null)
            equipment.Add($"Weapon: {pawn.equipment.Primary.LabelCap}");

        var apparelLabels = pawn.apparel?.WornApparel?.Select(a => a.LabelCap);
        if (apparelLabels?.Any() == true)
            equipment.Add($"Apparel: {string.Join(", ", apparelLabels)}");

        if (equipment.Any())
            return $"Equipment: {string.Join(", ", equipment)}";
        return null;
    }

    public static void BuildDialogueType(StringBuilder sb, TalkRequest talkRequest, List<Pawn> pawns, string shortName, Pawn mainPawn)
    {
        BuildDialogueType(sb, talkRequest, pawns, shortName, mainPawn, out _, out _);
    }

    public static void BuildDialogueType(StringBuilder sb, TalkRequest talkRequest, List<Pawn> pawns, string shortName, Pawn mainPawn, out string intent, out string topic)
    {
        var intentSb = new StringBuilder();
        var topicSb = new StringBuilder();

        // What this pawn was talking about before the situation escalated. Carried
        // through rather than deleted; see the combat branch below.
        string preoccupation = null;

        if (talkRequest.TalkType.IsFromUser())
        {
            topicSb.Append($"{pawns[1].LabelShort}({pawns[1].GetRole()}) said to {shortName}: '{talkRequest.Prompt}'. ");

            var mode = Settings.Get().PlayerDialogueMode;
            bool multiTurn = mode == Settings.PlayerDialogueMode.AIDriven || (!pawns[1].IsPlayer() && mode != Settings.PlayerDialogueMode.Manual);

            intentSb.Append(multiTurn
                ? $"Generate multi turn dialogues starting after this (do not repeat initial dialogue), beginning with {shortName}"
                : $"Generate dialogue starting after this. Do not generate any further lines for {pawns[1].LabelShort}");

            sb.Append(topicSb).Append(intentSb);
        }
        else
        {
            if (pawns.Count == 1)
            {
                intentSb.Append($"{shortName} short monologue");
            }
            else if (mainPawn.IsInCombat() || mainPawn.GetMapRole() == MapRole.Invading)
            {
                // The topic used to be destroyed here (talkRequest.Prompt = null), which
                // deleted whatever the conversation was about the instant anything
                // dramatic started -- precisely when a small preoccupation colliding with
                // a large situation becomes worth having. Sam thinking about potatoes at
                // the gates of Mordor is the effect; nulling the potatoes forbids it.
                //
                // Demoted, not discarded. See rim-universe #34.
                if (talkRequest.TalkType != TalkType.Urgent && !mainPawn.InMentalState)
                    preoccupation = Rand.Value < Settings.Get().Context.PreoccupationChance
                        ? talkRequest.Prompt
                        : null;

                talkRequest.TalkType = TalkType.Urgent;
                intentSb.Append(mainPawn.IsSlave || mainPawn.IsPrisoner
                    ? $"{shortName} dialogue short (worry)"
                    : $"{shortName} dialogue short, urgent tone ({mainPawn.GetMapRole().ToString().ToLower()}/command)");
            }
            else
            {
                intentSb.Append($"{shortName} starts conversation, taking turns");
            }

            if (mainPawn.InMentalState)
                topicSb.Append("be dramatic (mental break)");
            else if (mainPawn.Downed && !mainPawn.IsBaby())
                topicSb.Append("(downed in pain. Short, strained dialogue)");
            else if (talkRequest.Prompt != null)
                topicSb.Append(talkRequest.Prompt);

            // Stated as a secondary concern so the model keeps the urgency AND the
            // triviality, instead of averaging them into one flat register.
            if (preoccupation != null)
                topicSb.Append(topicSb.Length > 0 ? "\n" : "")
                       .Append($"{shortName} is still preoccupied with: {preoccupation}");

            AppendScaleGap(topicSb, mainPawn, preoccupation);

            sb.Append(intentSb);
            if (topicSb.Length > 0)
                sb.Append("\n").Append(topicSb);
        }

        intent = intentSb.ToString();
        topic = topicSb.ToString();
    }

    /// <summary>
    /// States the mismatch between how big the situation is and how big this pawn's
    /// concerns are. rim-universe #35.
    ///
    /// Humour and tragedy are the same mechanism running in opposite directions: a
    /// character whose concerns are the wrong SIZE for their situation. A model is
    /// very good at inhabiting a mismatch someone else has named and very bad at
    /// inventing one, so the game computes it and the prompt states it as fact.
    ///
    /// STATE it, do not INSTRUCT it. The first version ended "Let that mismatch
    /// show", which is a dial the model can see and will therefore play to --
    /// producing strained metaphor rather than a character who happens to be
    /// thinking about the wrong thing. Four roundtable reviewers predicted exactly
    /// this ("performed incongruity is quippy"), and JK hit it in game within the
    /// hour: "If I let go of this code, I'm not sure the colony's head won't crack."
    ///
    /// The roundtable's better formulation, adopted: hold both scales without
    /// collapsing either. "Both are true at once" says that without asking for a
    /// performance.
    /// </summary>
    public static void AppendScaleGap(StringBuilder sb, Pawn mainPawn, string preoccupation)
    {
        if (!Settings.Get().Context.IncludeScaleGap) return;
        if (mainPawn?.Map == null) return;

        var situation = ScaleDescriber.Situation(mainPawn.Map);
        if (situation == null) return;

        var concern = preoccupation != null
            ? "something small and unfinished"
            : ScaleDescriber.ConcernFor(mainPawn);

        sb.Append(sb.Length > 0 ? "\n" : "")
          .Append($"Situation: {situation}. On {mainPawn.LabelShort}'s mind: {concern}. Both are true at once.");
    }

    /// <summary>
    /// Picks ONE trait and declares it dominant for this exchange. rim-universe #35.
    ///
    /// A comma list of three traits makes the model average them into a composite
    /// nobody. GURPS solves this with self-control numbers: the trait is a contest
    /// the character can lose, and losing is the interesting outcome.
    /// </summary>
    public static string GetDominantTrait(Pawn pawn)
    {
        if (!Settings.Get().Context.IncludeDominantTrait) return null;

        var traits = pawn?.story?.traits?.TraitsSorted?.ToList();
        if (traits == null || traits.Count == 0) return null;

        // Rand, not UnityEngine.Random: RimWorld seeds Rand so a reloaded save
        // reproduces the same rolls.
        var winner = traits[Rand.Range(0, traits.Count)];
        var degree = GenCollection.FirstOrDefault(winner.def.degreeDatas, d => d.degree == winner.Degree);
        return degree?.label;
    }

    public static void BuildLocationContext(StringBuilder sb, ContextSettings contextSettings, Pawn mainPawn)
    {
        if (!contextSettings.IncludeLocationAndTemperature) return;
        
        var locationStatus = ContextHelper.GetPawnLocationStatus(mainPawn);
        if (string.IsNullOrEmpty(locationStatus)) return;
        
        var temperature = Mathf.RoundToInt(mainPawn.Position.GetTemperature(mainPawn.Map));
        var room = mainPawn.GetRoom();
        var roomRole = room is { PsychologicallyOutdoors: false } ? room.Role?.label ?? "Room" : "";

        var locationInfo = string.IsNullOrEmpty(roomRole)
            ? $"{locationStatus};{temperature}C"
            : $"{locationStatus};{temperature}C;{roomRole}";
        
        // Apply pawn hooks (location is now a pawn property)
        locationInfo = ContextHookRegistry.ApplyPawnHooks(
            ContextCategories.Pawn.Location, mainPawn, locationInfo);
        sb.Append($"\nLocation: {locationInfo}");
    }

    public static void BuildEnvironmentContext(StringBuilder sb, ContextSettings contextSettings, Pawn mainPawn)
    {
        if (contextSettings.IncludeTerrain)
        {
            var terrain = mainPawn.Position.GetTerrain(mainPawn.Map);
            if (terrain != null)
            {
                var value = ContextHookRegistry.ApplyPawnHooks(
                    ContextCategories.Pawn.Terrain, mainPawn, terrain.LabelCap);
                sb.Append($"\nTerrain: {value}");
            }
        }

        if (contextSettings.IncludeBeauty)
        {
            var nearbyCells = ContextHelper.GetNearbyCells(mainPawn);
            if (nearbyCells.Count > 0)
            {
                var beautySum = nearbyCells.Sum(c => BeautyUtility.CellBeauty(c, mainPawn.Map));
                var value = ContextHookRegistry.ApplyPawnHooks(
                    ContextCategories.Pawn.Beauty, mainPawn, Describer.Beauty(beautySum / nearbyCells.Count));
                sb.Append($"\nCellBeauty: {value}");
            }
        }

        var pawnRoom = mainPawn.GetRoom();
        if (contextSettings.IncludeCleanliness && pawnRoom is { PsychologicallyOutdoors: false })
        {
            var value = ContextHookRegistry.ApplyPawnHooks(
                ContextCategories.Pawn.Cleanliness, mainPawn,
                Describer.Cleanliness(pawnRoom.GetStat(RoomStatDefOf.Cleanliness)));
            sb.Append($"\nCleanliness: {value}");
        }

        if (contextSettings.IncludeSurroundings)
        {
            var surroundingsText = ContextHelper.CollectNearbyContextText(mainPawn, 3);
            if (!string.IsNullOrEmpty(surroundingsText))
            {
                var value = ContextHookRegistry.ApplyPawnHooks(
                    ContextCategories.Pawn.Surroundings, mainPawn, surroundingsText);
                sb.Append("\nSurroundings:\n");
                sb.Append(value);
            }
        }
    }

    [Obsolete("Use CommonUtil.Sanitize instead. Kept for backward compatibility.")]
    public static string Sanitize(string text, Pawn pawn = null)
    {
        return CommonUtil.Sanitize(text, pawn);
    }
}
