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

    // No IconFromPOV/IconColorFromPOV override here on purpose. rim-universe #43 tier
    // one put the tint on this class and it was invisible within minutes: SaveGamePatch
    // rewrites every entry of this type in the live log into a plain
    // PlayLogEntry_Interaction on each save, autosaves included, so the override stopped
    // applying to rows that still looked like ours. Both hooks now live in
    // SocialLogIconPatch, which patches PlayLogEntry_Interaction and therefore covers
    // this class and its converted twin through one code path.
}