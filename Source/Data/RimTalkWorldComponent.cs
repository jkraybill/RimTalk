using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimTalk.Util;
using RimWorld.Planet;
using Verse;

namespace RimTalk.Data;

public class RimTalkWorldComponent(World world) : WorldComponent(world)
{
    private const int MaxLogEntries = 1000;

    public Dictionary<string, string> RimTalkInteractionTexts = new();
    private Queue<string> _keyInsertionOrder = new();

    /// <summary>
    /// Harvested colony history. rim-universe #21 / roundtable S166.
    /// Bounded by NarrativeStore.MaxEvents; a decades-long save must not grow
    /// without limit. Lives here because this component is already scribed and
    /// already save-tested.
    /// </summary>
    public List<Narrative.NarrativeEvent> NarrativeEvents = new();

    /// <summary>
    /// The last few lines each pawn said, keyed by thingIDNumber, newline-joined.
    /// rim-universe #41. Newline-joined rather than a nested list because Scribe has
    /// no LookMode for Dictionary&lt;int, List&lt;string&gt;&gt; and a value type here
    /// costs nothing; RecentLines strips newlines on the way in.
    /// </summary>
    public Dictionary<int, string> RecentSpokenLines = new();

    /// <summary>
    /// Conversation history, flattened. rim-universe #9. The live store stays a
    /// ConcurrentDictionary in TalkHistory because it is written from the thread that
    /// finishes a streaming call; this is only the persistence medium, filled on save
    /// and drained on load.
    /// </summary>
    public List<ChatTurn> ChatTurns = new();

    /// <summary>
    /// What each person wrote the day they came to here. rim-universe #37. Deep, not
    /// Reference: these are meant to outlive the people in them.
    /// </summary>
    public List<Narrative.ArrivalEntry> ArrivalEntries = new();

    /// <summary>Back-pocket conversation topics, one set per pawn. rim-universe #44.</summary>
    public List<Narrative.TopicEntry> TopicEntries = new();

    /// <summary>
    /// What the colony has been doing, already worded. rim-universe #30's delta, and
    /// the generalised harvest #21 and #22 both need. Bounded by Chronicle.MaxEntries.
    /// Separate from NarrativeEvents so a good hunting week cannot evict a death.
    /// </summary>
    public List<Narrative.ChronicleEntry> ChronicleEntries = new();

    /// <summary>
    /// Conversation history keyed by pair. rim-universe #30. Same split as ChatTurns:
    /// the live store is a ConcurrentDictionary in PairStore because it is written
    /// from the thread that finishes a streaming call, and this is only the medium.
    /// </summary>
    public List<Narrative.PairRecord> PairRecords = new();

    /// <summary>
    /// What each colonist wants to see happen here next, and what they have already
    /// got. rim-universe #28. Resolved entries stay: they are what the cooldown reads
    /// and the only record that anything was ever achieved.
    /// </summary>
    public List<Goals.GoalEntry> GoalEntries = new();

    public override void ExposeData()
    {
        base.ExposeData();

        try 
        {
            Scribe_Collections.Look(ref RimTalkInteractionTexts, "rimtalkInteractionTexts", LookMode.Value, LookMode.Value);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load interaction texts. Resetting data to prevent save corruption. Error: {ex.Message}");
            RimTalkInteractionTexts = new Dictionary<string, string>();
            _keyInsertionOrder = new Queue<string>();
        }

        List<string> keyOrderList = null;
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            keyOrderList = _keyInsertionOrder.ToList();
        }

        Scribe_Collections.Look(ref keyOrderList, "rimtalkKeyOrder");

        // Deep, not Reference: these deliberately outlive the pawns they describe.
        try
        {
            Scribe_Collections.Look(ref NarrativeEvents, "rimtalkNarrativeEvents", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load narrative events. Resetting to prevent save corruption. Error: {ex.Message}");
            NarrativeEvents = new List<Narrative.NarrativeEvent>();
        }
        NarrativeEvents ??= new List<Narrative.NarrativeEvent>();

        // Filled from the live store on the way out. Doing it here rather than keeping
        // the component as the working structure keeps Scribe off the hot path, which
        // is written from a background thread.
        if (Scribe.mode == LoadSaveMode.Saving) ChatTurns = TalkHistory.Snapshot();
        if (Scribe.mode == LoadSaveMode.Saving) PairRecords = Narrative.PairStore.Snapshot();

        try
        {
            Scribe_Collections.Look(ref ChronicleEntries, "rimtalkChronicleEntries", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load the colony chronicle. Resetting to prevent save corruption. Error: {ex.Message}");
            ChronicleEntries = new List<Narrative.ChronicleEntry>();
        }
        ChronicleEntries ??= new List<Narrative.ChronicleEntry>();

        try
        {
            Scribe_Collections.Look(ref PairRecords, "rimtalkPairRecords", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load pair memory. Resetting to prevent save corruption. Error: {ex.Message}");
            PairRecords = new List<Narrative.PairRecord>();
        }
        PairRecords ??= new List<Narrative.PairRecord>();

        try
        {
            Scribe_Collections.Look(ref GoalEntries, "rimtalkGoalEntries", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load goals. Resetting to prevent save corruption. Error: {ex.Message}");
            GoalEntries = new List<Goals.GoalEntry>();
        }
        GoalEntries ??= new List<Goals.GoalEntry>();

        try
        {
            Scribe_Collections.Look(ref TopicEntries, "rimtalkTopicEntries", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load conversation topics. Resetting to prevent save corruption. Error: {ex.Message}");
            TopicEntries = new List<Narrative.TopicEntry>();
        }
        TopicEntries ??= new List<Narrative.TopicEntry>();

        try
        {
            Scribe_Collections.Look(ref ArrivalEntries, "rimtalkArrivalEntries", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load arrival log. Resetting to prevent save corruption. Error: {ex.Message}");
            ArrivalEntries = new List<Narrative.ArrivalEntry>();
        }
        ArrivalEntries ??= new List<Narrative.ArrivalEntry>();

        try
        {
            Scribe_Collections.Look(ref ChatTurns, "rimtalkChatTurns", LookMode.Deep);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load chat history. Resetting to prevent save corruption. Error: {ex.Message}");
            ChatTurns = new List<ChatTurn>();
        }
        ChatTurns ??= new List<ChatTurn>();

        try
        {
            Scribe_Collections.Look(ref RecentSpokenLines, "rimtalkRecentSpokenLines",
                                    LookMode.Value, LookMode.Value);
        }
        catch (System.Exception ex)
        {
            Logger.Error($"Failed to save/load recent spoken lines. Resetting to prevent save corruption. Error: {ex.Message}");
            RecentSpokenLines = new Dictionary<int, string>();
        }
        RecentSpokenLines ??= new Dictionary<int, string>();

        if (Scribe.mode != LoadSaveMode.PostLoadInit) return;
        RimTalkInteractionTexts ??= new Dictionary<string, string>();
        NarrativeEvents ??= new List<Narrative.NarrativeEvent>();
        RecentSpokenLines ??= new Dictionary<int, string>();
        ChatTurns ??= new List<ChatTurn>();
        ArrivalEntries ??= new List<Narrative.ArrivalEntry>();
        TopicEntries ??= new List<Narrative.TopicEntry>();
        ChronicleEntries ??= new List<Narrative.ChronicleEntry>();
        PairRecords ??= new List<Narrative.PairRecord>();
        GoalEntries ??= new List<Goals.GoalEntry>();

        // After RimTalk.cs's TalkHistory.Clear(), which runs on every load. #9: that
        // call is why no colony ever remembered a conversation across a save.
        TalkHistory.Restore(ChatTurns);
        Narrative.PairStore.Restore(PairRecords);
            
        _keyInsertionOrder = keyOrderList != null ? new Queue<string>(keyOrderList) : new Queue<string>();
    }

    public void SetTextFor(LogEntry entry, string text)
    {
        if (entry == null || text == null) return;

        string cleanText = SanitizeXmlString(text);
        string key = entry.GetUniqueLoadID();

        if (RimTalkInteractionTexts.ContainsKey(key))
        {
            RimTalkInteractionTexts[key] = cleanText;
            return;
        }

        while (_keyInsertionOrder.Count >= MaxLogEntries)
        {
            string oldestKey = _keyInsertionOrder.Dequeue();
            RimTalkInteractionTexts.Remove(oldestKey);
        }

        _keyInsertionOrder.Enqueue(key);
        RimTalkInteractionTexts[key] = cleanText;
    }

    public bool TryGetTextFor(LogEntry entry, out string text)
    {
        text = null;
        return entry != null && RimTalkInteractionTexts.TryGetValue(entry.GetUniqueLoadID(), out text);
    }

    private static string SanitizeXmlString(string invalidXml)
    {
        if (string.IsNullOrEmpty(invalidXml)) return invalidXml;

        StringBuilder stringBuilder = new StringBuilder(invalidXml.Length);
        foreach (char c in invalidXml)
        {
            // XML 1.0 allows:
            // #x9 | #xA | #xD | [#x20-#xD7FF] | [#xE000-#xFFFD] | [#x10000-#x10FFFF]
            if ((c == 0x9) || (c == 0xA) || (c == 0xD) ||
                ((c >= 0x20) && (c <= 0xD7FF)) ||
                ((c >= 0xE000) && (c <= 0xFFFD)))
            {
                stringBuilder.Append(c);
            }
        }
        return stringBuilder.ToString();
    }
}