using System;
using System.Collections.Generic;
using RimTalk.Prose;

namespace RimTalk.Narrative;

/// <summary>
/// The event harvest, generalised past the death slice.
///
/// The roadmap's "Then" tier says "event harvest generalised from the death slice,
/// one class at a time", and #30, #21 and #22 all need the same thing: a list of
/// what has happened lately that a colonist could bring up. Three consumers is the
/// signal the roundtable said to build on.
///
/// **RimWorld already keeps this list.** It records a Tale for anything memorable —
/// that is what the art-description system reads — and every one goes through a
/// single static call. One patch point covers sixty event classes; the alternative
/// was sixty Harmony patches written one at a time, which is what "one class at a
/// time" would have cost.
///
/// What a Tale is NOT is prose. Its own summary is written to be carved into a
/// sculpture. These clauses are written to sit in the middle of a sentence somebody
/// says out loud, so the mapping is by hand, it is bounded, and it runs in the test
/// project rather than being inspected.
/// </summary>
public static class TaleClause
{
    /// <summary>
    /// The colony-salient tales, by defName.
    ///
    /// Deliberately short. The store is bounded at 200 events, so what is left out
    /// matters as much as what is in: one firefight records a Wounded and a Downed
    /// per casualty and a Killed* per kill, and harvesting those would evict a
    /// decade of colony history in favour of forty rows saying somebody got shot.
    /// Chores go too — StruckMineable and Vomited fire constantly and mean nothing.
    /// </summary>
    public static readonly string[] Harvested =
    {
        // Food and animals — JK's own example turns on this one.
        "Hunted", "TamedAnimal", "BondedWithAnimal",

        // Things that now exist because somebody made them.
        "CompletedLongConstructionProject", "CompletedLongCraftingProject",
        "CraftedArt", "FinishedResearchProject", "MinedValuable",

        // Who is here, and who is with whom.
        "Recruited", "Marriage", "BecameLover", "Breakup", "SocialFight", "GaveBirth",

        // Occasions the whole colony was at.
        "AttendedParty", "AttendedConcert", "TileSettled",

        // Rare enough to be worth remembering for years.
        "KilledMajorThreat", "GainedMasterSkillWithPassion", "ExecutedPrisoner",
        "DidSurgery", "IllnessRevealed", "CaravanFormed", "LaunchedShip",
    };

    static readonly HashSet<string> HarvestedSet = new(Harvested, StringComparer.Ordinal);

    /// <summary>
    /// Events the colony experiences once between them, however many pawns record
    /// one. A party writes a tale per attendee; without this the "since then" list
    /// is the same party six times.
    /// </summary>
    static readonly HashSet<string> ColonyWide =
        new(new[] { "AttendedParty", "AttendedConcert", "TileSettled" }, StringComparer.Ordinal);

    public static bool IsHarvested(string kind) =>
        !string.IsNullOrWhiteSpace(kind) && HarvestedSet.Contains(kind.Trim());

    /// <summary>
    /// One clause, no trailing stop — these are joined into the middle of a sentence.
    ///
    /// Null when the tale's own arguments cannot fill it. A missing clause is the
    /// right failure: the event is still stored, it simply contributes nothing to
    /// this prompt, and "Kess and  were married" is worse than silence.
    /// </summary>
    public static string For(string kind, string subject, string other, string detail)
    {
        var k = (kind ?? "").Trim();
        var s = Trim(subject);
        var o = Trim(other);
        var d = detail == null ? null : ProseWords.Mid(detail);
        if (string.IsNullOrWhiteSpace(d)) d = null;

        switch (k)
        {
            // Needs a person and a thing.
            case "Hunted":                           return Both(s, d, $"{s} hunted {A(d)}");
            case "TamedAnimal":                      return Both(s, d, $"{s} tamed {A(d)}");
            case "BondedWithAnimal":                 return Both(s, d, $"{s} bonded with {A(d)}");
            case "CompletedLongConstructionProject": return Both(s, d, $"{s} finished the {d}");
            case "CompletedLongCraftingProject":     return Both(s, d, $"{s} finished making {A(d)}");
            case "CraftedArt":                       return Both(s, d, $"{s} made {A(d)}");
            case "FinishedResearchProject":          return Both(s, d, $"{s} finished the research into {d}");
            case "MinedValuable":                    return Both(s, d, $"{s} struck {d}");
            case "KilledMajorThreat":                return Both(s, d, $"{s} brought down {A(d)}");
            case "GainedMasterSkillWithPassion":     return Both(s, d, $"{s} became a master at {d}");
            case "IllnessRevealed":                  return Both(s, d, $"{s} was found to have {d}");

            // Needs two people.
            case "Marriage":                         return Both(s, o, $"{s} and {o} were married");
            case "BecameLover":                      return Both(s, o, $"{s} and {o} became lovers");
            case "Breakup":                          return Both(s, o, $"{s} and {o} broke up");
            case "SocialFight":                      return Both(s, o, $"{s} and {o} came to blows");
            case "DidSurgery":                       return Both(s, o, $"{s} operated on {o}");

            // The tale is (recruiter, recruitee) and the colony remembers the arrival,
            // not the persuasion.
            case "Recruited":                        return o == null ? null : $"{o} joined the colony";

            // Needs one person.
            case "GaveBirth":                        return s == null ? null : $"{s} gave birth";
            case "ExecutedPrisoner":                 return s == null ? null : $"{s} executed a prisoner";
            case "CaravanFormed":                    return s == null ? null : $"{s} left with a caravan";
            case "LaunchedShip":                     return s == null ? null : $"{s} launched the ship";

            // Needs nobody in particular.
            case "AttendedParty":                    return "there was a party";
            case "AttendedConcert":                  return "there was a concert";
            case "TileSettled":                      return "the colony was founded here";

            default:                                 return null;
        }
    }

    /// <summary>
    /// Identity for "is this the same thing that already happened". A hunter kills
    /// five boars before lunch; without this the delta is five identical rows and
    /// the bounded store fills with them.
    /// </summary>
    public static string DedupeKey(string kind, string subject, string detail)
    {
        var k = (kind ?? "").Trim();
        return ColonyWide.Contains(k) ? k : $"{k}|{Norm(subject)}|{Norm(detail)}";
    }

    /// <summary>
    /// Six in-game hours. Long enough that a morning's hunting is one remembered
    /// event, short enough that a second boar the next day is a second event.
    /// </summary>
    public const int DedupeTicks = 15000;

    static string Trim(string s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();
    static string Norm(string s) => (s ?? "").Trim().ToLowerInvariant();
    static string A(string noun) => $"{ProseWords.Article(noun)} {noun}";

    /// <summary>Render only when both halves the sentence needs are present.</summary>
    static string Both(string a, string b, string rendered) => a == null || b == null ? null : rendered;
}
