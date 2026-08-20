using HarmonyLib;
using RimTalk.Data;
using RimTalk.Service;
using RimTalk.Source.Data;
using RimTalk.Util;
using RimWorld;
using Verse;

namespace RimTalk.Patch;

[HarmonyPatch(typeof(TickManager), nameof(TickManager.DoSingleTick))]
internal static class TickManagerPatch
{
    private const double DisplayInterval = 0.5; // Display every half second
    private const double DebugStatUpdateInterval = 1;
    private const int UpdateCacheInterval = 5; // 5 seconds
    private static double TalkInterval => Settings.Get().TalkInterval;
    private static bool _noApiKeyMessageShown;
    private static bool _initialCacheRefresh;
    private static bool _chatHistoryCleared;
    private static int _lastTalkEndTick;

    public static void Postfix()
    {
        Counter.Tick++;

        if (IsNow(DebugStatUpdateInterval))
        {
            Stats.Update();
        }

        if (!Settings.Get().IsEnabled || Find.CurrentMap == null)
        {
            return;
        }

        if (!_initialCacheRefresh || IsNow(UpdateCacheInterval))
        {
            Cache.Refresh();
            _initialCacheRefresh = true;
        }
        
        if (IsNow(1))
        {
            // Clear LLM history daily to prevent repetitive/degraded dialogue
            int currentHour = CommonUtil.GetInGameHour(Find.TickManager.TicksAbs, Find.WorldGrid.LongLatOf(Find.CurrentMap.Tile));
            if (currentHour == 0 && !_chatHistoryCleared)
            {
                TalkHistory.Clear();
                _chatHistoryCleared = true;
            }
            else if (currentHour != 0)
            {
                _chatHistoryCleared = false;
            }
        }

        if (!_noApiKeyMessageShown && Settings.Get().GetActiveConfig() == null)
        {
            Messages.Message("RimTalk.TickManager.ApiKeyMissing".Translate(), MessageTypeDefOf.NegativeEvent,
                false);
            _noApiKeyMessageShown = true;
        }

        if (IsNow(DisplayInterval))
        {
            CustomDialogueService.Tick();
            TalkService.DisplayTalk();
        }

        if (IsNow(1))
        {
            // User-initiated talks are checked every second
            while (UserRequestPool.GetNextUserRequest() is { } pawn)
            {
                var pawnState = Cache.Get(pawn);
                if (pawnState == null)
                {
                    UserRequestPool.Remove(pawn);
                    continue;
                }
                var request = pawnState.GetNextTalkRequest();
                
                if (request == null)
                {
                    UserRequestPool.Remove(pawn);
                    continue;
                }

                if (!request.TalkType.IsFromUser()) break;

                if (TalkService.GenerateTalk(request))
                    UserRequestPool.Remove(pawn);
                return;
            }
        }

        if (AIService.IsBusy())
        {
            _lastTalkEndTick = GenTicks.TicksGame;
            return;
        }

        int intervalTicks = CommonUtil.GetTicksForDuration(TalkInterval);
        if (intervalTicks > 0 && GenTicks.TicksGame - _lastTalkEndTick >= intervalTicks)
        {
            // Select a pawn based on the current iteration strategy
            Pawn selectedPawn = PawnSelector.SelectNextAvailablePawn();

            if (selectedPawn != null)
            {
                // Own queue before the pool. rim-universe #40: the pool holds map-wide
                // events whose Initiator is overwritten with whoever was selected, so a
                // pool line is something this pawn merely witnessed. Answering a
                // witnessed event before the remark you just made yourself is what put
                // the courtship in the log after the line about the sick knot.
                var pawnState = Cache.Get(selectedPawn);
                var ownRequest = pawnState?.GetNextTalkRequest();

                var talkGenerated = ownRequest != null && TalkService.GenerateTalk(ownRequest);

                if (!talkGenerated)
                    talkGenerated = TryGenerateTalkFromPool(selectedPawn);

                // 3. Fallback: generate based on current context if nothing else worked.
                //
                // This used to pass a null prompt, so the model had nothing but the
                // profile and the environment envelope -- which is why untopiced
                // dialogue is all weather, meals and bedrolls. A conversation with no
                // topic cannot have a SMALL topic, so there is nothing for the
                // situation to collide with either (#34, #35). rim-universe #38.
                //
                // Seeded from what the pawn is actually doing. Thin, but real, and it
                // is replaced by a Need (#30) once those exist.
                if (!talkGenerated)
                {
                    TalkRequest talkRequest = new TalkRequest(FallbackTopic(selectedPawn), selectedPawn);
                    TalkService.GenerateTalk(talkRequest);
                }
            }
            
            _lastTalkEndTick = GenTicks.TicksGame;
        }
    }

    /// <summary>
    /// Something for an otherwise topicless conversation to be about. rim-universe #38.
    /// Deliberately modest: the pawn's current activity is concrete, already computed,
    /// and is not already in the profile block the way mood and thoughts are.
    /// </summary>
    private static string FallbackTopic(Pawn pawn)
    {
        var activity = pawn?.GetActivity();
        // A whole clause, not a fragment. The first version emitted "while hauling
        // steel" as a standalone topic line -- a dangling subordinate clause with
        // nothing to attach to, which the model then invented a referent for.
        return string.IsNullOrWhiteSpace(activity)
            ? null
            : $"{pawn.LabelShort} is {activity.StripTags().ToLower()}";
    }

    private static bool TryGenerateTalkFromPool(Pawn pawn)
    {
        // If the pawn is a free colonist not in danger and the pool has requests
        if (!pawn.IsFreeNonSlaveColonist || pawn.IsQuestLodger() || TalkRequestPool.IsEmpty || pawn.IsInDanger(true)) return false;
        var request = TalkRequestPool.GetRequestFromPool(pawn);
        return request != null && TalkService.GenerateTalk(request);
    }

    private static bool IsNow(double interval)
    {
        int ticksForDuration = CommonUtil.GetTicksForDuration(interval);
        if (ticksForDuration == 0) return false;
        return Counter.Tick % ticksForDuration == 0;
    }

    public static void Reset()
    {
        _noApiKeyMessageShown = false;
        _initialCacheRefresh = false;
        _lastTalkEndTick = GenTicks.TicksGame;
    }
}
