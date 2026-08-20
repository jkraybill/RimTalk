using System;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using RimTalk.Source.Data;
using RimWorld;
using Verse;

namespace RimTalk.Service;

public class PawnSelector
{
    private const float HearingRange = 10f;
    private const float ViewingRange = 20f;

    public enum DetectionType
    {
        Hearing,
        Viewing,
    }

    private static List<Pawn> GetNearbyPawnsInternal(Pawn pawn1, Pawn pawn2 = null,
        DetectionType detectionType = DetectionType.Hearing, bool onlyTalkable = false, int maxResults = 10)
    {
        float baseRange = detectionType == DetectionType.Hearing ? HearingRange : ViewingRange;
        PawnCapacityDef capacityDef = detectionType == DetectionType.Hearing
            ? PawnCapacityDefOf.Hearing
            : PawnCapacityDefOf.Sight;

        return Cache.Keys
            .Where(p => p != pawn1 && p != pawn2)
            .Where(p => !onlyTalkable || Cache.Get(p).CanGenerateTalk())
            .Where(p => p.health.capacities.GetLevel(capacityDef) > 0.0)
            .Where(p =>
            {
                var room = p.GetRoom();
                var capacityLevel = p.health.capacities.GetLevel(capacityDef);
                var detectionDistance = baseRange * capacityLevel;

                bool nearPawn1 = room == pawn1.GetRoom() &&
                                 p.Position.InHorDistOf(pawn1.Position, detectionDistance);

                if (pawn2 == null) return nearPawn1;

                bool nearPawn2 = room == pawn2.GetRoom() &&
                                 p.Position.InHorDistOf(pawn2.Position, detectionDistance);

                return nearPawn1 || nearPawn2;
            })
            .OrderBy(p => pawn2 == null
                ? pawn1.Position.DistanceTo(p.Position)
                : Math.Min(pawn1.Position.DistanceTo(p.Position),
                    pawn2.Position.DistanceTo(p.Position)))
            .Take(maxResults)
            .ToList();
    }

    public static List<Pawn> GetNearByTalkablePawns(Pawn pawn1, Pawn pawn2 = null,
        DetectionType detectionType = DetectionType.Hearing)
    {
        return GetNearbyPawnsInternal(pawn1, pawn2, detectionType, onlyTalkable: true);
    }

    public static List<Pawn> GetAllNearByPawns(Pawn pawn1, Pawn pawn2 = null)
    {
        return GetNearbyPawnsInternal(pawn1, pawn2, DetectionType.Hearing, onlyTalkable: false);
    }

    /// <summary>
    /// Who speaks next.
    ///
    /// rim-universe #40. This used to rank user requests and nothing else, so a pawn
    /// who had just made a remark to somebody had exactly the same claim on the next
    /// generation as one asleep across the map — a weighted coin flip. Chitchat
    /// requests expire in 20 seconds, so most of them died unserved and the line that
    /// did appear was a pool event narrated by whoever the flip picked. That is why
    /// the generated line arrived before the interaction that caused it and was about
    /// neither.
    ///
    /// The ranking itself lives in <see cref="TalkPriority"/>, which is pure and
    /// tested; this reads the game and hands it the facts.
    /// </summary>
    public static Pawn SelectNextAvailablePawn()
    {
        var byId = new Dictionary<int, Pawn>();
        var candidates = new List<TalkCandidate>();
        var talkReadyPawns = new List<Pawn>();

        foreach (var pawn in Cache.Keys)
        {
            var pawnState = Cache.Get(pawn);
            if (pawnState == null) continue;

            var canTalk = pawnState.CanGenerateTalk();
            if (canTalk) talkReadyPawns.Add(pawn);

            byId[pawn.thingIDNumber] = pawn;
            candidates.Add(Describe(pawn, pawnState, canTalk));
        }

        var preferred = TalkPriority.Preferred(candidates);
        if (preferred != null && byId.TryGetValue(preferred.PawnId, out var chosen))
            return chosen;

        // Nobody has anything to answer. An ordinary, unprompted moment.
        return talkReadyPawns.Any() ? Cache.GetRandomWeightedPawn(talkReadyPawns) : null;
    }

    static TalkCandidate Describe(Pawn pawn, PawnState state, bool canTalk)
    {
        var c = new TalkCandidate
        {
            PawnId = pawn.thingIDNumber,
            CanTalk = canTalk,
            OldestUserTick = int.MaxValue,
            OldestUrgentTick = int.MaxValue,
            OldestPendingTick = int.MaxValue,
        };

        foreach (var req in state.TalkRequests)
        {
            // A request that has already lapsed is not something to answer; leaving it
            // in the ranking would pin the selector to a pawn whose reason to speak is
            // gone. GetNextTalkRequest sweeps them on read.
            if (req == null || req.IsExpired()) continue;

            if (req.TalkType.IsFromUser())
            {
                c.HasUserRequest = true;
                c.OldestUserTick = Math.Min(c.OldestUserTick, req.CreatedTick);
            }
            else if (req.TalkType == TalkType.Urgent)
            {
                c.HasUrgentRequest = true;
                c.OldestUrgentTick = Math.Min(c.OldestUrgentTick, req.CreatedTick);
            }
            else
            {
                c.HasPendingRequest = true;
                c.OldestPendingTick = Math.Min(c.OldestPendingTick, req.CreatedTick);
            }
        }
        return c;
    }
}