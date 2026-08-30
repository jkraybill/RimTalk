using System.Collections.Generic;
using System.Linq;
using Verse;

namespace RimTalk.Narrative;

/// <summary>
/// Who was close enough to know something happened. rim-universe #22, the seed for
/// the Knowledge store.
///
/// RimWorld does not record perception, so this is a proximity rule and not a claim
/// about line of sight. That is the honest trade: a rule that is roughly right about
/// a settlement of eight, cheap enough to run inside a Tale postfix, and never wrong
/// in the direction that matters — a participant always knows what they did.
/// </summary>
public static class Witness
{
    /// <summary>
    /// Twenty-five cells. About a large room and its doorway.
    ///
    /// Deliberately generous rather than strict. Being told about something that
    /// happened across the yard is a small fidelity loss; being unable to mention the
    /// fistfight you were standing next to is the failure people notice, and #22
    /// exists because the mod already fails that way.
    /// </summary>
    public const int Radius = 25;

    /// <summary>
    /// The participants, plus every colonist close enough at the time.
    ///
    /// Ids, not Pawns, because the result is stored and outlives them. Participants
    /// go in even when they are off-map or downed: you know what happened to you.
    /// </summary>
    public static List<int> Around(IEnumerable<Pawn> participants, Pawn at)
    {
        var ids = new List<int>();

        foreach (var p in (participants ?? Enumerable.Empty<Pawn>()).Where(p => p != null))
            if (!ids.Contains(p.thingIDNumber))
                ids.Add(p.thingIDNumber);

        var map = at?.Map;
        if (map == null || !at.Spawned) return ids;

        var nearby = map.mapPawns?.FreeColonistsSpawned;
        if (nearby == null) return ids;

        foreach (var c in nearby)
        {
            if (c == null || c.Dead || !c.Spawned) continue;
            if (ids.Contains(c.thingIDNumber)) continue;
            if (!c.Position.InHorDistOf(at.Position, Radius)) continue;

            ids.Add(c.thingIDNumber);
        }

        return ids;
    }
}
