using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// The colony's arrival log. rim-universe #37.
///
/// One entry per person, written once, never rewritten. "Never rewritten" is not
/// tidiness: everything downstream treats the entry as that pawn's canonical account
/// of waking up here, and a canon that changes is not one.
/// </summary>
public static class ArrivalLog
{
    /// <summary>Bounded, like the narrative events. A four-generation save is the goal.</summary>
    public const int MaxEntries = 300;

    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    public static List<ArrivalEntry> All => Comp?.ArrivalEntries ?? new List<ArrivalEntry>();

    public static bool Has(Pawn pawn) => For(pawn) != null;

    public static ArrivalEntry For(Pawn pawn)
    {
        if (pawn == null) return null;
        return Comp?.ArrivalEntries?.FirstOrDefault(e => e != null && e.PawnId == pawn.thingIDNumber);
    }

    /// <summary>Ignored if this pawn already has one. Written once, and once only.</summary>
    public static void Record(Pawn pawn, string text)
    {
        var comp = Comp;
        if (comp == null || pawn == null || string.IsNullOrWhiteSpace(text)) return;
        if (Has(pawn)) return;

        comp.ArrivalEntries.Add(new ArrivalEntry
        {
            PawnId = pawn.thingIDNumber,
            Name = pawn.LabelShort,
            Tick = GenTicks.TicksGame,
            Text = text,
        });

        // Oldest first, which is the wrong end to lose and the only end that scales.
        // #9's Diary is where an entry that matters should be moved before this reaches it.
        while (comp.ArrivalEntries.Count > MaxEntries) comp.ArrivalEntries.RemoveAt(0);
    }
}
