using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Prose;

/// <summary>
/// Where this is, how long it has been here, and how it is doing.
///
/// rim-universe #23. Three things a colonist could not say before:
///
///   biome        grep found zero occurrences of "biome" in the whole mod. A pawn
///                could be told it was -34C and not that they live on an ice sheet.
///   place        RimWorld names the settlement; the prompt carried none of it, so
///                every reference to somewhere had to be "this place" or "out there".
///   tenure       nothing distinguished a pawn's first quadrum from their eleventh
///                year, which for a player whose memorable stories are all about
///                accumulation is the most conspicuous absence there was.
///
/// And the condition, which JK asked for by name and which #28's goals need: a pawn
/// cannot decide the base needs better defences without being able to see that the
/// food problem is solved.
/// </summary>
public class ColonyFacts
{
    public string SettlementName;
    public string BiomeLabel;
    public int DaysOld = -1;         // -1 when unknown

    /// <summary>Days of food in store at the current population. -1 when unknown.</summary>
    public float FoodDays = -1f;
    public int MedicineCount = -1;
    public bool? HasPower;           // null when unknown

    // #28's predicates. Added here rather than in a parallel ColonyState: one
    // gatherer, one shape. A second reader of the same map is the "second code path"
    // that got the stale config fixed in one entry point and not the other.
    /// <summary>Free colonists on the map. 0 when unknown, which reads as no goal.</summary>
    public int Colonists;
    /// <summary>How many of them have no bed assigned. -1 would be indistinguishable from none.</summary>
    public int ColonistsWithoutBed;
    /// <summary>Turrets and traps. -1 when unknown.</summary>
    public int Turrets = -1;
}

public static class ProseColonyText
{
    /// <summary>Null when there is nothing worth a sentence.</summary>
    public static string Compose(ColonyFacts c)
    {
        if (c == null) return null;
        var lines = new List<string> { Place(c), Supplies(c) };
        var text = string.Join(" ", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    /// <summary>
    /// Whether the colony's state is worth a conversation.
    ///
    /// Measured: stating the facts in the scene got them mentioned 0-18% of the time,
    /// and pointing at them from the instruction got 33-72%. So the instruction is
    /// where it works — and that is exactly why it has to be gated. A clause that
    /// fires every time makes every conversation about the food stores, which is the
    /// same failure as never mentioning them.
    /// </summary>
    public static bool IsPressing(ColonyFacts c)
    {
        if (c == null) return false;
        if (c.FoodDays >= 0f && c.FoodDays < 4f) return true;   // days, not weeks
        if (c.DaysOld >= 0 && c.DaysOld < 4) return true;       // still landing
        if (c.HasPower == false) return true;                   // had power, hasn't now
        return false;
    }

    static string Place(ColonyFacts c)
    {
        var name = string.IsNullOrWhiteSpace(c.SettlementName) ? null : c.SettlementName.Trim();
        var biome = string.IsNullOrWhiteSpace(c.BiomeLabel) ? null : ProseWords.Mid(c.BiomeLabel);

        string where = null;
        if (name != null && biome != null) where = $"This is {name}, in {ProseWords.Article(biome)} {biome}.";
        else if (name != null) where = $"This is {name}.";
        else if (biome != null) where = $"This is {ProseWords.Article(biome)} {biome}.";

        var age = Tenure(c.DaysOld);
        // Tenure hangs off the place, not off the pawn. Colony age is what the game
        // knows; how long THIS pawn has been here waits for the arrival log (#37), and
        // saying it of a pawn who joined last week would be quietly false.
        if (age != null) where = where == null ? $"The colony is {age} old." : $"{where} The colony is {age} old.";
        return where;
    }

    /// <summary>
    /// RimWorld's calendar: 15 days to a quadrum, 60 to a year. Rounded down and named
    /// in the largest unit that is not a lie — "a quadrum" reads as lived-in in a way
    /// "17 days" does not, and precision here buys nothing.
    /// </summary>
    public static string Tenure(int days)
    {
        if (days < 0) return null;
        if (days == 0) return "a day";
        if (days == 1) return "a day";
        if (days < 15) return $"{days} days";

        var years = days / 60;
        if (years >= 1) return years == 1 ? "a year" : $"{years} years";

        var quadrums = days / 15;
        return quadrums == 1 ? "a quadrum" : $"{quadrums} quadrums";
    }

    static string Supplies(ColonyFacts c)
    {
        var parts = new List<string>();

        var food = Food(c.FoodDays);
        if (food != null) parts.Add(food);

        var med = Medicine(c.MedicineCount);
        if (med != null) parts.Add(med);

        if (c.HasPower.HasValue) parts.Add(c.HasPower.Value ? "the lights on" : "no power");

        if (parts.Count == 0) return null;
        return ProseWords.Cap(ProseWords.Join(parts)) + ".";
    }

    /// <summary>
    /// Bands, not a number. "8.3 days of food" is a readout; "about a week of food" is
    /// something a person would say, and the whole point of #23's example sentence —
    /// "first time I've had a week of food in storage" — is that the pawn NOTICES.
    /// </summary>
    static string Food(float days)
    {
        if (days < 0f) return null;
        if (days < 1f) return "nothing much to eat";
        if (days < 4f) return "a few days of food";
        if (days < 10f) return "about a week of food";
        if (days < 30f) return "food enough for a while";
        return "more food than they can eat";
    }

    static string Medicine(int count)
    {
        if (count < 0) return null;
        if (count == 0) return "no medicine";
        if (count < 5) return "a little medicine";
        return "medicine in the shelf";
    }
}
