using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimTalk.Data;
using RimTalk.Patches;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Patch;

/// <summary>
/// The direction indicator on a colonist's social tab. rim-universe #43.
///
/// A RimTalk row is the generated line and nothing else, deliberately — no "X said to
/// Y" scaffolding — so a tab full of them reads as a wall of speech with no way to
/// tell what this colonist said from what was said to them. Vanilla rows escape that
/// because their text names both parties.
///
/// PATCHED HERE RATHER THAN OVERRIDDEN ON THE ENTRY, and that is the whole point.
/// SaveGamePatch rewrites every PlayLogEntry_RimTalkInteraction in the live log into
/// a plain PlayLogEntry_Interaction on every save — autosaves included — so an
/// override on our subclass stops applying to a row the moment the game saves. The
/// text already survives that, through a postfix reading the world component; the
/// icon did not, which is why tier one shipped and was never visible for more than a
/// few minutes at a time.
///
/// The patch targets PlayLogEntry_Interaction and not LogEntry because vanilla
/// declares its own IconFromPOV there. A postfix on the base virtual would never run
/// for the entries this is about.
/// </summary>
[HarmonyPatch]
public static class SocialLogIconPatch
{
    /// <summary>
    /// Loaded once at startup. A missing texture is not worth an exception in a UI
    /// draw loop that runs every frame, so a failed lookup leaves the vanilla glyph.
    /// </summary>
    [StaticConstructorOnStartup]
    static class Icons
    {
        static readonly Dictionary<SpeechDirection, Texture2D> Cache = new();

        static Icons()
        {
            foreach (var direction in new[] { SpeechDirection.Outward, SpeechDirection.Inward, SpeechDirection.Alone })
            {
                var path = Speech.IconPath(direction);
                if (path == null) continue;

                var tex = ContentFinder<Texture2D>.Get(path, false);
                if (tex != null) Cache[direction] = tex;
                else Util.Logger.Warning($"Speech direction glyph missing: Textures/{path}.png");
            }
        }

        public static Texture2D For(SpeechDirection direction) =>
            Cache.TryGetValue(direction, out var tex) ? tex : null;
    }

    static bool IsOurs(LogEntry entry) =>
        entry is PlayLogEntry_RimTalkInteraction || InteractionTextPatch.IsRimTalkInteraction(entry);

    /// <summary>
    /// Vanilla rows we are willing to mark. The glyph is a speech bubble, so this is
    /// deliberately the talking interactions and not every social event — an outbound
    /// bubble on "Valley slowly approached Santo" would be wrong art for a romance
    /// attempt, whatever the direction says.
    /// </summary>
    static bool IsSpeech(InteractionDef def) =>
        def != null && (def == InteractionDefOf.Chitchat || def == InteractionDefOf.DeepTalk);

    static InteractionDef DefOf(LogEntry entry) =>
        AccessTools.Field(entry.GetType(), "intDef")?.GetValue(entry) as InteractionDef;

    /// <summary>
    /// Whether this row may carry one of our glyphs.
    ///
    /// Our own rows always may: they are the generated line and nothing else, so the
    /// icon is the only thing that says who spoke. A vanilla row only may when its
    /// sentence actually addresses the recipient — JK's rule, and see
    /// Speech.ReadsAsAddressed for why that is a reading of English and not of state.
    /// </summary>
    static bool MayMark(LogEntry entry, Pawn viewer, SpeechDirection direction)
    {
        if (IsOurs(entry)) return true;
        if (!IsSpeech(DefOf(entry))) return false;

        // A monologue has no second party to be addressed, and a vanilla row always
        // has one, so this only ever asks about a genuine two-pawn exchange.
        if (direction is not (SpeechDirection.Outward or SpeechDirection.Inward)) return false;

        var other = entry.GetConcerns()?.OfType<Pawn>().FirstOrDefault(p => p != viewer);
        return other != null
               && Speech.ReadsAsAddressed(entry.ToGameStringFromPOV(viewer), other.LabelShort);
    }

    /// <summary>
    /// Who was talking, for a row that may or may not still be our subclass.
    ///
    /// GetConcerns is the one accessor that survives the save conversion: it yields
    /// initiator then recipient on both shapes. A row concerning a single pawn is a
    /// monologue, which is Alone rather than Outward — most rows in a quiet colony
    /// are monologues, so getting that wrong would mislabel the majority of the log.
    /// </summary>
    static SpeechDirection? Direction(LogEntry entry, Thing pov)
    {
        if (pov is not Pawn viewer) return null;

        var concerns = entry.GetConcerns()?.OfType<Pawn>().ToList();
        if (concerns == null || concerns.Count == 0) return null;

        var initiator = concerns[0];
        var recipient = concerns.Count > 1 ? concerns[1] : initiator;

        var direction = Speech.Of(viewer.thingIDNumber, initiator.thingIDNumber, recipient.thingIDNumber);
        return MayMark(entry, viewer, direction) ? direction : null;
    }

    [HarmonyPatch(typeof(PlayLogEntry_Interaction), nameof(PlayLogEntry_Interaction.IconFromPOV))]
    [HarmonyPostfix]
    public static void IconFromPOV_Postfix(LogEntry __instance, Thing pov, ref Texture2D __result)
    {
        var direction = Direction(__instance, pov);
        if (direction == null) return;

        var tex = Icons.For(direction.Value);
        if (tex != null) __result = tex;
    }

    [HarmonyPatch(typeof(PlayLogEntry_Interaction), nameof(PlayLogEntry_Interaction.IconColorFromPOV))]
    [HarmonyPostfix]
    public static void IconColorFromPOV_Postfix(LogEntry __instance, Thing pov, ref Color? __result)
    {
        var direction = Direction(__instance, pov);
        if (direction == null) return;

        var tint = Speech.Tint(direction.Value);
        if (tint != null) __result = new Color(tint.Value.R, tint.Value.G, tint.Value.B);
    }

    /// <summary>
    /// "Chitchat" becomes "Outbound chitchat" on mouseover. JK's request, S169.
    ///
    /// GetTipString takes no point of view — it is one string for a row, not a string
    /// per reader — so the pawn has to come from the selection, which is the pawn
    /// whose tab is open in every case that matters. Anything else (multi-select,
    /// nothing selected) leaves the vanilla tip alone rather than guessing.
    ///
    /// Gated exactly like the icon, through the same Direction() call, because a row
    /// showing an outbound bubble whose tooltip declines to say "outbound" is worse
    /// than either on its own. One gate, not two agreeing ones — JK caught the first
    /// version stating it twice, which is the shape that quietly loses a copy.
    /// </summary>
    [HarmonyPatch(typeof(PlayLogEntry_Interaction), nameof(PlayLogEntry_Interaction.GetTipString))]
    [HarmonyPostfix]
    public static void GetTipString_Postfix(LogEntry __instance, ref string __result)
    {
        var viewer = Find.Selector?.SingleSelectedObject as Pawn;
        if (viewer == null) return;

        var direction = Direction(__instance, viewer);
        if (direction == null) return;

        __result = Speech.Prefix(__result, direction.Value);
    }
}
