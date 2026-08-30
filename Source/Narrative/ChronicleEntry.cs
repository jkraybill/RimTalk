using System.Collections.Generic;
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
    public string Kind;      // the TaleDef's defName; #22 filters on it
    public string Key;       // TaleClause.DedupeKey — identity, not display
    public string Clause;    // "Kess hunted a boar" — no trailing stop

    /// <summary>
    /// Who the event is ABOUT, as ids. rim-universe #22.
    ///
    /// The clause already names them, but a name in a sentence cannot be compared
    /// against the people standing in a scene, and gossip is defined by exactly that
    /// comparison: an event is gossip when its subject is not in the room. Zero where
    /// the event has no person in it — a party, the founding of the colony.
    /// </summary>
    public int SubjectId;
    public int OtherId;

    /// <summary>
    /// Who knows this happened. Seeded at harvest with whoever was close enough to
    /// see it, and grows when somebody is told.
    ///
    /// This is the Knowledge domain #31 puts first in the build order, and it is the
    /// thing that makes gossip a mechanic rather than a flavour: a witness tells a
    /// non-witness, and afterwards the non-witness knows. Ids rather than names,
    /// because a name is not identity and this list outlives the colonists in it.
    /// </summary>
    public List<int> KnownBy = new();

    public ChronicleEntry() { }

    public ChronicleEntry(int tick, string kind, string key, string clause)
    {
        Tick = tick;
        Kind = kind;
        Key = key;
        Clause = clause;
    }

    public bool IsAbout(int pawnId) => pawnId != 0 && (SubjectId == pawnId || OtherId == pawnId);

    public bool Knows(int pawnId) => pawnId != 0 && KnownBy.Contains(pawnId);

    /// <summary>Returns whether this was news to them, so callers can log honestly.</summary>
    public bool Learn(int pawnId)
    {
        if (pawnId == 0 || KnownBy.Contains(pawnId)) return false;
        KnownBy.Add(pawnId);
        return true;
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Tick, "tick");
        Scribe_Values.Look(ref Kind, "kind");
        Scribe_Values.Look(ref Key, "key");
        Scribe_Values.Look(ref Clause, "clause");
        Scribe_Values.Look(ref SubjectId, "subjectId");
        Scribe_Values.Look(ref OtherId, "otherId");
        Scribe_Collections.Look(ref KnownBy, "knownBy", LookMode.Value);
        // Every entry saved before #22 loads with none of these, which is correct:
        // an old event nobody is recorded as knowing is simply never gossiped about.
        KnownBy ??= new List<int>();
    }
}
