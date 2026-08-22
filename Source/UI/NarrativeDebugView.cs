using System.Collections.Generic;
using System.Linq;
using RimTalk.Goals;
using RimTalk.Narrative;
using RimTalk.Prose;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.UI;

/// <summary>
/// What the narrative layers actually hold, on screen.
///
/// Every feature added in S168 shares one gap: the stores are written from game
/// state, so no test can reach them, and the only way to know whether any of it
/// fired was to search assembled prompts and hope. Three sessions running, this
/// project has found its worst bugs by reading a prompt out of Player.log — a
/// feature invisible behind a saved preset, a colony silenced by a stale bool — and
/// each time the instrument was a text search over a log file.
///
/// This is the instrument instead. It shows no prompt and changes no prompt, so it
/// cannot muddy a playtest; it just answers "did that happen".
///
/// The dev buttons exist because the honest playtest for pair memory is "wait an
/// in-game hour, then arrange for the same two people to meet again", which is a
/// twenty-minute round trip for a yes/no question.
/// </summary>
public static class NarrativeDebugView
{
    const float Row = 22f;
    const float Pad = 6f;

    public static void Draw(Rect rect, ref Vector2 scroll)
    {
        var buttons = new Rect(rect.x, rect.y, rect.width, Prefs.DevMode ? 28f : 0f);
        if (Prefs.DevMode) DrawDevButtons(buttons);

        var body = new Rect(rect.x, rect.y + buttons.height + Pad,
                            rect.width, rect.height - buttons.height - Pad);

        var lines = Lines();
        var view = new Rect(0f, 0f, body.width - 20f, lines.Count * Row + Pad);

        Widgets.BeginScrollView(body, ref scroll, view);
        var y = 0f;
        foreach (var (text, colour) in lines)
        {
            var r = new Rect(4f, y, view.width - 8f, Row);
            var old = GUI.color;
            GUI.color = colour;
            Widgets.Label(r, text);
            GUI.color = old;
            y += Row;
        }
        Widgets.EndScrollView();
    }

    static readonly Color Head = new(0.7f, 0.85f, 1f);
    static readonly Color Dim = new(0.6f, 0.6f, 0.6f);

    static List<(string, Color)> Lines()
    {
        var now = GenTicks.TicksGame;
        var lines = new List<(string, Color)>();
        void Add(string s) => lines.Add((s, Color.white));
        void Header(string s) => lines.Add((s, Head));
        void Faint(string s) => lines.Add((s, Dim));

        // ---- goals
        var goals = Find.World?.GetComponent<Data.RimTalkWorldComponent>()?.GoalEntries
                    ?? new List<GoalEntry>();
        var facts = ProseScene.GatherColony(Find.CurrentMap);

        Header($"GOALS  ({goals.Count(g => g?.State == GoalState.Active)} active, {goals.Count} total)");
        if (goals.Count == 0)
            Faint("  none yet — a goal is only offered when something is actually wrong here");
        foreach (var g in goals.OrderByDescending(g => g.SetTick).Take(12))
        {
            var progress = g.State == GoalState.Active
                ? $"now {Current(g.Kind, facts)} / need {g.Target:0.#}"
                : NarrativeMath.Elapsed(now - g.ResolvedTick);
            Add($"  {g.PawnName,-12} {g.Kind,-14} {g.State,-9} {progress,-24} \"{g.Statement}\"");
        }

        // What a goal WOULD be offered for right now. The most useful line here when
        // the list above is empty, because it separates "not generating" from
        // "nothing to want".
        var candidates = GoalMath.Candidates(facts);
        Faint(candidates.Count == 0
            ? "  candidates right now: none — this colony has nothing wrong with it"
            : "  candidates right now: " + string.Join(", ", candidates));
        Add("");

        // ---- chronicle
        var comp = Find.World?.GetComponent<Data.RimTalkWorldComponent>();
        var chron = comp?.ChronicleEntries ?? new List<ChronicleEntry>();
        Header($"CHRONICLE  ({chron.Count} entries, {Chronicle.Lately(now, 99).Count} in the last two days)");
        if (chron.Count == 0)
            Faint("  none yet — needs a hunt, a finished building, a wedding, a party...");
        foreach (var e in chron.AsEnumerable().Reverse().Take(12))
            Add($"  {NarrativeMath.ElapsedFine(now - e.Tick),-16} {e.Clause}");
        Add("");

        // ---- pairs
        var pairs = PairStore.Snapshot();
        var ready = pairs.Count(p => PairMath.WorthRecalling(p.LastMetTick, now));
        Header($"PAIR MEMORY  ({pairs.Count} pairs, {ready} past the one-hour gate)");
        if (pairs.Count == 0)
            Faint("  none yet — two colonists have to hold a conversation first");
        foreach (var p in pairs.OrderByDescending(p => p.LastMetTick).Take(12))
        {
            var gate = PairMath.WorthRecalling(p.LastMetTick, now) ? "" : "  (too recent to recall)";
            var last = p.LastExchange?.LastOrDefault() ?? "";
            lines.Add(($"  {p.AName} + {p.BName,-18} met {p.TimesMet,-3} last {NarrativeMath.ElapsedFine(now - p.LastMetTick),-16} {last}{gate}",
                       gate.Length == 0 ? Color.white : Dim));
        }
        Add("");

        // ---- deaths, which live in the other store on purpose
        Header($"DEATHS  ({NarrativeStore.All.Count})");
        foreach (var e in NarrativeStore.All.AsEnumerable().Reverse().Take(5))
            Add($"  {NarrativeMath.Elapsed(now - e.Tick),-16} {e.Subject}  ({e.Witnesses.Count} witness)");

        return lines;
    }

    /// <summary>Where the colony stands on this goal's own axis, in the target's units.</summary>
    static string Current(GoalKind kind, ColonyFacts c)
    {
        if (c == null) return "?";
        return kind switch
        {
            GoalKind.FoodSecurity  => c.FoodDays < 0f ? "?" : $"{c.FoodDays:0.#}",
            GoalKind.Medicine      => c.MedicineCount < 0 ? "?" : c.MedicineCount.ToString(),
            GoalKind.Shelter       => $"{c.ColonistsWithoutBed} without",
            GoalKind.Power         => c.HasPower switch { true => "on", false => "off", _ => "?" },
            GoalKind.BaseDefence   => c.Turrets < 0 ? "?" : c.Turrets.ToString(),
            GoalKind.Companionship => c.Colonists.ToString(),
            _ => "?",
        };
    }

    /// <summary>
    /// Dev mode only, and each one collapses a wait rather than faking an outcome.
    /// Nothing here writes a fake goal or a fake conversation: the point of a
    /// playtest is to find out whether the real path fires.
    /// </summary>
    static void DrawDevButtons(Rect rect)
    {
        var w = (rect.width - 3 * Pad) / 4f;
        var x = rect.x;

        if (Widgets.ButtonText(new Rect(x, rect.y, w, 24f), "Backdate pairs 3h"))
        {
            var n = PairStore.Backdate(3 * NarrativeMath.TicksPerHour);
            Messages.Message($"{n} pair(s) backdated — the next conversation between any of them " +
                             "will carry the recall block.", MessageTypeDefOf.NeutralEvent, false);
        }
        x += w + Pad;

        if (Widgets.ButtonText(new Rect(x, rect.y, w, 24f), "Add chronicle entry"))
        {
            var ok = Chronicle.Record(GenTicks.TicksGame, "Hunted", "dev|test",
                                      "somebody hunted a boar");
            Messages.Message(ok ? "Chronicle entry added." : "Refused — one is already there (dedupe).",
                             MessageTypeDefOf.NeutralEvent, false);
        }
        x += w + Pad;

        if (Widgets.ButtonText(new Rect(x, rect.y, w, 24f), "Generate a goal"))
        {
            GoalService.TryGenerate();
            Messages.Message("Goal generation asked for. It needs a free request slot and a " +
                             "colony with something wrong with it; watch the list.",
                             MessageTypeDefOf.NeutralEvent, false);
        }
        x += w + Pad;

        if (Widgets.ButtonText(new Rect(x, rect.y, w, 24f), "Evaluate goals now"))
        {
            GoalService.EvaluateAll();
            Messages.Message("Nightly goal pass run early.", MessageTypeDefOf.NeutralEvent, false);
        }
    }
}
