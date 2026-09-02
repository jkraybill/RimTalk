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
/// Every row in a colonist's social tab carries the same hand
/// glyph, because the InteractionDef has one &lt;symbol&gt;. Vanilla rows survive that
/// because their text names both parties — "Charon and Jesse spoke about fighting
/// vipers" — but RimTalk rows are the generated line and nothing else, deliberately,
/// so the log becomes a wall of speech with no way to tell who said any of it.
///
/// Three cases and not two. A monologue sets recipient = initiator, so an
/// outward/inward pair alone would label every solo line as outward — and most rows
/// in the log the player screenshotted are monologues.
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

    /// <summary>
    /// The word that turns a bare interaction label into a directional one:
    /// "chitchat" becomes "outbound chitchat". Null where there is no direction
    /// worth claiming.
    ///
    /// A word rather than a whole sentence because it has to sit in front of a label
    /// that is localised, may be several words, and is not ours — "deep talk",
    /// "insult", whatever a mod adds next.
    ///
    /// "solo" covers the monologue. The first cut left it bare on the theory that a
    /// monologue is neither inbound nor outbound, which is true and useless: the player hit a
    /// grey bubble whose tooltip still said plain "Chitchat" and had to ask what it
    /// meant. If the glyph makes a claim, the tooltip says the claim out loud.
    /// </summary>
    public static string Adjective(SpeechDirection direction) => direction switch
    {
        SpeechDirection.Outward => "outbound",
        SpeechDirection.Inward => "inbound",
        SpeechDirection.Alone => "solo",
        // Overheard is the only one left bare, and on purpose: it also gets no glyph,
        // so the row keeps the vanilla hand and the vanilla tip together. Every row
        // that shows one of our bubbles now says which bubble it is.
        _ => null,
    };

    /// <summary>
    /// Put the adjective in front of the first line of a tooltip and leave the rest
    /// alone.
    ///
    /// First line only, because the vanilla tip is the label followed by the line
    /// itself and a timestamp — prefixing the whole string would put "outbound" in
    /// front of a paragraph. Lower-cases the character it displaces so "Chitchat"
    /// reads as "Outbound chitchat" and not "Outbound Chitchat".
    /// </summary>
    public static string Prefix(string tip, SpeechDirection direction)
    {
        var word = Adjective(direction);
        if (word == null || string.IsNullOrWhiteSpace(tip)) return tip;

        var breakAt = tip.IndexOf('\n');
        var first = breakAt < 0 ? tip : tip.Substring(0, breakAt);
        var rest = breakAt < 0 ? "" : tip.Substring(breakAt);

        if (first.Length == 0) return tip;

        // Already done. GetTipString can be called more than once for one row.
        if (first.StartsWith(word, System.StringComparison.OrdinalIgnoreCase)) return tip;

        var head = char.ToUpperInvariant(word[0]) + word.Substring(1);
        return head + " " + char.ToLowerInvariant(first[0]) + first.Substring(1) + rest;
    }

    /// <summary>
    /// Whether a vanilla interaction line addresses the recipient, rather than
    /// describing the two of them as a pair. the player's rule: an icon on
    /// "Nicole and Olga joked about peppers" over-claims, because the sentence says
    /// they talked, not that Nicole talked AT Olga.
    ///
    /// This has to read the sentence, and that is worth saying out loud: RimWorld's
    /// Chitchat def carries three log templates for one interaction and picks between
    /// them at render time.
    ///
    ///   [A] and [B] [talkedabout] [subject].          mutual
    ///   [A] [talkedabout] [subject] with [B].         mutual
    ///   [A] [commentedabout] [subject] to [B].        addressed
    ///
    /// The initiator and recipient are identical in all three. So "mutual" is a coin
    /// flip in the prose, not a fact about the event, and this function is a reading
    /// of English rather than of the game state.
    ///
    /// Fails closed: only "to &lt;name&gt;" counts as addressed. Anything unrecognised —
    /// another language, a mod's own rule pack — keeps the vanilla icon and claims
    /// nothing, which is where every row started.
    /// </summary>
    public static bool ReadsAsAddressed(string line, string recipientName)
    {
        if (string.IsNullOrWhiteSpace(line) || string.IsNullOrWhiteSpace(recipientName))
            return false;

        // Every occurrence, not the first. A sentence can open with the recipient's
        // name - "Olga listened as Nicole ranted to Olga" - and an occurrence at index
        // 0 has no preceding word to judge, so it must be skipped rather than end the
        // scan. Getting that wrong made the whole line unreadable.
        var at = line.IndexOf(recipientName, System.StringComparison.Ordinal);
        while (at >= 0)
        {
            if (at > 0)
            {
                // The word immediately before the name, whatever punctuation follows.
                var before = line.Substring(0, at).TrimEnd();
                var space = before.LastIndexOf(' ');
                var word = space < 0 ? before : before.Substring(space + 1);

                if (word.Equals("to", System.StringComparison.OrdinalIgnoreCase)) return true;
                if (word.Equals("and", System.StringComparison.OrdinalIgnoreCase)) return false;
                if (word.Equals("with", System.StringComparison.OrdinalIgnoreCase)) return false;
            }

            at = line.IndexOf(recipientName, at + 1, System.StringComparison.Ordinal);
        }

        return false;
    }
}
