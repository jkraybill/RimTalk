using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.Narrative;
using RimTalk.Prose;
using RimTalk.Service;
using RimTalk.Util;
using RimWorld;
using Verse;

namespace RimTalk.Goals;

/// <summary>
/// Sets goals and, more importantly, resolves them. rim-universe #28.
///
/// Two halves on two schedules, and they are not symmetric. Generation is an API
/// call and rides the cache refresh, one candidate at a time, behind the same
/// in-flight gate as everything else. Evaluation is free, runs nightly, and must
/// never be skipped — an unresolved goal is the failure this whole issue is about.
/// </summary>
public static class GoalService
{
    static bool _generating;

    /// <summary>
    /// A pawn who could take a goal on. Cheap: no map read, no colony scan. The
    /// expensive half happens once, for the one pawn that gets picked.
    /// </summary>
    public static bool WantsGoal(Pawn pawn) =>
        Settings.Get().Context.Goals && ArrivalService.InOrbit(pawn) && GoalStore.HasRoom(pawn);

    public static Pawn NextNeeding() =>
        _generating || AIService.IsBusy() || Find.World == null
            ? null
            : Cache.Keys.FirstOrDefault(WantsGoal);

    public static void TryGenerate()
    {
        var pawn = NextNeeding();
        if (pawn == null) return;

        _generating = true;
        _ = GenerateFor(pawn).ContinueWith(_ => _generating = false);
    }

    static async Task GenerateFor(Pawn pawn)
    {
        try
        {
            var facts = ProseScene.GatherColony(pawn?.Map);
            var shortlist = GoalStore.Allowed(pawn, GoalMath.Candidates(facts), GenTicks.TicksGame);

            // Nothing wrong here, or everything wrong here is on cooldown. Both are
            // real answers and neither is worth an API call: #28's own mitigation for
            // taxing the player's strategy is that goals come from actual deficiencies,
            // and a pawn with nothing to want is a pawn who talks about something else.
            if (shortlist.Count == 0) return;

            var prompt = GoalText.Prompt(PromptService.CreatePawnContext(pawn),
                                         ProseColonyText.Compose(facts), shortlist);
            if (prompt == null) return;

            var data = await AIService.Query<GoalData>(new TalkRequest(prompt, pawn));
            var accepted = GoalText.Accept(data?.Kind, data?.Goal, shortlist);
            if (accepted == null)
            {
                // Refused, not repaired. A goal is canon for ten days and the pawn
                // keeps saying it, so a half-understood one is worse than none. The
                // slot stays open and the next refresh tries again.
                Logger.Message($"Goal for {pawn.LabelShort} was refused (no allowed kind, empty, " +
                               "over-long, or it decided how they got here). Will retry.");
                return;
            }

            // The target is read AGAIN here rather than reused from above: the await
            // is a real gap and the colony may have moved. A goal born already met is
            // one of the three failures #28 predicts.
            var now = GenTicks.TicksGame;
            var current = ProseScene.GatherColony(pawn?.Map);
            var target = GoalMath.Target(accepted.Kind, current);
            if (GoalMath.IsMet(accepted.Kind, target, current)) return;

            GoalStore.Record(new GoalEntry(pawn, accepted.Kind, accepted.Statement,
                                           target, now, GoalMath.SpanTicks));
            Logger.Debug($"Goal: {pawn.LabelShort} wants {accepted.Kind} — \"{accepted.Statement}\"");
        }
        catch (Exception e)
        {
            Logger.Error($"Goal generation failed: {e.Message}");
        }
    }

    /// <summary>
    /// The nightly pass. Free, and the reason the feature is not a nag.
    ///
    /// Grouped by map because the colony state is per-map and reading it once per
    /// goal would walk the resource counter a dozen times a night.
    /// </summary>
    public static void EvaluateAll()
    {
        // Not gated on the setting. A player who turns goals OFF mid-save still has
        // active ones stored, and leaving them unevaluated forever is the exact
        // "nags for a hundred days" failure — they should resolve out, not freeze.
        var now = GenTicks.TicksGame;
        var byMap = new Dictionary<Map, ColonyFacts>();

        foreach (var entry in GoalStore.ActiveEntries())
        {
            try
            {
                var pawn = Cache.Keys.FirstOrDefault(p => p?.thingIDNumber == entry.PawnId);

                // Gone: dead, captured, left with a caravan. Abandoned rather than
                // expired — nobody failed, the person is simply not here.
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    GoalStore.Resolve(entry, GoalState.Abandoned, now);
                    continue;
                }

                var map = pawn.Map;
                if (map == null) continue;
                if (!byMap.TryGetValue(map, out var facts))
                    byMap[map] = facts = ProseScene.GatherColony(map);

                var state = GoalMath.Evaluate(entry.Kind, entry.Target, entry.ExpiryTick, now,
                                              facts, entry.State);
                if (!GoalStore.Resolve(entry, state, now)) continue;

                Reward(pawn, entry);
                Announce(pawn, entry);
            }
            catch (Exception e)
            {
                // One bad entry must not stop the pass; an unevaluated goal is the
                // failure mode this method exists to prevent.
                Logger.WarningOnce($"goal-eval-{entry?.PawnId}", $"Goal evaluation failed: {e.Message}");
            }
        }
    }

    /// <summary>
    /// The mood. #29's exit criterion is "at least one mechanic writes to the
    /// simulation" and this is it — the first thing RimTalk has ever done that the
    /// game itself can see.
    ///
    /// Tiny on purpose, and asymmetric: a goal met is a small event, a goal missed is
    /// mostly just disappointing. #28's two hazards are taxing the player for their
    /// own strategy and a permanent mood floor nobody asked for; a five-day memory
    /// with a stack limit of one answers both.
    /// </summary>
    static void Reward(Pawn pawn, GoalEntry entry)
    {
        if (entry.Rewarded) return;
        entry.Rewarded = true;

        var def = entry.State switch
        {
            GoalState.Met     => DefDatabase<ThoughtDef>.GetNamedSilentFail("RimTalk_GoalMet"),
            GoalState.Expired => DefDatabase<ThoughtDef>.GetNamedSilentFail("RimTalk_GoalMissed"),
            _ => null,
        };
        // Silent-fail rather than GetNamed: a missing def is a mod-load problem and
        // must not throw inside a nightly loop over every colonist.
        if (def == null) return;
        pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(def);
    }

    /// <summary>
    /// A met goal is worth a letter and a missed one is not. Announcing both is how a
    /// good idea becomes letter spam, which #36 already called the fastest route to
    /// uninstall.
    /// </summary>
    static void Announce(Pawn pawn, GoalEntry entry)
    {
        if (entry.State != GoalState.Met) return;
        if (!Settings.Get().Context.NarrativeLetters) return;
        if (Find.LetterStack == null) return;

        Find.LetterStack.ReceiveLetter(
            $"{pawn.LabelShort} got what they wanted",
            $"{entry.Statement}\n\n{pawn.LabelShort} has been after this for " +
            $"{Narrative.NarrativeMath.Elapsed(entry.ResolvedTick - entry.SetTick)}.",
            LetterDefOf.PositiveEvent, new LookTargets(pawn));
    }
}
