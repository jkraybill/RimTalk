using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Service;

/// <summary>Where the next line should come from, in the order it should be tried.</summary>
public enum TalkSource
{
    /// <summary>Nothing to say and nothing to say it about.</summary>
    None,
    /// <summary>This pawn's own queue — something that happened to them.</summary>
    Own,
    /// <summary>The map-wide pool — something that happened near them.</summary>
    Pool,
    /// <summary>Neither; talk about what they are doing.</summary>
    Fallback,
}

/// <summary>One pawn, reduced to what the scheduler needs to rank them.</summary>
public class TalkCandidate
{
    public int PawnId;
    public bool CanTalk;

    public bool HasUserRequest;
    public int OldestUserTick;

    /// <summary>A request that stops mattering almost immediately — combat, mostly.</summary>
    public bool HasUrgentRequest;
    public int OldestUrgentTick;

    /// <summary>Anything else queued against this pawn: chitchat, a thought, a hediff, a level-up.</summary>
    public bool HasPendingRequest;
    public int OldestPendingTick;
}

/// <summary>
/// Who speaks next, and about what. rim-universe #40.
///
/// The bug JK saw in the social log was a generated line arriving *before* the
/// vanilla interaction that should have caused it, and about neither of the two
/// interactions around it. The cause is here rather than in the prompt:
///
///   1. The selector ranked only user requests. Everyone else was a weighted coin
///      flip, so the pawn who had just done something had no more claim on the next
///      generation than a pawn asleep across the map.
///   2. Whoever was picked then tried the map-wide pool FIRST, so even the right
///      pawn narrated somebody else's event instead of their own.
///
/// Together those meant a chitchat request — which expires in 20 seconds — usually
/// died unserved, while the line that did appear came from a pool event with its
/// Initiator overwritten to whoever the coin flip chose. Two streams sharing one log
/// and nothing synchronising them.
///
/// Pure on purpose: this file is source-linked into the test project and run, because
/// "the pawn who just spoke to someone answers next" is a claim that should be
/// checkable without launching RimWorld and waiting for a courtship.
/// </summary>
public static class TalkPriority
{
    /// <summary>
    /// The pawn who has the strongest claim on the next generation, or null when
    /// nobody does and the caller should fall back to its weighted random pick.
    ///
    /// User beats urgent beats anything else pending; within a class, the oldest
    /// request wins so answers arrive in the order their causes happened.
    ///
    /// A user request is honoured even from a pawn who cannot currently talk — that
    /// is existing behaviour and it is right, because the player asked. Everything
    /// else requires <see cref="TalkCandidate.CanTalk"/>.
    ///
    /// This cannot starve idle chatter: a pending request that is not served expires
    /// within seconds, and then its pawn is an ordinary candidate again.
    /// </summary>
    public static TalkCandidate Preferred(IEnumerable<TalkCandidate> candidates)
    {
        var all = (candidates ?? Enumerable.Empty<TalkCandidate>()).Where(c => c != null).ToList();

        return Oldest(all.Where(c => c.HasUserRequest), c => c.OldestUserTick)
            ?? Oldest(all.Where(c => c.CanTalk && c.HasUrgentRequest), c => c.OldestUrgentTick)
            ?? Oldest(all.Where(c => c.CanTalk && c.HasPendingRequest), c => c.OldestPendingTick);
    }

    static TalkCandidate Oldest(IEnumerable<TalkCandidate> pool, System.Func<TalkCandidate, int> tick)
    {
        TalkCandidate best = null;
        foreach (var c in pool)
            // Ties broken by PawnId so the choice does not depend on dictionary order.
            if (best == null || tick(c) < tick(best) ||
                (tick(c) == tick(best) && c.PawnId < best.PawnId))
                best = c;
        return best;
    }

    /// <summary>
    /// What the selected pawn should talk about, in order of preference.
    ///
    /// Own queue first. That queue holds what happened *to this pawn* — the remark
    /// they just made, the thought they just had. The pool holds map-wide events
    /// whose Initiator is overwritten with whoever was selected, so a pool line is
    /// something this pawn merely witnessed. Answering a witnessed event before your
    /// own is what put the courtship after the sick knot.
    /// </summary>
    public static TalkSource Pick(bool hasOwnRequest, bool poolEligible, bool poolHasRequest,
                                  bool fallbackAvailable)
    {
        if (hasOwnRequest) return TalkSource.Own;
        if (poolEligible && poolHasRequest) return TalkSource.Pool;
        return fallbackAvailable ? TalkSource.Fallback : TalkSource.None;
    }
}
