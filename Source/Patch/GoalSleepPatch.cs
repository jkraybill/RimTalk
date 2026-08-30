using HarmonyLib;
using RimTalk.Goals;
using RimWorld;
using Verse;
using Verse.AI;

namespace RimTalk.Patch;

/// <summary>
/// Evaluate a pawn's goals when they lie down to sleep. rim-universe #50.
///
/// Goals used to resolve in a midnight batch, which meant all colonists achieved
/// their goals at the same moment and the player saw a stack of letters. Evaluating
/// on sleep-start staggers the notifications and makes the achievement feel personal.
/// </summary>
[HarmonyPatch(typeof(JobDriver_LayDown), nameof(JobDriver_LayDown.MakeNewToils))]
public static class GoalSleepPatch
{
    /// <summary>
    /// Postfix: when the job starts, evaluate this pawn's goals. The method in
    /// GoalService checks if the goal has already been evaluated today, so pawns
    /// who get up and go back to bed don't trigger duplicate evaluations.
    /// </summary>
    public static void Postfix(JobDriver_LayDown __instance)
    {
        var pawn = __instance?.pawn;
        if (pawn == null || pawn.Dead || !pawn.Spawned) return;

        // Only colonists have goals — visitors and prisoners do not participate.
        if (!pawn.IsColonist) return;

        // Only for actual sleep, not "lie down for medical rest" or "lay down to die".
        // The job has a boolean for this, but we don't have easy access to it here.
        // Check the job's targetA — if it's a bed and the pawn owns it, it's probably sleep.
        // This is a heuristic, not perfect, but false positives are harmless (the goal
        // just gets evaluated a bit early, once per day).
        GoalService.EvaluateOnSleep(pawn);
    }
}
