namespace RimTalk.Data;

/// <summary>What a pawn's state does to how much they feel like talking.</summary>
public class TalkWeightFacts
{
    public float Baseline = 1f;

    /// <summary>Null when the pawn has no mood need. 0..1.</summary>
    public float? Mood;

    /// <summary>0..1, RimWorld's rest need. Null when the pawn does not sleep.</summary>
    public float? Rest;

    public bool Downed;
    public bool InMentalState;

    /// <summary>0..1 consciousness. Below about a quarter a pawn is barely present.</summary>
    public float? Consciousness;

    /// <summary>Held against their will, or owned. Re-read every time, not frozen at spawn.</summary>
    public bool Captive;

    /// <summary>Here briefly and not of the household — visitor, trader, raider.</summary>
    public bool Outsider;
}

/// <summary>
/// How likely a pawn is to start talking, computed when it is asked for rather than
/// written once at spawn.
///
/// rim-universe #16. TalkInitiationWeight was set in Hediff_Persona.GetOrAddNew and
/// never written again anywhere — grep confirmed the only other write was ExposeData.
/// So a pawn at 8% mood talked as much as one at 90%; bleeding out, exhausted and
/// missing a jaw made no difference; and a prisoner recruited into the colony kept
/// the 0.2 they were assigned as a captive, permanently, because the branch that set
/// it never ran again. That last one is a straightforward bug wearing a design
/// problem's clothes.
///
/// Multiplicative on purpose: the factors compound the way the states do — a downed,
/// exhausted, miserable pawn should be quieter than any one of those alone. Each
/// factor is clamped so no single one can silence a pawn outright; only the captive
/// and outsider terms are meant to be large, and even they leave a voice.
///
/// Pure, and source-linked into the test project, because "a recruited prisoner
/// starts talking again" is a claim that should not need a colony and an afternoon.
/// </summary>
public static class TalkWeight
{
    /// <summary>The floor. A pawn is never made completely mute by circumstance.</summary>
    public const float Min = 0.05f;

    public static float Effective(TalkWeightFacts f)
    {
        if (f == null) return Min;

        var w = f.Baseline <= 0f ? 0f : f.Baseline;
        if (w <= 0f) return 0f;      // an explicit zero is the player switching them off

        w *= MoodFactor(f.Mood);
        w *= RestFactor(f.Rest);
        w *= HealthFactor(f);
        w *= StandingFactor(f);

        return w < Min ? Min : w;
    }

    /// <summary>
    /// Misery quiets people and elation loosens them, and the middle is flat — a
    /// gradient across the whole range would make every pawn's chattiness twitch with
    /// every meal.
    /// </summary>
    public static float MoodFactor(float? mood)
    {
        if (mood == null) return 1f;
        var m = mood.Value;
        if (m < 0.15f) return 0.4f;      // close to breaking
        if (m < 0.35f) return 0.7f;      // worn down
        if (m > 0.85f) return 1.2f;      // genuinely good spirits
        return 1f;
    }

    /// <summary>Exhausted people do not chat. RimWorld's rest need is 0..1.</summary>
    public static float RestFactor(float? rest)
    {
        if (rest == null) return 1f;
        var r = rest.Value;
        if (r < 0.10f) return 0.3f;      // about to collapse
        if (r < 0.28f) return 0.6f;      // tired
        return 1f;
    }

    public static float HealthFactor(TalkWeightFacts f)
    {
        // A mental break is not quiet — it is the loudest a pawn gets — but it is not
        // conversation either. Left at 1: what they say is the mental-state prompt's
        // job, and suppressing them here would silence the most dramatic moment there is.
        if (f.InMentalState) return 1f;

        var w = 1f;
        if (f.Downed) w *= 0.35f;

        if (f.Consciousness != null)
        {
            var c = f.Consciousness.Value;
            if (c < 0.25f) w *= 0.3f;
            else if (c < 0.60f) w *= 0.7f;
        }

        return w;
    }

    /// <summary>
    /// The branch that used to run once at spawn and freeze. Recomputed, so recruiting
    /// a prisoner gives them their voice back.
    /// </summary>
    public static float StandingFactor(TalkWeightFacts f)
    {
        if (f.Captive) return 0.2f;
        if (f.Outsider) return 0.2f;
        return 1f;
    }
}
