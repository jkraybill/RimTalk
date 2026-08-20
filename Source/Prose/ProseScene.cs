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
            Shape = frame?.Shape ?? SceneShape.Conversation,
            Preoccupation = frame?.Preoccupation,
            Situation = frame?.Situation,
            Concern = frame?.Concern,
            OtherName = frame?.OtherName,
            PlayerLine = frame?.PlayerLine,
            // #41. Everyone in the scene, so a two-hander cannot have one speaker
            // repeat themselves just because the other one was noisier.
            RecentLines = RecentLines.ForAll(pawns),
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
}
