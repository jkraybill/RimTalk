using Verse;

namespace RimTalk.Data;

/// <summary>
/// One turn of one pawn's conversation history, flattened so it can be scribed.
///
/// rim-universe #9. MessageHistory was a static ConcurrentDictionary that nothing
/// serialised, and TalkHistory.Clear() runs on every game load — so every
/// conversation a colony had ever had was erased on reload. Hediff_Persona was the
/// only per-pawn state that survived, which is why nothing could be built on top.
///
/// A flat list rather than a nested dictionary because Scribe has no LookMode for
/// Dictionary&lt;int, List&lt;T&gt;&gt;, and because it is the same shape as
/// NarrativeEvents, which is already proven through a save cycle in this component.
/// </summary>
public class ChatTurn : IExposable
{
    public int PawnId;
    public Role Role;
    public string Text;

    public void ExposeData()
    {
        Scribe_Values.Look(ref PawnId, "pawnId");
        Scribe_Values.Look(ref Role, "role");
        Scribe_Values.Look(ref Text, "text");
    }
}
