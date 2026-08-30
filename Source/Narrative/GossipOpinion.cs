using System.Collections.Generic;
using System.Linq;

namespace RimTalk.Narrative;

/// <summary>What hearing a piece of gossip did to the listener's view of the world.</summary>
public enum GossipVerdict
{
    /// <summary>They heard it and it changed nothing, which is most of the time.</summary>
    Nothing,

    /// <summary>They took the speaker's side. The subject moves, in the direction of the news.</summary>
    Believed,

    /// <summary>They took the subject's side. The speaker takes the hit for talking.</summary>
    Disbelieved,
}

/// <summary>
/// Gossip that moves an opinion. rim-universe #22 + #31's Social domain — JK's
/// design, S169, with three amendments.
///
/// His shape: roll against the delta between how much the listener likes the speaker
/// and how much they like the subject, land on one side, and move both.
///
/// **The roll is inverted from the original.** As proposed, the chance of shifting
/// ROSE with the delta and the shift then widened it — positive feedback. Two
/// colonists five points apart end at the caps and the colony polarises into cliques
/// out of noise. It is also backwards about people: you do not need convincing about
/// someone you already despise. So certainty DAMPS. Maximum movement is at delta
/// zero, where the listener has no side and the tie-break decides, and nothing at all
/// moves past CertaintyRange.
///
/// **Valence carries.** The original assumed gossip is damning. It is not: half the
/// harvested tales are somebody finishing a research project or getting married, and
/// believing a compliment must not punish its subject.
///
/// **Nothing here writes a number.** RimWorld opinion is derived from memories, so
/// the caller turns a verdict into ThoughtDefs — which is where the cap and the decay
/// come from, and the second reason the runaway cannot happen.
/// </summary>
public static class GossipOpinion
{
    /// <summary>
    /// A quarter, at the point of maximum uncertainty and for an ordinary listener.
    /// Most hearings change nothing, which is the honest rate: colonists gossip
    /// constantly and revise their opinions of each other rarely.
    /// </summary>
    public const int BasePercent = 25;

    /// <summary>
    /// Forty points of opinion. Past this the listener has made their mind up and a
    /// rumour does not touch it.
    ///
    /// Chosen against RimWorld's own scale rather than tuned: -100..100, where a
    /// rival sits around -40 and a friend around +40. Somebody you actively like more
    /// than the person being talked about is exactly who you stop reassessing.
    /// </summary>
    public const int CertaintyRange = 40;

    /// <summary>
    /// How much this listener is moved by talk, as a percentage of the base. Traits
    /// only — RimWorld has no dogmatism stat, and Ideology's Certainty is real but
    /// DLC-gated, so leaning on it would make the feature vanish for half the players.
    /// </summary>
    public static int Suggestibility(IEnumerable<string> traitDefNames)
    {
        var pct = 100;
        foreach (var t in (traitDefNames ?? Enumerable.Empty<string>()).Where(t => !string.IsNullOrWhiteSpace(t)))
            pct = pct * Factor(t.Trim()) / 100;

        return pct < 0 ? 0 : pct;
    }

    static int Factor(string trait) => trait switch
    {
        // Not indifferent to people — indifferent to what people SAY about people.
        "Psychopath" => 20,
        "Bloodlust" => 80,
        // Slower to think badly of somebody on hearsay.
        "Kind" => 70,
        // Quicker to.
        "Abrasive" => 130,
        _ => 100,
    };

    /// <summary>
    /// The chance this hearing moves anything, 0-100.
    ///
    /// <paramref name="delta"/> is opinion-of-speaker minus opinion-of-subject.
    /// </summary>
    public static int ChancePercent(int delta, int suggestibility)
    {
        var certainty = System.Math.Abs(delta);
        if (certainty >= CertaintyRange) return 0;

        var damped = BasePercent * (CertaintyRange - certainty) / CertaintyRange;
        var scaled = damped * suggestibility / 100;

        return scaled < 0 ? 0 : scaled > 100 ? 100 : scaled;
    }

    /// <summary>
    /// The verdict. Rolls are passed in rather than drawn, so every branch is
    /// reachable from a test and the outcome does not depend on Rand's state.
    ///
    /// <paramref name="roll"/> and <paramref name="tieBreak"/> are 0-99.
    /// </summary>
    public static GossipVerdict Resolve(int delta, int valence, int chance, int roll, int tieBreak)
    {
        // Neutral news has nothing to be believed or disbelieved ABOUT. A party
        // happened; there is no version of that which reflects on anyone.
        if (valence == 0) return GossipVerdict.Nothing;
        if (roll >= chance) return GossipVerdict.Nothing;

        if (delta > 0) return GossipVerdict.Believed;
        if (delta < 0) return GossipVerdict.Disbelieved;

        // No lean either way, so the rumour lands wherever it lands. This is the case
        // the damping makes MOST likely, on purpose: an open mind is the one a story
        // can actually change.
        return tieBreak % 2 == 0 ? GossipVerdict.Believed : GossipVerdict.Disbelieved;
    }

    /// <summary>
    /// Whether an event reflects well or badly on the people in it: +1, -1, or 0.
    ///
    /// By TaleDef, at the point of use rather than at harvest, so re-reading an old
    /// save cannot disagree with a newer table. Anything unlisted is neutral and moves
    /// nothing, which is the right default for a modded tale nobody here has seen.
    /// </summary>
    public static int Valence(string kind) => (kind ?? "").Trim() switch
    {
        "SocialFight" or "Breakup" or "ExecutedPrisoner" or "IllnessRevealed" => -1,

        "Marriage" or "BecameLover" or "GaveBirth" or "Recruited"
            or "CraftedArt" or "FinishedResearchProject" or "KilledMajorThreat"
            or "GainedMasterSkillWithPassion" or "MinedValuable" or "Hunted"
            or "TamedAnimal" or "BondedWithAnimal" or "DidSurgery" or "LaunchedShip"
            or "CompletedLongConstructionProject" or "CompletedLongCraftingProject" => 1,

        _ => 0,
    };
}
