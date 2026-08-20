using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using RimTalk.Util;
using Verse;

namespace RimTalk.Data;

public static class TalkHistory
{
    private static readonly ConcurrentDictionary<int, List<(Role role, string message)>> MessageHistory = new();
    private static readonly ConcurrentDictionary<Guid, int> SpokenTickCache = new() { [Guid.Empty] = 0 };
    private static readonly ConcurrentBag<Guid> IgnoredCache = [];
    
    // Add a new talk with the current game tick
    public static void AddSpoken(Guid id)
    {
        SpokenTickCache.TryAdd(id, GenTicks.TicksGame);
    }
    
    public static void AddIgnored(Guid id)
    {
        IgnoredCache.Add(id);
    }

    public static int GetSpokenTick(Guid id)
    {
        return SpokenTickCache.TryGetValue(id, out var tick) ? tick : -1;
    }
    
    public static bool IsTalkIgnored(Guid id)
    {
        return IgnoredCache.Contains(id);
    }

    public static void AddMessageHistory(Pawn pawn, string request, string response)
    {
        var messages = MessageHistory.GetOrAdd(pawn.thingIDNumber, _ => []);

        lock (messages)
        {
            messages.Add((Role.User, request));
            messages.Add((Role.AI, response));
            EnsureMessageLimit(messages);
        }
    }

    /// <summary>
    /// What to send, already fitted to the token budget.
    ///
    /// rim-universe #9. The cap used to be a message COUNT applied on write, so a long
    /// generated reply cost the same as a short one and whatever fell off the end was
    /// gone. Now the store keeps everything and the budget is applied on read, with the
    /// overflow collapsed rather than deleted.
    /// </summary>
    public static List<(Role role, string message)> GetMessageHistory(Pawn pawn, bool simplified = false)
    {
        if (pawn == null || !MessageHistory.TryGetValue(pawn.thingIDNumber, out var history))
            return [];

        List<(Role, string)> cleaned;
        lock (history)
        {
            cleaned = new List<(Role, string)>();
            foreach (var msg in history)
            {
                var content = msg.message;
                if (simplified)
                {
                    if (msg.role == Role.AI)
                        content = BuildAssistantHistoryText(content);

                    content = CleanHistoryText(content);
                }

                if (!string.IsNullOrWhiteSpace(content))
                    cleaned.Add((msg.role, content));
            }
        }

        var fitted = TalkMemory.Fit(cleaned, TokenBudget());
        return fitted.AsMessages().Select(m => (m.Item1, m.Item2)).ToList();
    }

    /// <summary>
    /// Derived from the exchange count the player set, so one slider still means what
    /// it says — but spent in tokens, which is the thing that actually costs money.
    /// An exchange is an envelope plus a reply: call it 250 tokens.
    /// </summary>
    public const int TokensPerExchange = 250;

    static int TokenBudget() =>
        Math.Max(1, Settings.Get().Context.ConversationHistoryCount) * TokensPerExchange;

    private static void EnsureMessageLimit(List<(Role role, string message)> messages)
    {
        // First, ensure alternating pattern by removing consecutive duplicates from the end
        for (int i = messages.Count - 1; i > 0; i--)
        {
            if (messages[i].role == messages[i - 1].role)
            {
                // Remove the earlier message of the consecutive pair
                messages.RemoveAt(i - 1);
            }
        }

        // The store keeps everything the nightly collapse has not yet compressed; the
        // budget is applied on read by TalkMemory. This is only a runaway guard, and it
        // is deliberately far above any window a player would set. rim-universe #9:
        // the old line here was `messages.Count > ConversationHistoryCount * 2`, which
        // at the default of 1 meant the model saw exactly one prior turn and everything
        // else was destroyed on write.
        const int hardCeiling = 200;
        while (messages.Count > hardCeiling)
        {
            messages.RemoveAt(0);
        }
    }

    private static string CleanHistoryText(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        var cleaned = CommonUtil.StripFormattingTags(text);
        return cleaned.Replace("\r\n", " ").Replace("\n", " ").Replace("\r", " ").Trim();
    }

    private static string BuildAssistantHistoryText(string response)
    {
        if (string.IsNullOrWhiteSpace(response)) return "";

        var lines = new List<string>();
        var trimmed = response.Trim();
        if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
        {
            try
            {
                var parsed = JsonUtil.DeserializeFromJson<List<TalkResponse>>(trimmed);
                if (parsed != null)
                {
                    foreach (var r in parsed)
                    {
                        if (r == null) continue;
                        var text = r.Text;
                        if (string.IsNullOrWhiteSpace(text)) continue;
                        var name = r.Name;
                        lines.Add(string.IsNullOrWhiteSpace(name) ? text : $"{name}: {text}");
                    }
                }
            }
            catch
            {
                lines.Clear();
            }
        }

        if (lines.Count == 0)
        {
            lines.Add(response);
        }

        return string.Join("\n", lines);
    }

    /// <summary>
    /// Wipes everything. Called when a game is loaded, before <see cref="Restore"/>
    /// puts the saved history back, and by the debug window on demand.
    /// </summary>
    public static void Clear()
    {
        MessageHistory.Clear();
        // clearing spokenCache may block child talks waiting to display
    }

    /// <summary>
    /// The nightly pass. rim-universe #9: this used to be Clear() — "clear LLM history
    /// daily to prevent repetitive/degraded dialogue" — which is a real problem solved
    /// by amnesia. Collapse instead: the envelopes go, the speech stays as one line,
    /// and a pawn can still refer to yesterday.
    /// </summary>
    public static void CollapseAll()
    {
        foreach (var id in MessageHistory.Keys.ToList())
        {
            if (!MessageHistory.TryGetValue(id, out var messages)) continue;
            lock (messages)
            {
                var digest = TalkMemory.Digest(messages);
                messages.Clear();
                if (digest != null) messages.Add((Role.AI, digest));
            }
        }
    }

    /// <summary>
    /// Everything, flattened for the save. Bounded: a decades-long colony must not grow
    /// a save file without limit, and the oldest exchanges have already been collapsed
    /// by the nightly pass.
    /// </summary>
    public const int MaxSavedTurns = 400;

    public static List<ChatTurn> Snapshot()
    {
        var all = new List<ChatTurn>();
        foreach (var pair in MessageHistory)
        {
            var messages = pair.Value;
            if (messages == null) continue;
            lock (messages)
                foreach (var (role, message) in messages)
                    if (!string.IsNullOrWhiteSpace(message))
                        all.Add(new ChatTurn { PawnId = pair.Key, Role = role, Text = message });
        }

        // Trim the oldest first, and only whole pawns' worth is not worth the
        // complexity — a flat cap keeps the newest and that is what a prompt uses.
        if (all.Count > MaxSavedTurns) all.RemoveRange(0, all.Count - MaxSavedTurns);
        return all;
    }

    public static void Restore(List<ChatTurn> turns)
    {
        MessageHistory.Clear();
        if (turns == null) return;

        foreach (var turn in turns)
        {
            if (turn == null || string.IsNullOrWhiteSpace(turn.Text)) continue;
            MessageHistory.GetOrAdd(turn.PawnId, _ => []).Add((turn.Role, turn.Text));
        }
    }
}
