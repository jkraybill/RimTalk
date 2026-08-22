using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Narrative;

/// <summary>
/// The parts of the narrative layer that are decidable without a running game.
///
/// Split out for the same reason Describer is: no RimWorld dependency means this
/// file is source-linked into the test project and its behaviour is actually
/// verified, rather than inspected. Everything that needs a Pawn or a Map lives in
/// NarrativeEvent / NarrativeStore and cannot be tested here.
/// </summary>
public static class NarrativeMath
{
    public const int TicksPerDay = 60000;
    public const int DaysPerQuadrum = 15;
    public const int DaysPerYear = 60;

    /// <summary>
    /// How long ago, in words. Boundaries matter more than they look: this is the
    /// only phrasing a pawn has for the passage of time, and JK's memorable stories
    /// are measured in quadrums and generations.
    /// </summary>
    public static string Elapsed(long elapsedTicks)
    {
        if (elapsedTicks < 0) elapsedTicks = 0;
        var days = elapsedTicks / (double)TicksPerDay;

        if (days < 1d) return "today";
        if (days < 2d) return "yesterday";
        if (days < DaysPerQuadrum) return $"{(int)System.Math.Round(days)} days ago";

        var years = days / DaysPerYear;
        if (years >= 1d)
        {
            var y = (int)System.Math.Round(years);
            return y == 1 ? "a year ago" : $"{y} years ago";
        }

        var q = (int)System.Math.Round(days / DaysPerQuadrum);
        return q <= 1 ? "a quadrum ago" : $"{q} quadrums ago";
    }

    /// <summary>
    /// Add a witness id, refusing self-witness and duplicates. Returns false when
    /// nothing was added, so callers can keep the parallel label list in step.
    /// </summary>
    public static bool TryAddWitness(List<int> ids, int witnessId, int subjectId)
    {
        if (ids == null) return false;
        if (witnessId == subjectId) return false;
        if (ids.Contains(witnessId)) return false;
        ids.Add(witnessId);
        return true;
    }

    /// <summary>
    /// What happened in a window, newest first. rim-universe #30's delta: the events
    /// between the last time two people spoke and now.
    ///
    /// STRICTLY after <paramref name="afterTick"/>. The meeting itself is the anchor,
    /// not part of what has happened since it — an inclusive bound puts whatever was
    /// recorded on that same tick into "since then", which reads as the pair being
    /// told about something they were standing in front of.
    /// </summary>
    public static IEnumerable<T> Since<T>(IEnumerable<T> events, int afterTick,
                                          System.Func<T, int> tick, int max)
    {
        if (events == null || max <= 0) return Enumerable.Empty<T>();
        return events.Where(e => tick(e) > afterTick).OrderByDescending(tick).Take(max);
    }

    /// <summary>Drop oldest-first until the list fits. A decades-long save must be bounded.</summary>
    public static void Trim<T>(List<T> list, int max)
    {
        if (list == null || max < 0) return;
        while (list.Count > max) list.RemoveAt(0);
    }

    /// <summary>
    /// Rank for recall: what a pawn saw outranks what they were told, and newer
    /// outranks older within each group. That distinction is the entire reason
    /// witnesses are tracked at all.
    /// </summary>
    public static IEnumerable<T> Rank<T>(IEnumerable<T> events, System.Func<T, bool> witnessed,
                                         System.Func<T, int> tick, int max)
    {
        if (events == null) return Enumerable.Empty<T>();
        return events
            .OrderByDescending(e => witnessed(e) ? 1 : 0)
            .ThenByDescending(tick)
            .Take(max < 0 ? 0 : max);
    }
}
