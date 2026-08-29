namespace RimTalk.Data;

/// <summary>Who was talking, from the point of view of whoever is reading the log.</summary>
public enum SpeechDirection
{
    /// <summary>This pawn spoke to somebody.</summary>
    Outward,
    /// <summary>Somebody spoke to this pawn.</summary>
    Inward,
    /// <summary>Said aloud, to nobody in particular.</summary>
    Alone,
    /// <summary>Neither party is the reader — overheard, or a log opened on a third pawn.</summary>
    Overheard,
}

/// <summary>
/// rim-universe #43. Every row in a colonist's social tab carries the same hand
/// glyph, because the InteractionDef has one &lt;symbol&gt;. Vanilla rows survive that
/// because their text names both parties — "Charon and Jesse spoke about fighting
/// vipers" — but RimTalk rows are the generated line and nothing else, deliberately,
/// so the log becomes a wall of speech with no way to tell who said any of it.
///
/// Three cases and not two. A monologue sets recipient = initiator, so an
/// outward/inward pair alone would label every solo line as outward — and most rows
/// in the log JK screenshotted are monologues.
///
/// Pure so the three-way split is checkable; the colours it drives are next door.
/// </summary>
public static class Speech
{
    public static SpeechDirection Of(int povId, int initiatorId, int recipientId)
    {
        if (initiatorId == recipientId)
            return povId == initiatorId ? SpeechDirection.Alone : SpeechDirection.Overheard;

        if (povId == initiatorId) return SpeechDirection.Outward;
        if (povId == recipientId) return SpeechDirection.Inward;
        return SpeechDirection.Overheard;
    }

    /// <summary>
    /// The glyph for a direction, as a RimWorld content path. Pure so the three stay
    /// provably distinct: two directions sharing a texture is the whole feature
    /// failing, silently, and it looks identical to it working.
    /// </summary>
    public static string IconPath(SpeechDirection direction) => direction switch
    {
        SpeechDirection.Outward => "UI/Speech/Out",
        SpeechDirection.Inward => "UI/Speech/In",
        SpeechDirection.Alone => "UI/Speech/Alone",
        _ => null,   // overheard keeps the vanilla glyph
    };

    /// <summary>
    /// The tint for a direction, as r/g/b in 0..1. Held here without UnityEngine so
    /// it can be tested: warm and forward for speaking, cool for being spoken to, dim
    /// for talking to yourself.
    ///
    /// The three differ in BRIGHTNESS as well as hue, which is the part a colour-blind
    /// player depends on — and with #43 tier two they also differ in shape, so colour
    /// is never the only cue.
    /// </summary>
    public static (float R, float G, float B)? Tint(SpeechDirection direction) => direction switch
    {
        SpeechDirection.Outward => (1.00f, 0.85f, 0.45f),
        SpeechDirection.Inward => (0.55f, 0.80f, 1.00f),
        SpeechDirection.Alone => (0.62f, 0.62f, 0.62f),
        _ => null,
    };

    /// <summary>Rec. 709 relative luminance, for the contrast check in the tests.</summary>
    public static float Luminance((float R, float G, float B) c) =>
        (0.2126f * c.R) + (0.7152f * c.G) + (0.0722f * c.B);
}
