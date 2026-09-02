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
    /// <summary>
    /// This was a ConcurrentBag, which is not a set: it has no hash index, so
    /// Contains walks the whole thing. IsTalkIgnored is called on the DISPLAY path
    /// for every generated talk, and the bag grows by one Guid every time a user
    /// request pre-empts a queue — which is every player-initiated conversation. A
    /// long session turns the interaction the player uses most into a linear scan
    /// over thousands of entries.
    ///
    /// A dictionary makes it O(1). <see cref="Prune"/> makes it bounded, which the
    /// bag never was: TalkHistory.Clear() only ever cleared MessageHistory.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, int> IgnoredTickCache = new();

    /// <summary>
    /// Two in-game days. A talk that could have been displayed two days ago cannot be
    /// displayed now — the response objects it refers to are long gone — so anything
    /// older is dead weight in both caches.
    /// </summary>
    public const int RememberTicks = 120000;   // GenDate.TicksPerDay * 2

    /// <summary>Only walk the caches when one of them has grown enough to be worth it.</summary>
    public const int PruneAbove = 512;

    // Add a new talk with the current game tick
    public static void AddSpoken(Guid id)
    {
        SpokenTickCache.TryAdd(id, GenTicks.TicksGame);
        Prune();
    }

    public static void AddIgnored(Guid id)
    {
        IgnoredTickCache.TryAdd(id, GenTicks.TicksGame);
        Prune();
    }

    public static int GetSpokenTick(Guid id)
    {
        return SpokenTickCache.TryGetValue(id, out var tick) ? tick : -1;
    }

    public static bool IsTalkIgnored(Guid id)
    {
        return IgnoredTickCache.ContainsKey(id);
    }

    /// <summary>
    /// Drop entries older than <see cref="RememberTicks"/>. Both caches held one
    /// entry per talk for the entire session and neither was ever trimmed.
    /// Guid.Empty is seeded at tick 0 and must survive — it is the sentinel for "no
    /// parent talk", and pruning it makes every root talk look unspoken.
    /// </summary>
    static void Prune()
    {
        var now = GenTicks.TicksGame;
        if (SpokenTickCache.Count <= PruneAbove && IgnoredTickCache.Count <= PruneAbove) return;

        foreach (var pair in SpokenTickCache)
            if (pair.Key != Guid.Empty && TalkCacheMath.Expired(pair.Value, now, RememberTicks))
                SpokenTickCache.TryRemove(pair.Key, out _);

        foreach (var pair in IgnoredTickCache)
            if (TalkCacheMath.Expired(pair.Value, now, RememberTicks))
                IgnoredTickCache.TryRemove(pair.Key, out _);
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

    public static List<(Role role, string message)> GetMessageHistory(Pawn pawn, bool simplified = false)
    {
        if (!MessageHistory.TryGetValue(pawn.thingIDNumber, out var history))
            return [];

        lock (history)
        {
            var result = new List<(Role role, string message)>();
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
                    result.Add((msg.role, content));
            }
            return result;
        }
    }

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

        // Then, enforce the maximum message limit by removing the oldest messages
        int maxMessages = Settings.Get().Context.ConversationHistoryCount;
        while (messages.Count > maxMessages * 2)
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

    public static void Clear()
    {
        MessageHistory.Clear();
        // clearing spokenCache may block child talks waiting to display
    }
}
