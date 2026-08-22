using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// What has been happening lately, as clauses. The delta half of rim-universe #30.
///
/// JK's example is not a callback to the last conversation, it is a callback plus a
/// delta: they last complained about the rice, a pig has been killed since, and the
/// line only exists because both halves are in the prompt. This is the second half.
/// </summary>
public static class Chronicle
{
    /// <summary>
    /// A hundred and twenty. A busy colony records a handful of these a day, so this
    /// is roughly a quadrum of history — long enough that two colonists who have not
    /// spoken in a fortnight still get a delta, short enough to stay cheap in a save.
    /// </summary>
    public const int MaxEntries = 120;

    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    public static List<ChronicleEntry> All => Comp?.ChronicleEntries ?? new List<ChronicleEntry>();

    /// <summary>
    /// Record, unless the same thing has just been recorded. A hunter kills five
    /// boars before lunch and a party writes one tale per attendee; without the
    /// dedupe the delta is the same row five times and the bounded list fills with
    /// it. Returns whether anything was stored, so callers can log honestly.
    /// </summary>
    public static bool Record(int tick, string kind, string key, string clause)
    {
        var comp = Comp;
        if (comp == null || string.IsNullOrWhiteSpace(clause)) return false;

        var entries = comp.ChronicleEntries;
        for (var i = entries.Count - 1; i >= 0; i--)
        {
            var e = entries[i];
            if (e == null) continue;
            if (tick - e.Tick > TaleClause.DedupeTicks) break;   // list is append-ordered
            if (e.Key == key) return false;
        }

        entries.Add(new ChronicleEntry(tick, kind, key, clause));
        NarrativeMath.Trim(entries, MaxEntries);
        return true;
    }

    /// <summary>
    /// What has happened since a given tick, newest first, as clauses.
    ///
    /// Deliberately NOT filtered by who witnessed it. A colony of eight eats the same
    /// meals and walks past the same half-built smithy; treating "did Kess see the
    /// boar die" as the test for whether Kess can mention the pork would be a fidelity
    /// win on paper and wrong about how a small settlement works. Deaths keep their
    /// witness distinction because there the difference is the whole point.
    /// </summary>
    public static List<string> Since(int afterTick, int max)
    {
        var comp = Comp;
        if (comp == null) return new List<string>();

        var clauses = NarrativeMath
            .Since(comp.ChronicleEntries.Where(e => e != null), afterTick, e => e.Tick, max)
            .Select(e => e.Clause)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .ToList();

        // Deaths live in the other store and outrank everything here, so they go on
        // the front rather than competing for the same slots.
        var deaths = NarrativeMath
            .Since(NarrativeStore.All.Where(e => e != null), afterTick, e => e.Tick, 1)
            .Select(e => string.IsNullOrWhiteSpace(e.Detail) ? $"{e.Subject} died" : $"{e.Subject} died ({e.Detail})")
            .ToList();

        deaths.AddRange(clauses);
        return deaths.Take(max).ToList();
    }
}
