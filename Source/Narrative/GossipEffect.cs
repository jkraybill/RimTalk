using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;
using Logger = RimTalk.Util.Logger;

namespace RimTalk.Narrative;

/// <summary>
/// The write. rim-universe #22 + #31's Social domain, and the first thing in the mod
/// that changes the simulation rather than describing it.
///
/// This file reads the game and hands numbers to <see cref="GossipOpinion"/>, which
/// holds every judgement and runs in the test suite. Nothing here decides anything.
/// </summary>
public static class GossipEffect
{
    /// <summary>
    /// The last few verdicts, for the Narrative panel.
    ///
    /// An opinion shift of two points inside a hundred-point scale is invisible in
    /// the social tab unless you knew the number before, and "most hearings change
    /// nothing" is the mechanic's honest behaviour — so a run where it never fires
    /// and a run where it is broken look identical without this.
    /// </summary>
    public static readonly List<string> Recent = new();

    const int MaxRecent = 12;

    static void Note(string line)
    {
        Recent.Add(line);
        if (Recent.Count > MaxRecent) Recent.RemoveAt(0);
    }

    /// <summary>
    /// Resolve one piece of gossip for one listener.
    ///
    /// Only ever called for news — an item the listener did not already know — so
    /// rehashing the same rejection for the fifth time cannot move an opinion five
    /// times. That gate lives in GossipMath.Tell, which returns exactly the items
    /// that were new.
    /// </summary>
    public static void Apply(Pawn listener, Pawn speaker, Pawn subject, string kind)
    {
        if (listener == null || speaker == null || subject == null) return;
        if (listener == speaker || listener == subject || speaker == subject) return;
        if (listener.needs?.mood?.thoughts?.memories == null) return;

        var valence = GossipOpinion.Valence(kind);
        if (valence == 0) return;

        var delta = listener.relations.OpinionOf(speaker) - listener.relations.OpinionOf(subject);
        var chance = GossipOpinion.ChancePercent(delta, GossipOpinion.Suggestibility(Traits(listener)));
        if (chance <= 0) return;

        var verdict = GossipOpinion.Resolve(delta, valence, chance, Rand.Range(0, 100), Rand.Range(0, 100));

        // Recorded even when nothing moved. A panel that only shows the hits cannot
        // tell "it never fired" from "it fired and rolled Nothing", which is the
        // distinction the whole instrument exists for.
        Note($"{listener.LabelShort} heard about {subject.LabelShort} from {speaker.LabelShort} " +
             $"[{kind} {(valence > 0 ? "+" : "-")}] delta {delta}, {chance}% -> {verdict}");

        if (verdict == GossipVerdict.Nothing) return;

        if (verdict == GossipVerdict.Believed)
        {
            // They took the speaker's word for it. The subject moves the way the news
            // points — a compliment believed must not punish its subject — and the
            // speaker gains a little for being the one who keeps them informed.
            Remember(listener, subject, valence > 0 ? "RimTalk_GossipHeardGood" : "RimTalk_GossipHeardBad");
            Remember(listener, speaker, "RimTalk_GossipToldMe");
        }
        else
        {
            // They took the subject's side, so the subject does not move at all and
            // the speaker wears it. Disbelief is about the teller, not the tale.
            Remember(listener, speaker, "RimTalk_GossipTalksAboutPeople");
        }

        Logger.Debug($"Gossip {verdict}: {listener.LabelShort} heard about {subject.LabelShort} " +
                     $"from {speaker.LabelShort} (delta {delta}, chance {chance}%, {kind})");
    }

    static void Remember(Pawn listener, Pawn about, string defName)
    {
        var def = DefDatabase<ThoughtDef>.GetNamedSilentFail(defName);
        if (def == null)
        {
            // A missing def is a packaging fault, not a gameplay one. Say so once
            // rather than throwing inside a conversation.
            Logger.Warning($"Gossip thought {defName} is missing; opinion unchanged.");
            return;
        }

        listener.needs.mood.thoughts.memories.TryGainMemory(def, about);
    }

    static IEnumerable<string> Traits(Pawn p) =>
        p?.story?.traits?.allTraits?
            .Where(t => t?.def != null)
            .Select(t => t.def.defName)
        ?? Enumerable.Empty<string>();
}
