using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// What one person wrote, in their own voice, the day they came to in this colony.
/// rim-universe #37.
///
/// Stores the name as well as the id, deliberately, and for the same reason
/// NarrativeEvent does: the entry is meant to outlive the person. A great-grandchild
/// reading what was written the day their great-grandmother woke up is the point,
/// and by then the Pawn is long gone.
/// </summary>
public class ArrivalEntry : IExposable
{
    public int PawnId;
    public string Name;
    public int Tick;
    public string Text;

    public void ExposeData()
    {
        Scribe_Values.Look(ref PawnId, "pawnId");
        Scribe_Values.Look(ref Name, "name");
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Values.Look(ref Text, "text");
    }
}
