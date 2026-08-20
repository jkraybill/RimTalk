using System.Collections.Generic;
using RimTalk.Data;
using RimWorld;
using UnityEngine;
using Verse;
using RimTalk.Service;

namespace RimTalk;

public class PlayLogEntry_RimTalkInteraction : PlayLogEntry_Interaction
{
    private string _cachedString;

    public PlayLogEntry_RimTalkInteraction()
    {
        // Parameterless constructor required for Scribing (loading from save)
    }

    public PlayLogEntry_RimTalkInteraction(
        InteractionDef interactionDef,
        Pawn initiator,
        Pawn recipient,
        List<RulePackDef> rules)
        : base(interactionDef, initiator, recipient, rules)
    {
        _cachedString = TalkService.GetTalk(initiator);
    }

    public Pawn Initiator => initiator;
    public Pawn Recipient => recipient;
    public List<RulePackDef> ExtraSentencePacks => extraSentencePacks;
    public string CachedString => _cachedString;
    public int TicksAbs => ticksAbs;

    // Override this method to customize the log message
    protected override string ToGameStringFromPOV_Worker(Thing pov, bool forceLog)
    {
        return _cachedString;
    }

    /// <summary>
    /// Tint the glyph by who was talking. rim-universe #43, tier one: the log had one
    /// icon for every row and RimTalk rows carry no names, so a colonist's social tab
    /// was a wall of undifferentiated speech.
    ///
    /// Colour rather than new art because IconFromPOV returns a Texture2D and this
    /// returns a Color? — the hook for tinting already exists and needs no PNG, no
    /// patch and no vanilla UI internals. Three glyphs are tier two; the portrait JK
    /// actually wants does not fit through either hook and is costed in the issue.
    /// </summary>
    public override Color? IconColorFromPOV(Thing pov)
    {
        if (pov is not Pawn viewer) return base.IconColorFromPOV(pov);

        return Speech.Of(viewer.thingIDNumber,
                         initiator?.thingIDNumber ?? -1,
                         recipient?.thingIDNumber ?? -1) switch
        {
            // Warm and forward for speaking, cool for being spoken to, dim for talking
            // to yourself. Readable against the dark log, and distinguishable without
            // relying on hue alone — the three differ in brightness as well.
            SpeechDirection.Outward => new Color(1.00f, 0.85f, 0.45f),
            SpeechDirection.Inward => new Color(0.55f, 0.80f, 1.00f),
            SpeechDirection.Alone => new Color(0.62f, 0.62f, 0.62f),
            _ => base.IconColorFromPOV(pov),
        };
    }
}