using System;

namespace RimTalk.Service;

/// <summary>
/// Whether the single global in-flight slot should be considered stuck.
///
/// rim-universe #17. RimTalk allows one API call at a time, gated on a plain
/// non-volatile `static bool _busy`. It is set true on the main thread and set false
/// in an async `finally`, which runs on a threadpool thread — so the main thread,
/// reading it in a tight tick loop, can go on seeing a cached `true` indefinitely.
/// When that happens every colonist in the colony stops speaking, permanently, with
/// no error anywhere.
///
/// Observed: JK's session made exactly one successful call and then went silent for
/// the rest of the session. One request, one response, no exceptions, nothing in the
/// log at all — which is the signature.
///
/// `volatile` fixes the visibility. This is the belt to that braces: no request can
/// legitimately outlive the client's own 60-second connect and read timeouts by this
/// much, so if the flag has been set that long, it is stuck rather than busy.
///
/// Wall clock and not game ticks: a request keeps running while the game is paused.
/// </summary>
public static class BusyGate
{
    /// <summary>
    /// Generous on purpose. The client allows 60s to connect and 60s of read
    /// inactivity, and the error handler retries, so a slow reasoning model on a bad
    /// connection can legitimately take minutes. Five is past any of that and well
    /// short of a play session.
    /// </summary>
    public const int StuckAfterSeconds = 300;

    /// <summary>
    /// True when the flag should be forced back down. False when it is not set at all,
    /// or is set and still plausibly in flight.
    /// </summary>
    public static bool IsStuck(bool busy, DateTime? busySince, DateTime now,
                               int stuckAfterSeconds = StuckAfterSeconds)
    {
        if (!busy) return false;

        // No stamp means nobody recorded the start. Treat that as not stuck rather than
        // as stuck: clearing a flag we know nothing about could cut a live request off
        // mid-stream, and the visibility fix is the actual repair.
        if (busySince == null) return false;

        return (now - busySince.Value).TotalSeconds >= stuckAfterSeconds;
    }
}
