using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimTalk.Data;
using RimWorld;
using Verse;

namespace RimTalk.Prose;

/// <summary>
/// Prompt and response handling for personality expansion. rim-universe #51.
///
/// One LLM call per pawn that reads backstory, traits, skills, and biome, then
/// produces a rich personality with preferences. Everything downstream reads from
/// this instead of making separate calls.
/// </summary>
public static class PersonalityText
{
    public static string Prompt(Pawn pawn)
    {
        if (pawn == null) return null;

        var sb = new StringBuilder();
        sb.AppendLine("Expand this character's backstory into a rich personality.");
        sb.AppendLine();
        sb.AppendLine("From their background, derive:");
        sb.AppendLine("- A 2-3 paragraph narrative expanding their history");
        sb.AppendLine("- Their speech style (one word: folksy, formal, clipped, rambling, dry, warm)");
        sb.AppendLine("- 2-3 personality quirks (habits, preferences, pet peeves)");
        sb.AppendLine("- 4-5 topics they bring up in idle conversation");
        sb.AppendLine("- Preferences: one food they love, one they hate; one animal they'd want");
        sb.AppendLine("  as a companion, one they despise; one material they prefer working with");
        sb.AppendLine();
        sb.AppendLine("Preferences must come from the available lists below. Pick what fits");
        sb.AppendLine("their personality — a farmer loves simple food, a noble loves fine meals.");
        sb.AppendLine();

        // Backstory
        sb.AppendLine("[Character]");
        sb.AppendLine($"Name: {pawn.LabelShort}");

        if (pawn.story?.Childhood != null)
        {
            sb.AppendLine($"Childhood: {pawn.story.Childhood.title}");
            if (!string.IsNullOrWhiteSpace(pawn.story.Childhood.description))
                sb.AppendLine(pawn.story.Childhood.description.Trim());
        }

        if (pawn.story?.Adulthood != null)
        {
            sb.AppendLine($"Adulthood: {pawn.story.Adulthood.title}");
            if (!string.IsNullOrWhiteSpace(pawn.story.Adulthood.description))
                sb.AppendLine(pawn.story.Adulthood.description.Trim());
        }

        // Traits
        var traits = pawn.story?.traits?.allTraits?
            .Where(t => t?.def != null)
            .Select(t => t.LabelCap)
            .ToList();
        if (traits?.Count > 0)
            sb.AppendLine($"Traits: {string.Join(", ", traits)}");

        // Top skills
        var skills = pawn.skills?.skills?
            .Where(s => s?.def != null && !s.TotallyDisabled)
            .OrderByDescending(s => s.Level)
            .Take(5)
            .Select(s => $"{s.def.label} ({s.Level})")
            .ToList();
        if (skills?.Count > 0)
            sb.AppendLine($"Best skills: {string.Join(", ", skills)}");

        sb.AppendLine();

        // Available preferences (biome-filtered where possible)
        var map = pawn.Map;
        sb.AppendLine("[Available preferences]");

        // Foods - common ingestibles
        var foods = GetAvailableFoods();
        sb.AppendLine($"Foods: {string.Join(", ", foods)}");

        // Animals - from biome if available
        var animals = GetAvailableAnimals(map);
        sb.AppendLine($"Animals: {string.Join(", ", animals)}");

        // Materials
        var materials = GetAvailableMaterials();
        sb.AppendLine($"Materials: {string.Join(", ", materials)}");

        // Ideology rituals if applicable
        var rituals = GetRituals(pawn);
        if (rituals.Count > 0)
        {
            sb.AppendLine($"Rituals (from their beliefs): {string.Join(", ", rituals)}");
            sb.AppendLine("Also pick a ritual they especially enjoy and one they dislike.");
        }

        sb.AppendLine();
        sb.AppendLine("Reply with JSON only:");
        sb.AppendLine("{");
        sb.AppendLine("  \"narrative\": \"...\",");
        sb.AppendLine("  \"speechStyle\": \"...\",");
        sb.AppendLine("  \"quirks\": [\"...\", \"...\"],");
        sb.AppendLine("  \"topics\": [\"...\", \"...\", \"...\"],");
        sb.AppendLine("  \"lovedFood\": \"...\",");
        sb.AppendLine("  \"hatedFood\": \"...\",");
        sb.AppendLine("  \"lovedAnimal\": \"...\",");
        sb.AppendLine("  \"hatedAnimal\": \"...\",");
        sb.AppendLine("  \"lovedMaterial\": \"...\",");
        if (rituals.Count > 0)
        {
            sb.AppendLine("  \"lovedRitual\": \"...\",");
            sb.AppendLine("  \"hatedRitual\": \"...\"");
        }
        sb.AppendLine("}");

        return sb.ToString();
    }

    /// <summary>
    /// Convert LLM response to a PawnPersonality, or null if invalid.
    /// </summary>
    public static PawnPersonality Accept(Pawn pawn, PersonalityExpansionData data)
    {
        if (pawn == null || data == null) return null;
        if (string.IsNullOrWhiteSpace(data.Narrative)) return null;

        // Basic validation - narrative should be substantial
        if (data.Narrative.Length < 100) return null;
        if (data.Narrative.Length > 3000) return null;

        var personality = new PawnPersonality(pawn)
        {
            Narrative = data.Narrative.Trim(),
            SpeechStyle = Clean(data.SpeechStyle),
            Quirks = data.Quirks?.Where(q => !string.IsNullOrWhiteSpace(q))
                         .Select(q => q.Trim()).Take(5).ToList() ?? new List<string>(),
            Topics = data.Topics?.Where(t => !string.IsNullOrWhiteSpace(t))
                         .Select(t => t.Trim()).Take(6).ToList() ?? new List<string>(),
            LovedFood = Clean(data.LovedFood),
            HatedFood = Clean(data.HatedFood),
            LovedAnimal = Clean(data.LovedAnimal),
            HatedAnimal = Clean(data.HatedAnimal),
            LovedMaterial = Clean(data.LovedMaterial),
            LovedRitual = Clean(data.LovedRitual),
            HatedRitual = Clean(data.HatedRitual),
        };

        return personality;
    }

    static string Clean(string s) =>
        string.IsNullOrWhiteSpace(s) ? null : s.Trim().ToLowerInvariant();

    /// <summary>Common foods a colonist might have opinions about.</summary>
    static List<string> GetAvailableFoods()
    {
        // Core foods that exist in most games
        return new List<string>
        {
            "simple meal", "fine meal", "lavish meal", "nutrient paste",
            "pemmican", "packaged survival meal", "berries", "rice",
            "corn", "potatoes", "raw meat", "insect jelly", "milk"
        };
    }

    /// <summary>Animals from the biome, or common ones if no map.</summary>
    static List<string> GetAvailableAnimals(Map map)
    {
        if (map?.Biome != null)
        {
            var biomeAnimals = map.Biome.AllWildAnimals?
                .Where(a => a != null)
                .Select(a => a.label)
                .Distinct()
                .OrderBy(a => a)
                .Take(15)
                .ToList();

            if (biomeAnimals?.Count > 5)
                return biomeAnimals;
        }

        // Fallback to common animals
        return new List<string>
        {
            "dog", "cat", "horse", "muffalo", "alpaca", "chicken",
            "rat", "squirrel", "deer", "boar", "wolf", "bear", "thrumbo"
        };
    }

    /// <summary>Common building materials.</summary>
    static List<string> GetAvailableMaterials()
    {
        return new List<string>
        {
            "wood", "steel", "plasteel", "stone", "marble", "granite",
            "sandstone", "slate", "limestone", "jade", "gold", "silver", "uranium"
        };
    }

    /// <summary>Rituals from the pawn's ideology, if any.</summary>
    static List<string> GetRituals(Pawn pawn)
    {
        if (pawn?.Ideo == null) return new List<string>();

        var rituals = pawn.Ideo.PreceptsListForReading?
            .Where(p => p?.def?.issue?.label != null)
            .Where(p => p.def.defName.Contains("Ritual") ||
                        p.def.issue.label.ToLowerInvariant().Contains("ritual"))
            .Select(p => p.def.issue.label)
            .Distinct()
            .Take(8)
            .ToList();

        return rituals ?? new List<string>();
    }
}
