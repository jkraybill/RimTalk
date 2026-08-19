using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// The colony's harvested history. rim-universe roundtable S166 disposition:
/// build ONE vertical slice — one event class, persisted, referenced in dialogue,
/// announced by a letter — and discover the schema from use rather than specifying
/// a general harvest platform against seven consumers that do not exist yet.
///
/// Death is the first slice: it is the event JK's remembered storylines turn on
/// most, and it exercises persistence, witnessing, recall and the letter channel
/// end to end.
/// </summary>
public static class NarrativeStore
{
    /// <summary>Bounded. A decades-long save must not grow without limit.</summary>
    public const int MaxEvents = 200;

    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    public static List<NarrativeEvent> All => Comp?.NarrativeEvents ?? new List<NarrativeEvent>();

    public static void Record(NarrativeEvent e)
    {
        var comp = Comp;
        if (comp == null || e == null) return;

        comp.NarrativeEvents.Add(e);
        NarrativeMath.Trim(comp.NarrativeEvents, MaxEvents);
    }

    /// <summary>
    /// What this pawn can bring to a conversation, newest first.
    ///
    /// Witnessed events rank above hearsay: a pawn who was there has a different
    /// relationship to the event than one who was told, and that difference is the
    /// entire reason witnesses are tracked.
    /// </summary>
    public static IEnumerable<NarrativeEvent> For(Pawn pawn, int max = 2)
    {
        if (pawn == null) yield break;
        var comp = Comp;
        if (comp == null) yield break;

        var ordered = NarrativeMath.Rank(comp.NarrativeEvents, e => e.WasWitnessedBy(pawn), e => e.Tick, max);
        foreach (var e in ordered) yield return e;
    }
}
