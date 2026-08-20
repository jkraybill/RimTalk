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
}
