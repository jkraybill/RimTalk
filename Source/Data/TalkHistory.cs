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
    /// Who said each generated line.
    ///
    /// Needed because a reply's RECIPIENT is the speaker of the line it answers, and
    /// that was the only fact missing when CreateInteraction had to decide who a line
    /// was addressed to. It had been falling back to the speaker themselves, which
    /// logs every ordinary two-person exchange as a monologue.
    ///
    /// Names rather than Pawns: this is written from the streaming callback, off the
    /// main thread, and holding pawn references there is how the rest of this file
    /// learned not to.
    /// </summary>
    private static readonly ConcurrentDictionary<Guid, string> SpeakerCache = new();

    public static void RecordSpeaker(Guid id, string name)
    {
        if (id == Guid.Empty || string.IsNullOrWhiteSpace(name)) return;
        SpeakerCache[id] = name;
    }

    public static string GetSpeaker(Guid id) =>
        id != Guid.Empty && SpeakerCache.TryGetValue(id, out var name) ? name : null;
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
        SpeakerCache.Clear();
        MessageHistory.Clear();
        // clearing spokenCache may block child talks waiting to display
    }
}
