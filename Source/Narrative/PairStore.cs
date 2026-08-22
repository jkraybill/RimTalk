using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Prose;
using RimTalk.Util;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// Conversation history keyed by PAIR rather than by pawn. rim-universe #30.
///
/// The live store is a ConcurrentDictionary for the same reason TalkHistory's is:
/// it is written from the thread that finishes a streaming call. The world component
/// is only the persistence medium, filled on save and drained on load — the pattern
/// #9 landed on, reused rather than re-invented.
/// </summary>
public static class PairStore
{
    /// <summary>
    /// Three hundred. Twenty colonists is a hundred and ninety pairs, and the
    /// visitors and prisoners who pass through push past that over a long game.
    /// Evicted least-recently-met first.
    /// </summary>
    public const int MaxPairs = 300;

    /// <summary>Six lines. Two more than the render shows, so a trimmed exchange still has an answer in it.</summary>
    public const int MaxExchangeLines = 6;

    static readonly ConcurrentDictionary<long, PairRecord> Pairs = new();

    /// <summary>
    /// Remember an exchange between everyone who spoke in it.
    ///
    /// Every unordered pair among the speakers, not just the first two: a three-hander
    /// is three relationships, and recording only (first, second) is the same
    /// list-position bug #7 was.
    /// </summary>
    public static void Record(IList<Pawn> speakers, IList<string> lines, int tick)
    {
        if (speakers == null || lines == null) return;

        var people = speakers.Where(p => p != null).Distinct().ToList();
        if (people.Count < 2) return;

        var kept = lines.Where(l => !string.IsNullOrWhiteSpace(l))
                        .Select(l => l.Trim())
                        .TakeLast(MaxExchangeLines)
                        .ToList();
        if (kept.Count == 0) return;

        for (var i = 0; i < people.Count; i++)
        for (var j = i + 1; j < people.Count; j++)
            RecordOne(people[i], people[j], kept, tick);

        Evict();
    }

    static void RecordOne(Pawn a, Pawn b, List<string> lines, int tick)
    {
        if (!PairMath.IsPair(a.thingIDNumber, b.thingIDNumber)) return;

        var key = PairMath.Key(a.thingIDNumber, b.thingIDNumber);
        var rec = Pairs.GetOrAdd(key, k => new PairRecord(k, a.thingIDNumber, b.thingIDNumber,
                                                          a.LabelShort, b.LabelShort));
        lock (rec)
        {
            // A six-turn conversation arrives as two or three generations. Counting
            // each one is how a pair reaches "forty times" in an afternoon, which
            // would make the familiarity clause meaningless within a day.
            if (PairMath.WorthRecalling(rec.LastMetTick, tick) || rec.LastMetTick == 0)
                rec.TimesMet++;

            rec.LastMetTick = tick;
            rec.AName = a.LabelShort;
            rec.BName = b.LabelShort;
            rec.LastExchange = new List<string>(lines);
        }
    }

    /// <summary>
    /// What these two have between them, or null when there is nothing worth saying.
    ///
    /// The gate is deliberately early and cheap: most calls are two colonists who
    /// have never spoken, and building facts for them would spend a dictionary lookup
    /// and three list walks to produce nothing.
    /// </summary>
    public static PairFacts Facts(Pawn a, Pawn b, int now)
    {
        if (a == null || b == null) return null;
        if (!PairMath.IsPair(a.thingIDNumber, b.thingIDNumber)) return null;
        if (!Pairs.TryGetValue(PairMath.Key(a.thingIDNumber, b.thingIDNumber), out var rec)) return null;
        if (!PairMath.WorthRecalling(rec.LastMetTick, now)) return null;

        List<string> exchange;
        int lastMet, timesMet;
        lock (rec)
        {
            exchange = new List<string>(rec.LastExchange ?? new List<string>());
            lastMet = rec.LastMetTick;
            timesMet = rec.TimesMet;
        }

        return new PairFacts
        {
            // Named in the order they are standing in this scene, not the order they
            // were first stored in.
            AName = a.LabelShort,
            BName = b.LabelShort,
            TimesMet = timesMet,
            LastSpokeAgo = NarrativeMath.ElapsedFine(now - lastMet),
            LastExchange = exchange,
            Since = Chronicle.Since(lastMet, PairText.MaxSinceItems),
        };
    }

    static void Evict()
    {
        if (Pairs.Count <= MaxPairs) return;
        foreach (var key in Pairs.Values
                     .OrderBy(r => r.LastMetTick)
                     .Take(Pairs.Count - MaxPairs)
                     .Select(r => r.Key)
                     .ToList())
            Pairs.TryRemove(key, out _);
    }

    /// <summary>
    /// Shift every pair's last-met back, so the one-hour recall gate is already
    /// passed. Dev-mode only: the honest test for pair memory is "wait an in-game
    /// hour and arrange for the same two people to meet again", which is a
    /// twenty-minute round trip for a yes/no question.
    ///
    /// Collapses a WAIT rather than faking an outcome — the exchange it recalls is
    /// still a real one the pawns really had. Returns how many were moved.
    /// </summary>
    public static int Backdate(int ticks)
    {
        var n = 0;
        foreach (var rec in Pairs.Values)
            lock (rec)
            {
                if (rec.LastMetTick <= 0) continue;
                rec.LastMetTick -= ticks;
                n++;
            }
        return n;
    }

    public static void Clear() => Pairs.Clear();

    public static List<PairRecord> Snapshot() => Pairs.Values.ToList();

    public static void Restore(List<PairRecord> records)
    {
        Pairs.Clear();
        if (records == null) return;
        foreach (var r in records.Where(r => r != null))
        {
            r.LastExchange ??= new List<string>();
            Pairs[r.Key] = r;
        }
        Logger.Debug($"PairStore: restored {Pairs.Count} pair(s)");
    }
}
