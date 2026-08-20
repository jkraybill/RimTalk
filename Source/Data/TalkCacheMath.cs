namespace RimTalk.Data;

/// <summary>
/// When a cached talk id stops being worth remembering. rim-universe #3.
///
/// Its own file, and pure, because the interesting cases are the ones a running game
/// makes hard to reach: a save loaded from an earlier point, where `now` is BEHIND
/// the stamp, and the tick counter at zero.
/// </summary>
public static class TalkCacheMath
{
    public static bool Expired(int stampTick, int nowTick, int rememberTicks)
    {
        if (rememberTicks <= 0) return true;

        // A stamp can legitimately sit in the FUTURE: loading an earlier save rewinds
        // TicksGame, and those entries are still live. There is deliberately no guard
        // for it — the subtraction goes negative and the comparison already answers
        // "not expired". A guard here was written, tested, and then deleted, because
        // sabotaging it failed no test: it was a branch nothing could reach, which is
        // the kind of code that reads as care and is really just surface.
        return nowTick - stampTick > rememberTicks;
    }
}
