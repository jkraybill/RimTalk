using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// One thing the colony did, already worded.
///
/// Separate from <see cref="NarrativeEvent"/> on purpose. Deaths are rare, witnessed
/// and precious; chronicle entries are frequent and colony-wide. Sharing one bounded
/// list would let a good hunting week evict a decade of deaths, which is exactly
/// backwards — so they are two lists with two budgets and the readers merge them.
///
/// The clause is rendered AT HARVEST, while the game still has the objects. Storing
/// the ids and re-rendering later would make an entry's wording depend on whether a
/// mod is still loaded a hundred hours from now.
/// </summary>
public class ChronicleEntry : IExposable
{
    public int Tick;
    public string Kind;      // the TaleDef's defName; #22 will want to filter on it
    public string Key;       // TaleClause.DedupeKey — identity, not display
    public string Clause;    // "Kess hunted a boar" — no trailing stop

    public ChronicleEntry() { }

    public ChronicleEntry(int tick, string kind, string key, string clause)
    {
        Tick = tick;
        Kind = kind;
        Key = key;
        Clause = clause;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Key, "key");
        Scribe_Values.Look(ref Clause, "clause");
    }
}
