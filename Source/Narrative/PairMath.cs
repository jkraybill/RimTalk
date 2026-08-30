using System.Collections.Generic;
namespace RimTalk.Narrative;

/// <summary>
/// The pair half of rim-universe #30, decidable without a running game.
///
/// Conversation history has always been keyed on `pawn.thingIDNumber` — one flat
/// stream per person of everything they have said to anyone. Two colonists meeting
/// for the fortieth time have no idea they have ever met, because nothing in the
/// data model has a place to put "these two, together".
///
/// Source-linked into the test project like the rest of the pure layer.
/// </summary>
public static class PairMath
{
    /// <summary>
    /// A canonical key for an unordered pair, so (A,B) and (B,A) collide.
    ///
    /// The `(uint)` on the high half is belt and braces and NOT a bug fix. #30's
    /// sketch wrote `(long)Math.Min(a,b) << 32`, and the sign extension that looks
    /// like a collision hazard lands entirely in bits 63..32, which the shift has
    /// already vacated — the two forms are bit-identical for all 2^64 input pairs.
    /// Checked, because the first version of this comment asserted the opposite and
    /// a sabotage run of the negative-id test refused to fail.
    /// </summary>
    public static long Key(int a, int b)
    {
        var lo = a < b ? a : b;
        var hi = a < b ? b : a;
        return ((long)(uint)lo << 32) | (uint)hi;
    }

    /// <summary>A pawn talking to themselves is a monologue, and #27 owns that.</summary>
    public static bool IsPair(int a, int b) => a != b;

    public static bool Involves(long key, int id)
    {
        var lo = (int)(uint)(key >> 32);
        var hi = (int)(uint)(key & 0xFFFFFFFFL);
        return lo == id || hi == id;
    }

    /// <summary>
    /// One in-game hour. Below this the live conversation history (#9) is already
    /// carrying the same turns, so the pair block would put the exchange in the
    /// prompt twice and then instruct the model to call back to a conversation it is
    /// still in the middle of.
    /// </summary>
    public const int MinGapTicks = 2500;

    /// <summary>
    /// Whether a past meeting is far enough back to be remembered rather than
    /// continued. Guards three ways: never met, still talking, and a tick counter
    /// that has run backwards — which a rewound save or dev mode will do, and which
    /// as an unsigned subtraction would read as a gap of two billion ticks.
    /// </summary>
    public static bool WorthRecalling(int lastMetTick, int nowTick)
    {
        if (lastMetTick <= 0) return false;
        var gap = (long)nowTick - lastMetTick;
        return gap >= MinGapTicks;
    }

    /// <summary>
    /// Twenty. "Many times" has to mean many: a busy afternoon can put two colonists
    /// in the same room five times, and a pair block that announces deep familiarity
    /// after one day makes every colony read the same way.
    /// </summary>
    public const int ManyTimes = 20;

    /// <summary>
    /// The lines in a conversation that these two actually spoke.
    ///
    /// rim-universe S169. PairStore.Record correctly forms every unordered pair among
    /// the speakers, then handed the SAME full transcript to each of them — so a
    /// three-hander stored Syd's lines under "Scrooge and Kaito", and the prompt told
    /// the model those two last spoke and then showed it a third person talking. 10 of
    /// 14 pair blocks in JK's 2026-08-30 log were contaminated that way.
    ///
    /// Filtering happens BEFORE the cap, which is the other half of the bug: capping
    /// the whole conversation first could leave a pair with none of their own lines at
    /// all, or with fewer than they had.
    ///
    /// Lines arrive as "Name: text", which is how TalkService formats them.
    /// </summary>
    public static List<string> Between(IEnumerable<string> lines, string a, string b, int max)
    {
        var kept = new List<string>();
        if (lines == null) return kept;

        foreach (var line in lines)
            if (SpokenBy(line, a) || SpokenBy(line, b))
                kept.Add(line.Trim());

        return kept.Count <= max ? kept : kept.GetRange(kept.Count - max, max);
    }

    /// <summary>
    /// Whether a "Name: text" line was spoken by this person. Compares the whole
    /// speaker field rather than a prefix, so "Kai" does not claim "Kaito"'s lines.
    /// </summary>
    public static bool SpokenBy(string line, string name)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(name)) return false;

        var colon = line.IndexOf(':');
        return colon > 0
               && line.Substring(0, colon).Trim()
                      .Equals(name.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }
}
