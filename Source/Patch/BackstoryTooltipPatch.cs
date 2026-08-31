using HarmonyLib;
using RimTalk.Data;
using RimWorld;
using UnityEngine;
using Verse;

namespace RimTalk.Patch;

/// <summary>
/// Appends the expanded backstory narrative to the stock backstory tooltip. rim-universe #51.
///
/// When a pawn has a personality expansion, their backstory mouseover shows
/// the RimWorld description followed by our LLM-generated narrative.
///
/// Uses a simple static to pass the pawn context, set when the character card
/// starts drawing and cleared when done.
/// </summary>
public static class BackstoryTooltipPatch
{
    /// <summary>
    /// The pawn whose card is currently being drawn. Set by FillTab prefix,
    /// read by FullDescriptionFor postfix.
    /// </summary>
    internal static Pawn CurrentPawn;
}

/// <summary>
/// Captures the pawn when the Bio tab starts drawing.
/// </summary>
[HarmonyPatch(typeof(ITab_Pawn_Character), "FillTab")]
public static class BioTabFillPatch
{
    public static void Prefix(ITab_Pawn_Character __instance)
    {
        // PawnToShowInfoAbout is protected, so use reflection on the instance's type hierarchy
        var prop = __instance.GetType().GetProperty("PawnToShowInfoAbout",
            System.Reflection.BindingFlags.Instance |
            System.Reflection.BindingFlags.NonPublic |
            System.Reflection.BindingFlags.Public |
            System.Reflection.BindingFlags.FlattenHierarchy);
        BackstoryTooltipPatch.CurrentPawn = prop?.GetValue(__instance) as Pawn;
    }

    public static void Postfix()
    {
        BackstoryTooltipPatch.CurrentPawn = null;
    }
}

/// <summary>
/// Appends the expanded narrative to backstory descriptions.
/// </summary>
[HarmonyPatch(typeof(BackstoryDef), nameof(BackstoryDef.FullDescriptionFor))]
public static class BackstoryDescriptionPatch
{
    public static void Postfix(ref TaggedString __result)
    {
        var pawn = BackstoryTooltipPatch.CurrentPawn;
        if (pawn == null || string.IsNullOrEmpty(__result)) return;

        var personality = PersonalityStore.Get(pawn);
        if (personality == null || string.IsNullOrWhiteSpace(personality.Narrative)) return;

        // Add a separator and the expanded narrative
        __result += "\n\n" +
            "RimTalk Expanded History".Colorize(ColoredText.TipSectionTitleColor) +
            "\n" +
            personality.Narrative;
    }
}
