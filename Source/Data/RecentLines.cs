using System.Collections.Generic;
using System.Linq;
using RimTalk.Prose;
using RimTalk.Util;
using Verse;

namespace RimTalk.Data;

/// <summary>
/// What each pawn has said lately, so they can be told not to say it again.
///
/// rim-universe #41. Measured: general n-gram overlap between generated lines is
/// low, but the *opening* line converges hard — "Quiet, isn't it?" came back 7 times
/// in 16 generations of the same scene, and the solo case, which is the opening of
/// every colony, was worst. That is what "slop" is at scale, and it is invisible in
/// a five-minute test.
///
/// Deliberately separate from <see cref="TalkHistory"/>'s message history. That list
/// is a conversation and is meant to be forgotten; this one has to outlive the
/// conversation and survive a save, because the repetition a player notices is one
/// colonist across hours, not twice in one exchange.
/// </summary>
public static class RecentLines
{
    /// <summary>
    /// Per pawn, matching what the prompt will actually use. One number, so the store
    /// cannot quietly keep a different amount from what the composer emits.
    /// </summary>
    public const int PerPawn = ProseSceneText.MaxRecentLines;

    /// <summary>
    /// Pawns tracked before the store prunes. A pawn who has left the map or died
    /// keeps their entry until then, which is the point — a visitor who comes back
    /// next quadrum should not open with the line they used last time.
    /// </summary>
    public const int MaxPawns = 200;

    static RimTalkWorldComponent Comp => Find.World?.GetComponent<RimTalkWorldComponent>();

    public static void Record(Pawn pawn, string line)
    {
        var comp = Comp;
        if (comp == null || pawn == null) return;

        var clean = Clean(line);
        if (string.IsNullOrWhiteSpace(clean)) return;

        var kept = Split(comp.RecentSpokenLines.TryGetValue(pawn.thingIDNumber, out var s) ? s : null);
        kept.RemoveAll(l => l == clean);        // a repeat moves to the front, not into a second slot
        kept.Insert(0, clean);
        while (kept.Count > PerPawn) kept.RemoveAt(kept.Count - 1);

        comp.RecentSpokenLines[pawn.thingIDNumber] = string.Join("\n", kept);
        if (comp.RecentSpokenLines.Count > MaxPawns) Prune(comp);
    }

    /// <summary>Newest first. Empty, never null.</summary>
    public static List<string> For(Pawn pawn)
    {
        var comp = Comp;
        if (comp == null || pawn == null) return new List<string>();
        return Split(comp.RecentSpokenLines.TryGetValue(pawn.thingIDNumber, out var s) ? s : null);
    }

    /// <summary>
    /// Everyone in the scene, interleaved newest-first-ish so no single speaker fills
    /// the whole ban list. The composer caps the result again.
    /// </summary>
    public static List<string> ForAll(IEnumerable<Pawn> pawns)
    {
        var lists = (pawns ?? Enumerable.Empty<Pawn>())
            .Where(p => p != null).Distinct().Select(For).Where(l => l.Count > 0).ToList();
        var merged = new List<string>();
        for (int i = 0; merged.Count < PerPawn && lists.Any(l => i < l.Count); i++)
            foreach (var l in lists)
                if (i < l.Count && !merged.Contains(l[i])) merged.Add(l[i]);
        return merged.Take(PerPawn).ToList();
    }

    /// <summary>
    /// Drop anyone the game no longer has. Only runs when the store is over its cap,
    /// so the cost is paid once per few hundred lines rather than per line.
    /// </summary>
    static void Prune(RimTalkWorldComponent comp)
    {
        var alive = new HashSet<int>();
        foreach (var map in Find.Maps ?? new List<Map>())
        foreach (var p in map?.mapPawns?.AllPawns ?? new List<Pawn>())
            if (p != null) alive.Add(p.thingIDNumber);
        foreach (var p in Find.WorldPawns?.AllPawnsAliveOrDead ?? new List<Pawn>())
            if (p != null) alive.Add(p.thingIDNumber);

        foreach (var gone in comp.RecentSpokenLines.Keys.Where(k => !alive.Contains(k)).ToList())
            comp.RecentSpokenLines.Remove(gone);
    }

    static List<string> Split(string blob) =>
        string.IsNullOrWhiteSpace(blob)
            ? new List<string>()
            : blob.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

    /// <summary>
    /// One line, no markup, no newlines — the value is stored newline-joined, so a
    /// line containing one would silently become two entries.
    /// </summary>
    static string Clean(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return null;
        return CommonUtil.StripFormattingTags(line)
            .Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
    }
}
