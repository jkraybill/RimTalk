using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Narrative;

/// <summary>
/// One candidate piece of gossip, flattened out of a ChronicleEntry so the selection
/// below can be tested without a game. rim-universe #22.
/// </summary>
public class GossipItem
{
    public int Tick;
    public string Kind;          // the TaleDef defName, for GossipOpinion.Valence
    public string Clause;
    public int SubjectId;
    public int OtherId;

    /// <summary>Who already knows. The speaker must be in here; that is the point.</summary>
    public List<int> KnownBy = new();

    public bool IsAbout(int id) => id != 0 && (SubjectId == id || OtherId == id);
    public bool Knows(int id) => id != 0 && KnownBy.Contains(id);
}

/// <summary>
/// Which of the things that have happened are worth these two talking about.
/// rim-universe #22.
///
/// JK's example is the whole specification:
///
///   "OMG did you see Adrian get massively rejected by Starling today?"
///
/// Neither speaker is in the event. Both absent parties are named. The line only
/// exists because the speaker knows something, and it is only interesting because
/// the people it is about are not standing there.
///
/// Pure, and keyed on ids rather than Pawns, because the three rules below are the
/// entire feature and every one of them is a judgement that deserves a test.
/// </summary>
public static class GossipMath
{
    /// <summary>
    /// Two. The block competes with the pair memory and the chronicle for the same
    /// context, and three pieces of gossip in one exchange is a briefing rather than
    /// a conversation.
    /// </summary>
    public const int MaxItems = 2;

    /// <summary>
    /// Four days.
    ///
    /// Was a day and a half, on the reasoning that stale gossip is not gossip, it is
    /// history, and history already has a block. The principle was right and the
    /// number was set against an imagined event rate rather than a measured one.
    ///
    /// Measured, from JK's ten-day colony: nine chronicle entries in 600,000 ticks,
    /// eight of them about a person. That is roughly one event a day, so a
    /// day-and-a-half window holds about one item — and gossip then needs the right
    /// two people to meet while that single item is still hot. The snapshot showed a
    /// pool of 1 and zero opinion shifts, with every other part of the chain working.
    ///
    /// Four days holds three or four items, which is a conversation's worth. It is
    /// also now LONGER than Chronicle.LatelyTicks rather than shorter, and that is the
    /// right way round: a fight between two people is remembered a good while after
    /// "there was a party" has stopped being worth mentioning.
    /// </summary>
    public const int FreshTicks = 240000;

    /// <summary>
    /// What <paramref name="speaker"/> could tell <paramref name="listener"/>,
    /// newest first.
    ///
    /// Three rules, and each one drops a category the others would let through:
    ///
    ///  1. The speaker must know it. Gossip a pawn has not heard is not available to
    ///     them, and this is the only thing the Knowledge store is for.
    ///  2. It must be ABOUT somebody, and that somebody must not be in the room.
    ///     "Did you see Adrian get rejected" said to Adrian is not gossip, it is a
    ///     provocation, and the failure reads as the mod not understanding the scene.
    ///  3. It must be fresh. See FreshTicks.
    ///
    /// Deliberately NOT filtered on whether the listener already knows. Two people
    /// rehashing the same rejection is exactly what colonists do, and requiring
    /// novelty would silence the second, third and fourth conversation about the
    /// most interesting thing that happened all quadrum.
    /// </summary>
    public static List<GossipItem> For(IEnumerable<GossipItem> pool, int speaker, IReadOnlyCollection<int> present, int now)
    {
        var result = new List<GossipItem>();
        if (pool == null || speaker == 0) return result;

        foreach (var item in pool.Where(i => i != null)
                                 .OrderByDescending(i => i.Tick))
        {
            if (!item.Knows(speaker)) continue;
            if (item.SubjectId == 0 && item.OtherId == 0) continue;
            if (now - item.Tick > FreshTicks) continue;
            if (present != null && present.Any(item.IsAbout)) continue;

            result.Add(item);
            if (result.Count >= MaxItems) break;
        }

        return result;
    }

    /// <summary>
    /// Who learns something by being told. The knowledge write, and the reason #31
    /// puts this domain first: after this call the listener knows, can bring it up
    /// themselves, and can pass it on to a third person.
    ///
    /// Returns the items that were actually news, so a caller can report the transfer
    /// rather than assert it — a store that silently already contained everything
    /// looks identical to one that is working.
    /// </summary>
    public static List<GossipItem> Tell(IEnumerable<GossipItem> told, int listener)
    {
        var news = new List<GossipItem>();
        if (told == null || listener == 0) return news;

        foreach (var item in told.Where(i => i != null))
        {
            if (item.Knows(listener)) continue;
            item.KnownBy.Add(listener);
            news.Add(item);
        }

        return news;
    }
}
