using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RimTalk.Goals;
using RimTalk.Service;
using Verse;
using Logger = RimTalk.Util.Logger;
using S = RimTalk.Narrative.SnapshotText;

namespace RimTalk.Narrative;

/// <summary>
/// The Narrative panel, as a file. rim-universe S169.
///
/// JK, on being asked for a screenshot of it: "why can't we make it so those panels
/// are machine-readable?" He is right, and it had been a screenshot only because the
/// panel was written for a person to read and nobody extended it.
///
/// A play session is the expensive resource in this project. Every run so far has
/// ended with him photographing a panel and me reading numbers out of a picture, or
/// grepping a megabyte of Player.log for a string. This writes the same state where
/// it can just be read.
///
/// Written on every save — autosaves included, so it keeps itself current with no
/// effort from the player — and on demand from the dev button.
/// </summary>
public static class Snapshot
{
    /// <summary>
    /// Beside the saves, under a folder of our own. Not in the save file: this is
    /// disposable diagnostics, it must never be something a colony depends on, and it
    /// should be deletable without a thought.
    /// </summary>
    public static string Path =>
        System.IO.Path.Combine(GenFilePaths.SaveDataFolderPath, "RimTalk", "snapshot.json");

    /// <summary>
    /// Write, or say why not. Never throws: this runs inside the save path, and
    /// diagnostics that can break a save are worse than no diagnostics.
    /// </summary>
    public static bool Write()
    {
        try
        {
            var dir = System.IO.Path.GetDirectoryName(Path);
            if (dir != null && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            File.WriteAllText(Path, Build());
            return true;
        }
        catch (Exception e)
        {
            Logger.Warning($"Narrative snapshot could not be written: {e.Message}");
            return false;
        }
    }

    static string Build()
    {
        var now = GenTicks.TicksGame;
        var comp = Find.World?.GetComponent<Data.RimTalkWorldComponent>();

        return S.Obj(
            S.Field("tick", now),
            S.Field("colony", Find.CurrentMap?.Parent?.LabelCap ?? "?"),
            $"{S.Str("recipients")}:{Recipients()}",
            S.Field("socialEffectsApplied", TalkService.SocialEffectsApplied),
            $"{S.Str("gossip")}:{Gossip(now)}",
            $"{S.Str("opinionShifts")}:{S.ArrOfStrings(GossipEffect.Recent)}",
            $"{S.Str("pairs")}:{Pairs(now)}",
            $"{S.Str("chronicle")}:{ChronicleRows(comp, now)}",
            $"{S.Str("goals")}:{GoalRows()}",
            S.Field("deaths", NarrativeStore.All.Count),
            $"{S.Str("invariants")}:{Checks(comp, now)}");
    }


    /// <summary>
    /// The verdicts. Ordered so a reader hitting a false stops there — a violated
    /// invariant is the finding, and everything below it is context for it.
    /// </summary>
    public static List<Invariant> Verdicts()
    {
        var comp = Find.World?.GetComponent<Data.RimTalkWorldComponent>();
        var pairs = PairStore.Snapshot();
        var entries = comp?.ChronicleEntries ?? new List<ChronicleEntry>();
        var aboutPeople = entries.Where(e => e != null && (e.SubjectId != 0 || e.OtherId != 0)).ToList();

        TalkService.RecipientSources.TryGetValue(TalkService.RecipientSource.Monologue, out var solo);
        var total = TalkService.RecipientSources.Values.Sum();

        return new List<Invariant>
        {
            Invariants.PairExchangesAreTwoPeople(pairs.Select(p => DistinctSpeakers(p.LastExchange))),
            Invariants.MostLinesAreAddressed(solo, total),
            Invariants.SocialEffectsFire(TalkService.SocialEffectsApplied, total - solo),
            Invariants.GossipableEventsHaveWitnesses(aboutPeople.Count, aboutPeople.Count(e => e.KnownBy.Count == 0)),
        };
    }

    static string Checks(Data.RimTalkWorldComponent comp, int now) =>
        S.Arr(Verdicts().Select(r => S.Obj(
            S.Field("name", r.Name),
            S.Field("ok", r.Ok ? 1 : 0),
            S.Field("undecided", r.Undecided ? 1 : 0),
            S.Field("detail", r.Detail))));

    static string Recipients()
    {
        var fields = new List<string>();
        foreach (TalkService.RecipientSource k in Enum.GetValues(typeof(TalkService.RecipientSource)))
        {
            TalkService.RecipientSources.TryGetValue(k, out var n);
            fields.Add(S.Field(k.ToString(), n));
        }
        return S.Obj(fields.ToArray());
    }

    static string Gossip(int now)
    {
        var pool = Chronicle.GossipPool(now);
        var items = pool.Select(x => S.Obj(
            S.Field("clause", x.Item.Clause),
            S.Field("ageTicks", now - x.Item.Tick),
            S.Field("kind", x.Item.Kind),
            S.Field("valence", GossipOpinion.Valence(x.Item.Kind)),
            S.Field("knownBy", x.Item.KnownBy.Count)));

        return S.Obj(S.Field("poolSize", pool.Count), $"{S.Str("items")}:{S.Arr(items)}");
    }

    static string Pairs(int now)
    {
        var pairs = PairStore.Snapshot();
        var rows = pairs.OrderByDescending(p => p.LastMetTick).Take(20).Select(p => S.Obj(
            S.Field("a", p.AName),
            S.Field("b", p.BName),
            S.Field("timesMet", p.TimesMet),
            S.Field("lastMetAgoTicks", now - p.LastMetTick),
            S.Field("pastGate", PairMath.WorthRecalling(p.LastMetTick, now) ? 1 : 0),
            // The S169 contamination check, machine-readable: how many DISTINCT
            // speakers the remembered exchange has. Anything above two is the bug back.
            S.Field("speakersInExchange", DistinctSpeakers(p.LastExchange)),
            $"{S.Str("lastExchange")}:{S.ArrOfStrings(p.LastExchange)}"));

        return S.Arr(rows);
    }

    static int DistinctSpeakers(List<string> lines)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var l in lines ?? new List<string>())
        {
            var colon = l?.IndexOf(':') ?? -1;
            if (colon > 0) names.Add(l.Substring(0, colon).Trim());
        }
        return names.Count;
    }

    static string ChronicleRows(Data.RimTalkWorldComponent comp, int now)
    {
        var entries = comp?.ChronicleEntries ?? new List<ChronicleEntry>();
        var rows = entries.AsEnumerable().Reverse().Take(20).Select(e => S.Obj(
            S.Field("clause", e.Clause),
            S.Field("kind", e.Kind),
            S.Field("ageTicks", now - e.Tick),
            S.Field("subjectId", e.SubjectId),
            S.Field("otherId", e.OtherId),
            S.Field("knownBy", e.KnownBy.Count)));

        return S.Obj(S.Field("total", entries.Count), $"{S.Str("recent")}:{S.Arr(rows)}");
    }

    static string GoalRows()
    {
        // GoalStore.All is private, and rightly so — the public surface is the one
        // the game uses. Read the component directly rather than widening it for a
        // diagnostic.
        var all = Find.World?.GetComponent<Data.RimTalkWorldComponent>()?.GoalEntries
                  ?? new List<GoalEntry>();

        var rows = all.AsEnumerable().Reverse().Take(10).Where(g => g != null).Select(g => S.Obj(
            S.Field("pawn", g.PawnName),
            S.Field("kind", g.Kind.ToString()),
            S.Field("state", g.State.ToString()),
            S.Field("statement", g.Statement)));

        return S.Obj(S.Field("total", all.Count), $"{S.Str("recent")}:{S.Arr(rows)}");
    }
}
