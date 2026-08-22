using System.Collections.Generic;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// What two people have between them. rim-universe #30.
///
/// Keeps NAMES as well as ids, for the same reason <see cref="ArrivalEntry"/> does:
/// the record is meant to outlive at least one of the people in it, and a dead
/// pawn's id resolves to nothing.
///
/// Only the LAST exchange is kept. Keeping a rolling window was the first design and
/// it is speculative state: the render uses one exchange, #22's gossip will want a
/// different shape entirely, and an unbounded-per-pair list in a save is exactly the
/// kind of growth #3 and #4 were about.
/// </summary>
public class PairRecord : IExposable
{
    public long Key;
    public int AId, BId;
    public string AName, BName;

    /// <summary>Tick of the most recent conversation. 0 means never.</summary>
    public int LastMetTick;

    /// <summary>Separate occasions, not turns. #30's forty.</summary>
    public int TimesMet;

    /// <summary>Their last exchange, oldest line first, already "Name: line".</summary>
    public List<string> LastExchange = new();

    public PairRecord() { }

    public PairRecord(long key, int aId, int bId, string aName, string bName)
    {
        Key = key;
        AId = aId;
        BId = bId;
        AName = aName;
        BName = bName;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Key, "key");
        Scribe_Values.Look(ref AId, "aId");
        Scribe_Values.Look(ref BId, "bId");
        Scribe_Values.Look(ref AName, "aName");
        Scribe_Values.Look(ref BName, "bName");
        Scribe_Values.Look(ref LastMetTick, "lastMetTick");
        Scribe_Values.Look(ref TimesMet, "timesMet");
        Scribe_Collections.Look(ref LastExchange, "lastExchange", LookMode.Value);
        LastExchange ??= new List<string>();
    }
}
