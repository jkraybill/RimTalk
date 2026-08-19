using System.Collections.Generic;
using RimWorld;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// One thing that happened, harvested from GAME STATE rather than from the dialogue
/// stream. rim-universe #21 / #38 / roundtable S166.
///
/// Deliberately stores NAMES and ids, never Pawn references. A dead pawn holding a
/// live reference in a decades-old save is a liability, and the whole point of this
/// record is that it outlives the people in it.
/// </summary>
public class NarrativeEvent : IExposable
{
    public int Tick;
    public string Kind;            // "death" for now; the vocabulary grows with consumers
    public string Subject;         // who it happened to
    public int SubjectId;
    public string Detail;          // one clause: how, or why it mattered
    public List<string> Witnesses = new();
    public List<int> WitnessIds = new();

    public NarrativeEvent() { }

    public NarrativeEvent(int tick, string kind, Pawn subject, string detail)
    {
        Tick = tick;
        Kind = kind;
        Subject = subject?.LabelShort ?? "someone";
        SubjectId = subject?.thingIDNumber ?? 0;
        Detail = detail;
    }

    public void AddWitness(Pawn p)
    {
        if (p == null) return;
        if (NarrativeMath.TryAddWitness(WitnessIds, p.thingIDNumber, SubjectId))
            Witnesses.Add(p.LabelShort);
    }

    public bool WasWitnessedBy(Pawn p) => p != null && WitnessIds.Contains(p.thingIDNumber);

    /// <summary>How this reads in a prompt. Kept short — it competes for context.</summary>
    public string AsRemembered(Pawn reader)
    {
        var when = NarrativeMath.Elapsed(GenTicks.TicksGame - Tick);
        var seen = WasWitnessedBy(reader) ? "saw it" : "heard about it";
        return string.IsNullOrWhiteSpace(Detail)
            ? $"{Subject} died {when}; {reader?.LabelShort} {seen}"
            : $"{Subject} died {when} ({Detail}); {reader?.LabelShort} {seen}";
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Subject, "subject");
        Scribe_Values.Look(ref SubjectId, "subjectId");
        Scribe_Values.Look(ref Detail, "detail");
        Scribe_Collections.Look(ref Witnesses, "witnesses", LookMode.Value);
        Scribe_Collections.Look(ref WitnessIds, "witnessIds", LookMode.Value);
        Witnesses ??= new List<string>();
        WitnessIds ??= new List<int>();
    }
}
