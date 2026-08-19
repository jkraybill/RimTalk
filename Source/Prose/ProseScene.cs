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
/// </summary>
public static class ProseScene
{
    public static string Build(TalkRequest req, List<Pawn> pawns, string intent, string preoccupation)
    {
        var pawn = pawns?.FirstOrDefault();
        if (pawn?.Map == null) return intent ?? "";

        var map = pawn.Map;
        var lines = new List<string> { Setting(pawn, map) };

        var others = Others(pawn, pawns);
        if (others != null) lines.Add(others);
        else if (map.mapPawns?.FreeColonistsSpawned?.Count(c => c != pawn && !c.Dead) == 0)
            lines.Add("There is nobody to hear it.");

        if (!string.IsNullOrWhiteSpace(preoccupation))
            lines.Add($"A moment ago {pawn.LabelShort} was on about {ProseWords.Mid(preoccupation.TrimEnd('.'))}, and is not finished with it.");

        if (!string.IsNullOrWhiteSpace(intent)) lines.Add(intent.Trim());

        return string.Join("\n\n", lines.Where(l => !string.IsNullOrWhiteSpace(l)));
    }

    static string Setting(Pawn pawn, Map map)
    {
        var data = CommonUtil.GetInGameData();
        var hour = CommonUtil.GetInGameHour(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(map.Tile));
        var temp = Mathf.RoundToInt(pawn.Position.GetTemperature(map));

        var bits = new List<string>();
        var weather = map.weatherManager?.curWeather?.label;
        if (!string.IsNullOrWhiteSpace(weather) && !weather.Equals("clear", System.StringComparison.OrdinalIgnoreCase))
            bits.Add(ProseWords.Mid(weather));
        bits.Add(ProseWords.Cold(temp));

        var room = pawn.GetRoom();
        bits.Add(room is { PsychologicallyOutdoors: false }
            ? ProseWords.Mid(room.Role?.label ?? "indoors")
            : "open ground");

        var doing = pawn.GetActivity();
        var act = string.IsNullOrWhiteSpace(doing)
            ? $"{pawn.LabelShort} is standing still"
            : $"{pawn.LabelShort} is {ProseWords.Mid(doing.StripTags())}";

        return $"{ProseWords.TimeOfDay(hour)}. {ProseWords.Join(bits).CapitalizeFirst()}. {act}.";
    }

    static string Others(Pawn pawn, List<Pawn> pawns)
    {
        var near = (pawns ?? new List<Pawn>())
            .Where(p => p != null && p != pawn && !p.Dead && !p.IsPlayer())
            .Select(p =>
            {
                var doing = p.GetActivity();
                var danger = p.IsInDanger(true) ? ", in trouble" : "";
                return string.IsNullOrWhiteSpace(doing)
                    ? $"{p.LabelShort} is here{danger}"
                    : $"{p.LabelShort} is {ProseWords.Mid(doing.StripTags())}{danger}";
            })
            .Take(3).ToList();
        return near.Count == 0 ? null : ProseWords.Join(near).CapitalizeFirst() + ".";
    }
}
