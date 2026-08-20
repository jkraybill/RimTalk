using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using RimTalk.Prose;
using Verse;

namespace RimTalk.Narrative;

/// <summary>What one pawn has to bring up. rim-universe #44.</summary>
public class TopicEntry : IExposable
{
    public int PawnId;
    public List<string> Topics = new();

    /// <summary>Generated at this tick. Refreshed when it is old enough to have gone stale.</summary>
    public int Tick;

    /// <summary>
    /// Topics already raised, so a pawn does not bring the same grudge up forever —
    /// which is #41's problem wearing a different hat.
    /// </summary>
    public List<string> Used = new();

    public void ExposeData()
    {
        Scribe_Values.Look(ref PawnId, "pawnId");
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Collections.Look(ref Topics, "topics", LookMode.Value);
        Scribe_Collections.Look(ref Used, "used", LookMode.Value);
        Topics ??= new List<string>();
        Used ??= new List<string>();
    }
}

/// <summary>
/// The colony's back-pocket topics, one set per pawn. rim-universe #44.
/// </summary>
public static class TopicStore
{
    /// <summary>
    /// A year. JK asked for annual; the floor matters more than the ceiling, because
    /// a decade-old colonist still opening with the thing that happened in year one
    /// is the failure this is meant to fix rather than cause.
    /// </summary>
    public const int RefreshTicks = 3600000;   // GenDate.TicksPerYear

    public const int MaxEntries = 300;

    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    public static TopicEntry For(Pawn pawn) =>
        pawn == null ? null : Comp?.TopicEntries?.FirstOrDefault(e => e != null && e.PawnId == pawn.thingIDNumber);

    /// <summary>True when this pawn has usable topics that are not yet stale.</summary>
    public static bool Fresh(Pawn pawn)
    {
        var e = For(pawn);
        return e != null && e.Topics.Count > e.Used.Count
               && !TalkCacheMath.Expired(e.Tick, GenTicks.TicksGame, RefreshTicks);
    }

    /// <summary>
    /// One unused topic, or null. Marks it used, so the next draw is a different one
    /// and a pawn works through what they have rather than repeating the first.
    /// </summary>
    public static string Draw(Pawn pawn)
    {
        var e = For(pawn);
        if (e == null) return null;

        var unused = e.Topics.Where(t => !e.Used.Contains(t)).ToList();
        if (unused.Count == 0) return null;

        var topic = unused.RandomElement();   // Rand, not Unity's — RandomElement uses it
        e.Used.Add(topic);
        return topic;
    }

    public static void Record(Pawn pawn, List<string> topics)
    {
        var comp = Comp;
        if (comp == null || pawn == null || topics == null || topics.Count == 0) return;

        var existing = For(pawn);
        if (existing != null) comp.TopicEntries.Remove(existing);

        comp.TopicEntries.Add(new TopicEntry
        {
            PawnId = pawn.thingIDNumber,
            Topics = topics,
            Tick = GenTicks.TicksGame,
        });

        while (comp.TopicEntries.Count > MaxEntries) comp.TopicEntries.RemoveAt(0);
    }
}
