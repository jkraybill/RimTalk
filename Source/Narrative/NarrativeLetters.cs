using System.Linq;
using RimWorld;
using Verse;
using Logger = RimTalk.Util.Logger;

namespace RimTalk.Narrative;

/// <summary>
/// The output channel. rim-universe #36.
///
/// Speech bubbles vanish in under a second at real play speeds, so everything this
/// design produces is currently invisible. A letter is persistent, enters the
/// searchable Archive, and is clickable via LookTargets.
///
/// Restraint is the whole discipline here: letter spam is the fastest route to
/// uninstall, and vanilla already sends a letter for a colonist death. This adds
/// one ONLY when someone who mattered to the dead was there to see it — which is
/// the narrative beat rather than the bookkeeping.
/// </summary>
public static class NarrativeLetters
{
    /// <summary>Opinion at or above this counts as "mattered", in a colony big enough
    /// for the distinction to be meaningful.</summary>
    const float BondThreshold = 40f;

    /// <summary>
    /// At or below this many colonists, ANY witnessed death is a narrative beat and
    /// the relation gate is dropped.
    ///
    /// The first version required a relation or opinion >= 40, which would have
    /// suppressed the letter for exactly the case JK plays: a naked landing where
    /// three strangers arrive with no relations and near-zero opinion, and one of
    /// them dying is the most significant thing that has ever happened. R1.
    /// </summary>
    const int SmallColony = 5;

    public static void AnnounceDeath(Pawn dead, NarrativeEvent e)
    {
        try
        {
            if (dead == null || e == null || Find.LetterStack == null) return;
            if (!Settings.Get().Context.NarrativeLetters) return;

            var mourner = FindClosestWitness(dead, e);
            if (mourner == null) return;   // nobody who mattered saw it: vanilla's letter is enough

            var relation = mourner.GetMostImportantRelation(dead);
            var bond = relation != null
                ? relation.GetGenderSpecificLabelCap(dead).ToString().ToLower()
                : "one of the few people here";

            var label = $"{mourner.LabelShort} saw {dead.LabelShort} die";
            var text = $"{mourner.LabelShort} was there when {dead.LabelShort} died"
                     + (string.IsNullOrWhiteSpace(e.Detail) ? "" : $" of {e.Detail}")
                     + $". {dead.LabelShort} was their {bond}.\n\n"
                     + $"{mourner.LabelShort} will remember this.";

            Find.LetterStack.ReceiveLetter(label, text, LetterDefOf.NegativeEvent,
                new LookTargets(mourner));
        }
        catch (System.Exception ex)
        {
            Logger.Warning($"Narrative letter failed: {ex.Message}");
        }
    }

    static Pawn FindClosestWitness(Pawn dead, NarrativeEvent e)
    {
        if (dead?.Map?.mapPawns == null || dead.relations == null) return null;

        var colonists = dead.Map.mapPawns.FreeColonistsSpawned;
        bool small = colonists.Count <= SmallColony;

        return colonists
            .Where(p => p != null && !p.Dead && e.WasWitnessedBy(p))
            .Select(p => (pawn: p, score: SafeOpinion(p, dead), rel: p.GetMostImportantRelation(dead)))
            .Where(x => small || x.rel != null || x.score >= BondThreshold)
            .OrderByDescending(x => x.rel != null ? 1 : 0)
            .ThenByDescending(x => x.score)
            .Select(x => x.pawn)
            .FirstOrDefault();
    }

    static float SafeOpinion(Pawn a, Pawn b)
    {
        try { return a.relations.OpinionOf(b); }
        catch (System.Exception ex)
        {
            // A mod conflict here must not swallow the whole letter silently.
            Logger.Warning($"OpinionOf failed for {a?.LabelShort} -> {b?.LabelShort}: {ex.Message}");
            return 0f;
        }
    }
}
