using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RimTalk.Data;
using RimTalk.Prompt;
using RimTalk.Source.Data;
using RimTalk.UI;
using RimTalk.Util;
using RimWorld;
using Verse;
using Cache = RimTalk.Data.Cache;
using Logger = RimTalk.Util.Logger;

namespace RimTalk.Service;

/// <summary>
/// Core service for generating and managing AI-driven conversations between pawns.
/// </summary>
public static class TalkService
{
    /// <summary>
    /// Initiates the process of generating a conversation. It performs initial checks and then
    /// starts a background task to handle the actual AI communication.
    /// </summary>
    public static bool GenerateTalk(TalkRequest talkRequest)
    {
        // Guard clauses to prevent generation when the feature is disabled or the AI service is busy.
        var settings = Settings.Get();
        if (!settings.IsEnabled || !CommonUtil.ShouldAiBeActiveOnSpeed()) return false;
        if (settings.GetActiveConfig() == null) return false;
        if (AIService.IsBusy()) return false;

        if (!SleepDialogueTracker.TryRefreshRequest(talkRequest)) return false;

        PawnState pawn1 = Cache.Get(talkRequest.Initiator);
        if (!talkRequest.TalkType.IsFromUser() && (pawn1 == null || !pawn1.CanGenerateTalk())) return false;

        if (!settings.AllowSimultaneousConversations && AnyPawnHasPendingResponses()) return false;

        // Ensure the recipient is valid and capable of talking.
        PawnState pawn2 = talkRequest.Recipient != null ? Cache.Get(talkRequest.Recipient) : null;
        if (pawn2 == null || talkRequest.Recipient?.Name == null || !pawn2.CanDisplayTalk())
        {
            talkRequest.Recipient = null;
        }

        // Recipient may have just been nulled above. IsPlayer() is an extension, so it
        // does not throw on null -- and when no player pawn exists it returns true for
        // null, inserting a null that NREs downstream in GetPawnStatusFull. fix.
        bool isPlayerAnnouncement = talkRequest.IsAnnouncement && talkRequest.Recipient != null && talkRequest.Recipient.IsPlayer();
        Pawn mainPawn = isPlayerAnnouncement ? talkRequest.Recipient : talkRequest.Initiator;

        List<Pawn> nearbyPawns = PawnSelector.GetAllNearByPawns(talkRequest.Initiator, isAnnouncement: talkRequest.IsAnnouncement);
        if (isPlayerAnnouncement) nearbyPawns.Insert(0, talkRequest.Initiator);
        else if (talkRequest.Recipient != null && talkRequest.Recipient.IsPlayer()) nearbyPawns.Insert(0, talkRequest.Recipient);

        var (status, isInDanger) = mainPawn.GetPawnStatusFull(nearbyPawns, talkRequest.IsAnnouncement);

        // Avoid spamming generations if the pawn's status hasn't changed recently.
        if (!talkRequest.TalkType.IsFromUser() && talkRequest.TalkType != TalkType.Interaction && status == pawn1.LastStatus && pawn1.RejectCount < 2)
        {
            pawn1.RejectCount++;
            return false;
        }

        if (!talkRequest.TalkType.IsFromUser() && isInDanger) talkRequest.TalkType = TalkType.Urgent;

        pawn1.RejectCount = 0;
        pawn1.LastStatus = status;

        // Select the most relevant pawns for the conversation context.
        List<Pawn> pawns = new List<Pawn> { mainPawn, isPlayerAnnouncement ? null : talkRequest.Recipient }
            .Where(p => p != null)
            .Concat(nearbyPawns.Where(p =>
            {
                var pawnState = Cache.Get(p);
                pawnState?.DrainIncomingTalkResponses();
                return pawnState != null && pawnState.CanDisplayTalk() &&
                       (talkRequest.IsAnnouncement || pawnState.TalkResponses.Empty());
            }))
            .Distinct()
            .Take(talkRequest.IsAnnouncement ? Math.Max(settings.Context.MaxPawnContextCount, 8) : settings.Context.MaxPawnContextCount)
            .ToList();

        if (talkRequest.IsAnnouncement)
            foreach (var p in pawns.Where(p => p != null && !p.IsPlayer()))
                Cache.Get(p)?.IgnoreAllTalkResponses([TalkType.Urgent, TalkType.User, TalkType.Announcement]);

        if (talkRequest.TalkType == TalkType.Sleep)
            talkRequest.IsMonologue = pawns.Count == 1;
        else if (pawns.Count == 1)
            talkRequest.IsMonologue = true;

        if (!settings.AllowMonologue && talkRequest.IsMonologue && !talkRequest.TalkType.IsFromUser())
            return false;

        // Store dialogue participants for disambiguating duplicate pawn names during async streaming response processing
        talkRequest.Participants = pawns;

        // Delegate prompt assembly to PromptManager (Handles Simple/Advanced modes and fallbacks)
        talkRequest.PromptMessages = PromptManager.Instance.BuildMessages(talkRequest, pawns, status);

        // Update prompt with the actual rendered content (important for Advanced Mode history)
        var extracted = PromptManager.ExtractUserPrompt(talkRequest.PromptMessages);
        if (!string.IsNullOrEmpty(extracted))
        {
            talkRequest.Prompt = extracted;
        }

        // Offload the AI request and processing to a background thread to avoid blocking the game's main thread.
        Task.Run(() => GenerateAndProcessTalkAsync(talkRequest));

        pawn1.MarkRequestSpoken(talkRequest);

        return true;
    }

    /// <summary>
    /// Handles the asynchronous AI streaming and processes the responses.
    /// </summary>
    private static async Task GenerateAndProcessTalkAsync(TalkRequest talkRequest)
    {
        var initiator = talkRequest.Initiator;
        try
        {
            Cache.Get(initiator).IsGeneratingTalk = true;

            var receivedResponses = new List<TalkResponse>();

            // Call the streaming chat service. The callback is executed as each piece of dialogue is parsed.
            await AIService.ChatStreaming(talkRequest, talkResponse =>
                {
                    Logger.Debug($"Streamed: {talkResponse}");

                    // Resolve target pawn from response name (handling aliases) and revert to native LabelShort for in-game talk bubbles
                    PawnState pawnState = talkRequest.ResolvePawnState(talkResponse.Name);
                    if (pawnState == null) return;
                    talkResponse.Name = pawnState.Pawn.LabelShort;

                    // Link replies to the previous message in the conversation.
                    if (receivedResponses.Any())
                    {
                        talkResponse.ParentTalkId = receivedResponses.Last().Id;
                    }

                    // Who said it, so the NEXT line can be addressed to them.
                    TalkHistory.RecordSpeaker(talkResponse.Id, talkResponse.Name);

                    receivedResponses.Add(talkResponse);

                    // Hand off to the main thread for display later; PawnState.TalkResponses itself must only ever be touched from the main thread.
                    pawnState.QueueIncomingResponse(talkResponse);
                }
            );

            // Once the stream is complete, save the full conversation to history.
            AddResponsesToHistory(receivedResponses, talkRequest.Prompt, talkRequest);
        }
        catch (OperationCanceledException)
        {
            Logger.Debug("Dialogue generation canceled.");
        }
        catch (Exception ex)
        {
            Logger.Error(ex.StackTrace);
        }
        finally
        {
            var pState = Cache.Get(initiator);
            if (pState != null) pState.IsGeneratingTalk = false;
        }
    }

    /// <summary>
    /// Serializes the generated responses and adds them to the message history for all involved pawns.
    /// </summary>
    private static void AddResponsesToHistory(List<TalkResponse> responses, string prompt, TalkRequest talkRequest)
    {
        if (!responses.Any()) return;
        string serializedResponses = JsonUtil.SerializeToJson(responses);
        var uniquePawns = talkRequest.Participants ?? [talkRequest.Initiator];

        for (int i = 0; i < uniquePawns.Count; i++)
        {
            var pawn = uniquePawns[i];
            if (pawn != null)
            {
                TalkHistory.AddMessageHistory(pawn, prompt, serializedResponses);
            }
        }
    }

    /// <summary>
    /// Iterates through all pawns on each game tick to display any queued talks.
    /// </summary>
    public static void DisplayTalk()
    {
        // Drain all pawns upfront so every pawn has a consistent view of TalkResponses for this tick cycle.
        foreach (Pawn pawn in Cache.Keys)
        {
            Cache.Get(pawn)?.DrainIncomingTalkResponses();
        }

        foreach (Pawn pawn in Cache.Keys)
        {
            PawnState pawnState = Cache.Get(pawn);
            if (pawnState == null) continue;

            if (pawnState.TalkResponses.Empty()) continue;

            var talk = pawnState.TalkResponses.First();
            if (talk == null)
            {
                pawnState.TalkResponses.RemoveAt(0);
                continue;
            }

            // Skip this talk if its parent was ignored or the pawn is currently unable to speak.
            if (TalkHistory.IsTalkIgnored(talk.ParentTalkId) || !pawnState.CanDisplayTalk())
            {
                pawnState.IgnoreTalkResponse();
                continue;
            }

            int replyInterval = Settings.Get().ReplyInterval;
            if (pawn.IsInDanger() || talk.TalkType == TalkType.Announcement)
            {
                replyInterval = Math.Min(replyInterval, 2);
                if (pawn.IsInDanger())
                    pawnState.IgnoreAllTalkResponses([TalkType.Urgent, TalkType.User, TalkType.Announcement]);
            }

            // Enforce a delay for replies to make conversations feel more natural.
            int parentTalkTick = TalkHistory.GetSpokenTick(talk.ParentTalkId);
            if (parentTalkTick == -1 || !CommonUtil.HasPassed(parentTalkTick, replyInterval)) continue;

            CreateInteraction(pawn, talk);

            break; // Display only one talk per tick to prevent overwhelming the screen.
        }
    }

    /// <summary>
    /// Retrieves the text for a pawn's current talk. Called by the game's UI system.
    /// </summary>
    public static string GetTalk(Pawn pawn)
    {
        PawnState pawnState = Cache.Get(pawn);
        if (pawnState == null) return null;

        pawnState.DrainIncomingTalkResponses();
        TalkResponse talkResponse = ConsumeTalk(pawnState);
        pawnState.LastTalkTick = GenTicks.TicksGame;

        return talkResponse.Text;
    }

    /// <summary>
    /// Calls AI service directly for debug purpose.
    /// </summary>
    public static void GenerateTalkDebug(TalkRequest talkRequest)
    {
        Task.Run(() => GenerateAndProcessTalkAsync(talkRequest));
    }

    /// <summary>
    /// Dequeues a talk and updates its history as either spoken or ignored.
    /// </summary>
    private static TalkResponse ConsumeTalk(PawnState pawnState)
    {
        // Failsafe check
        if (pawnState.TalkResponses.Empty())
            return new TalkResponse(TalkType.Other, null!, "");

        var talkResponse = pawnState.TalkResponses.First();
        pawnState.TalkResponses.Remove(talkResponse);
        TalkHistory.AddSpoken(talkResponse.Id);
        var apiLog = ApiHistory.GetApiLog(talkResponse.Id);
        if (apiLog != null)
            apiLog.SpokenTick = GenTicks.TicksGame;

        Overlay.NotifyLogUpdated();
        return talkResponse;
    }


    /// <summary>
    /// Who a generated line is addressed to. seen in play:
    /// four lines of an obvious back-and-forth, every one of them logged as a
    /// monologue.
    ///
    /// It was `talk.GetTarget() ?? pawn`. GetTarget reads the model's optional
    /// "target" field, and the prompt only asks for it "if social interaction
    /// occurs" — so 61% of lines in a real session carry none, and the fallback made
    /// every one of those speakers their own recipient. That is the monologue signature, and it
    /// silently disabled ApplySocialEffects too, which is guarded on
    /// `pawn != recipient`.
    ///
    /// Three sources, most reliable first:
    ///
    ///  1. What the model said, when it said anything.
    ///  2. The speaker of the line this one is REPLYING to. Not a guess — the
    ///     ParentTalkId link is built as the stream arrives.
    ///  3. The only other person in the scene, when there is exactly one. Two people
    ///     in a room are talking to each other; three might not be, so a crowd falls
    ///     through rather than being assigned a recipient at random.
    ///
    /// Falls back to the speaker, which is a real monologue: a lone colonist muttering
    /// is a thing this mod deliberately does.
    /// </summary>
    private static Pawn RecipientOf(Pawn pawn, TalkResponse talk)
    {
        var stated = talk.GetTarget();
        if (stated != null && stated != pawn) return stated;

        var parentSpeaker = TalkHistory.GetSpeaker(talk.ParentTalkId);
        if (parentSpeaker != null)
        {
            var replyingTo = Cache.GetByName(parentSpeaker)?.Pawn;
            if (replyingTo != null && replyingTo != pawn) return replyingTo;
        }

        var others = PawnSelector.GetAllNearByPawns(pawn)
            .Where(p => p != null && p != pawn && !p.Dead && (p.RaceProps?.Humanlike ?? false))
            .Take(2)
            .ToList();

        return others.Count == 1 ? others[0] : pawn;
    }

    /// <summary>
    /// Whether a social memory ABOUT this pawn will still resolve after a save.
    ///
    /// Thought_MemorySocial stores its subject by reference. A raider talks, a
    /// colonist gains a memory pointing at them, the raider dies and their corpse
    /// burns, and the save carries a reference to nothing — which RimWorld reports as
    /// "referenced (xml node name: otherPawn) but is not deep-saved. This will cause
    /// errors during loading", sixteen times in one session and forty-six in the one
    /// before.
    ///
    /// The player's own people are the ones the save keeps and the only ones a lasting
    /// opinion is worth holding. A raider being memorable is not worth a corrupt load,
    /// and RimTalk having a view on somebody already over the horizon buys nothing.
    ///
    /// Not a claim about who may TALK — enemies and other factions still speak, and
    /// their lines still appear. Only about who gets a permanent memory written about
    /// them.
    /// </summary>
    private static bool Persists(Pawn p) =>
        p != null
        && (p.RaceProps?.Humanlike ?? false)
        && (p.IsFreeColonist || p.IsSlaveOfColony || p.IsPrisonerOfColony);

    private static void CreateInteraction(Pawn pawn, TalkResponse talk)
    {
        // Create the interaction log entry, which triggers the display of the talk bubble in-game.
        InteractionDef intDef = DefDatabase<InteractionDef>.GetNamed("RimTalkInteraction");
        var recipient = RecipientOf(pawn, talk);
        var playLogEntryInteraction = new PlayLogEntry_RimTalkInteraction(intDef, pawn, recipient, null);

        if (playLogEntryInteraction.CachedString.NullOrEmpty())
            return;

        Find.PlayLog.Add(playLogEntryInteraction);

        if (Settings.Get().ApplyMoodAndSocialEffects && pawn != recipient && Persists(recipient) && Persists(pawn))
        {
            var interactionType = talk.GetInteractionType();
            var memory = interactionType.GetThoughtDef();
            if (memory != null)
            {
                recipient.needs?.mood?.thoughts?.memories?.TryGainMemory(memory, pawn);
                if (interactionType is InteractionType.Chat)
                {
                    pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(memory, recipient);
                }
            }
        }
    }

    private static bool AnyPawnHasPendingResponses()
    {
        return Cache.GetAll().Any(pawnState =>
        {
            pawnState.DrainIncomingTalkResponses();
            return pawnState.TalkResponses.Count > 0;
        });
    }
}
