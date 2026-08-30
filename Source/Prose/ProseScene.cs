using System.Collections.Generic;
using System.Linq;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Prose;

/// <summary>
/// The moment as a scene rather than a telemetry readout.
///
/// The envelope used to be "Time: 6am / Season: Spring / Weather: Light rain /
/// Location: Outdoors;8C" — nine labelled rows a model reads as a status page and
/// answers with a weather report. As sentences it is a place someone is standing in.
///
/// This file reads the game and nothing else; the sentences are assembled in
/// <see cref="ProseSceneText"/>, which the test suite executes.
/// </summary>
public static class ProseScene
{
    public static string Build(TalkRequest req, List<Pawn> pawns, ContextBuilder.DialogueFrame frame)
    {
        var facts = Gather(pawns, frame);
        // Falling back to the telegraphic intent is better than falling back to
        // nothing: without a map there is no scene to describe, but there is still
        // something to ask for.
        return facts == null ? (frame?.Intent ?? "") : ProseSceneText.Compose(facts);
    }

    /// <summary>Reads the game. Null when there is no map to stand on.</summary>
    public static SceneFacts Gather(List<Pawn> pawns, ContextBuilder.DialogueFrame frame)
    {
        var pawn = pawns?.FirstOrDefault();
        if (pawn?.Map == null) return null;

        var map = pawn.Map;
        var room = pawn.GetRoom();
        var doing = pawn.GetActivity();

        return new SceneFacts
        {
            Hour24 = CommonUtil.GetInGameHour(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile)),
            TempCelsius = Mathf.RoundToInt(pawn.Position.GetTemperature(map)),
            Weather = map.weatherManager?.curWeather?.label,
            RoomLabel = room?.Role?.label,
            Indoors = room is { PsychologicallyOutdoors: false },
            PawnName = pawn.LabelShort,
            PawnActivity = string.IsNullOrWhiteSpace(doing) ? null : doing.StripTags(),
            OtherColonistsOnMap = map.mapPawns?.FreeColonistsSpawned?.Count(c => c != pawn && !c.Dead) ?? 0,
            Colony = GatherColony(map),
            Shape = frame?.Shape ?? SceneShape.Conversation,
            Topic = frame?.Topic,
            Preoccupation = frame?.Preoccupation,
            Situation = frame?.Situation,
            Concern = frame?.Concern,
            OtherName = frame?.OtherName,
            PlayerLine = frame?.PlayerLine,
            // #41. Everyone in the scene, so a two-hander cannot have one speaker
            // repeat themselves just because the other one was noisier.
            RecentLines = RecentLines.ForAll(pawns),
            // #30. Two people only. A pair callback in a three-hander is a callback
            // the third person was not there for, and the model has no way to know
            // that from a block that only names two of the three.
            Pair = PairFor(pawns),
            // The harvest is otherwise invisible to a solo colony and to two people
            // meeting for the first time — which is most scenes, early. ProseSceneText
            // drops this when the pair block is present.
            Lately = Narrative.Chronicle.Lately(GenTicks.TicksGame, ProseSceneText.MaxLatelyItems),
            // #28. The speaking pawn first, so a two-hander shows theirs even when the
            // other person is the one with the louder ambition.
            Wants = Wants(pawns),
            // #22. What the speaker could raise about people who are not here.
            Gossip = GossipFor(pawns),
            Others = (pawns ?? new List<Pawn>())
                .Where(p => p != null && p != pawn && !p.Dead && !p.IsPlayer())
                .Select(p =>
                {
                    var act = p.GetActivity();
                    return new PersonNote
                    {
                        Name = p.LabelShort,
                        Activity = string.IsNullOrWhiteSpace(act) ? null : act.StripTags(),
                        InDanger = p.IsInDanger(true),
                    };
                })
                .ToList(),
        };
    }

    /// <summary>
    /// The pair memory for a two-person scene. rim-universe #30.
    ///
    /// The player counts as nobody: a pawn answering the player is not having a
    /// conversation with a colonist, and PairStore only ever holds colonist pairs.
    /// </summary>
    static PairFacts PairFor(List<Pawn> pawns)
    {
        var people = (pawns ?? new List<Pawn>())
            .Where(p => p != null && !p.Dead && !p.IsPlayer())
            .Distinct()
            .ToList();
        return people.Count == 2
            ? Narrative.PairStore.Facts(people[0], people[1], GenTicks.TicksGame)
            : null;
    }

    /// <summary>
    /// What the people here are trying to see happen. rim-universe #28.
    ///
    /// Ordered by the scene's own order, not by anything about the goals: the speaking
    /// pawn is pawns[0] and their goal is the one the instruction is about to ask them
    /// to talk from.
    /// </summary>
    static List<string> Wants(List<Pawn> pawns) =>
        !Settings.Get().Context.Goals
        ? new List<string>()
        : (pawns ?? new List<Pawn>())
            .Where(p => p != null && !p.Dead && !p.IsPlayer())
            .Select(p => Goals.GoalText.Block(p.LabelShort, Goals.GoalStore.Active(p)?.Statement))
            .Where(s => s != null)
            .Take(ProseSceneText.MaxWants)
            .ToList();

    /// <summary>
    /// The colony as facts. rim-universe #23.
    ///
    /// Every read here is defensive: a map mid-generation, a scenario with no resource
    /// counter, a modded biome with no label. A null field renders as no sentence,
    /// which is the right failure — a missing clause beats an invented one.
    /// </summary>
    public static ColonyFacts GatherColony(Map map)
    {
        if (map == null) return null;

        var colonists = map.mapPawns?.FreeColonistsSpawned?.Count(c => !c.Dead) ?? 0;

        return new ColonyFacts
        {
            SettlementName = map.Parent?.LabelCap,
            BiomeLabel = map.Biome?.label,
            DaysOld = GenDate.DaysPassed,
            FoodDays = FoodDays(map, colonists),
            MedicineCount = MedicineCount(map),
            HasPower = HasPower(map),
            // #28's predicates. Same gatherer as everything else on this object —
            // a second reader of the same map is the second code path that gets one
            // entry point maintained and the other one forgotten.
            Colonists = colonists,
            ColonistsWithoutBed = WithoutBed(map),
            Turrets = Turrets(map),
        };
    }

    /// <summary>
    /// Days of food at the current population. A colonist eats about 1.6 nutrition a
    /// day, which is the figure RimWorld's own food-supply readout uses.
    /// </summary>
    static float FoodDays(Map map, int colonists)
    {
        var nutrition = map.resourceCounter?.TotalHumanEdibleNutrition ?? -1f;
        if (nutrition < 0f) return -1f;
        if (colonists <= 0) return -1f;      // nobody to feed; the number means nothing
        return nutrition / (colonists * 1.6f);
    }

    /// <summary>
    /// Colonists with no bed they own. #28's Shelter predicate, and the one JK would
    /// notice first — a pawn sleeping on the floor is visible in a way a food number
    /// is not.
    /// </summary>
    static int WithoutBed(Map map)
    {
        var people = map.mapPawns?.FreeColonistsSpawned;
        if (people == null) return 0;
        return people.Count(p => !p.Dead && p.ownership?.OwnedBed == null);
    }

    /// <summary>
    /// Turrets and traps. Counted rather than valued: #28's BaseDefence is "this place
    /// could not hold off much", which is about there being emplacements at all.
    /// -1 when the map cannot be read, which reads as no goal rather than as none.
    /// </summary>
    static int Turrets(Map map)
    {
        var things = map.listerThings;
        if (things == null) return -1;
        return things.ThingsInGroup(ThingRequestGroup.BuildingArtificial)
                     .Count(t => t is Building_Turret || (t?.def?.building?.isTrap ?? false));
    }

    static int MedicineCount(Map map)
    {
        var counter = map.resourceCounter;
        if (counter == null) return -1;

        var total = 0;
        foreach (var def in new[] { ThingDefOf.MedicineHerbal, ThingDefOf.MedicineIndustrial,
                                    ThingDefOf.MedicineUltratech })
            if (def != null) total += counter.GetCount(def);
        return total;
    }

    /// <summary>
    /// Null rather than false when the map has no power net at all, so a scenario that
    /// never had electricity is not told it has none.
    /// </summary>
    static bool? HasPower(Map map)
    {
        var nets = map.powerNetManager?.AllNetsListForReading;
        if (nets == null || nets.Count == 0) return null;
        return nets.Any(n => n.CurrentEnergyGainRate() > 0f || n.CurrentStoredEnergy() > 0f);
    }

    /// <summary>
    /// Talk about absent people. rim-universe #22.
    ///
    /// The speaker is pawns[0] — the block is what THEY could bring up, not what is
    /// merely true, and that distinction is the Knowledge store's entire job.
    ///
    /// Telling happens here rather than after the reply, and that is deliberate: the
    /// block is in the prompt by the time this returns, so everyone in the room has
    /// been told whether or not the model chooses to use it. Waiting for the model to
    /// prove it said something would mean parsing dialogue for comprehension, which is
    /// the fragile half of every feature that has gone wrong in this repo.
    /// </summary>
    static string GossipFor(List<Pawn> pawns)
    {
        var people = (pawns ?? new List<Pawn>())
            .Where(p => p != null && !p.Dead && !p.IsPlayer())
            .Distinct()
            .ToList();

        var speaker = people.FirstOrDefault();
        if (speaker == null || people.Count < 2) return null;

        var now = GenTicks.TicksGame;
        var present = people.Select(p => p.thingIDNumber).ToList();

        var pool = Narrative.Chronicle.GossipPool(now);
        var picked = Narrative.GossipMath.For(pool.Select(x => x.Item), speaker.thingIDNumber, present, now);
        if (picked.Count == 0) return null;

        // The knowledge write. Everybody else in the room hears it.
        foreach (var listener in people.Skip(1))
            Narrative.GossipMath.Tell(picked, listener.thingIDNumber);

        var absent = picked
            .SelectMany(i => new[] { i.SubjectId, i.OtherId })
            .Where(id => id != 0 && !present.Contains(id))
            .Distinct()
            .Select(FindColonist)
            .Where(p => p != null)
            .Select(p => GossipText.Describe(p.LabelShort, WhatTheyAre(p)))
            .Where(s => s != null)
            .ToList();

        return GossipText.Compose(picked.Select(i => i.Clause), absent);
    }

    /// <summary>A colonist by id, or null once they have left or died.</summary>
    static Pawn FindColonist(int id) =>
        Find.Maps?.SelectMany(m => m.mapPawns?.FreeColonistsSpawned ?? new List<Pawn>())
            .FirstOrDefault(p => p != null && p.thingIDNumber == id);

    /// <summary>
    /// The one thing about an absent person worth a stranger's sentence: what they
    /// are best at. Null below a threshold, because "Adrian is a miner" is only worth
    /// saying when Adrian actually is one.
    /// </summary>
    const int NotableSkill = 8;

    static string WhatTheyAre(Pawn p)
    {
        var best = p?.skills?.skills?
            .Where(s => s != null && !s.TotallyDisabled)
            .OrderByDescending(s => s.Level)
            .FirstOrDefault();

        return best == null || best.Level < NotableSkill
            ? null
            : GossipText.SkillNoun(best.def?.defName);
    }
}
