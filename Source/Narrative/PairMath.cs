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
}
