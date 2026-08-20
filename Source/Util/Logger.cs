using System.Collections.Generic;
using Verse;

namespace RimTalk.Util;

public static class Logger
{
    private const string ModTag = "[RimTalk]";

    /// <summary>
    /// Keys already warned about. Bounded, because these are written from inside
    /// per-pawn loops that run every prompt and an unbounded set here would be the
    /// same growth bug this pass exists to fix.
    /// </summary>
    private static readonly HashSet<string> Warned = new();
    private const int MaxWarnedKeys = 200;

    /// <summary>
    /// Warn the first time a given key fails, and stay quiet after.
    ///
    /// rim-universe #2. The alternative to a silent catch is not a loud one — these
    /// sit in loops over every pawn in every prompt, and a mod conflict that throws
    /// once throws every time. A bare Warning here would bury the log and get the
    /// handler reverted to a bare catch by the next person.
    /// </summary>
    public static void WarningOnce(string key, object message)
    {
        lock (Warned)
        {
            if (!Warned.Add(key)) return;
            if (Warned.Count > MaxWarnedKeys) Warned.Clear();   // a session-long cap, not a cache
        }
        Warning(message);
    }
    public static void Message(object message)
    {
        Log.Message($"{ModTag} {message}\n\n");
    }
        
    public static void Debug(object message)
    {
        if (Prefs.LogVerbose)
            Log.Message($"{ModTag} {message}\n\n");
    }
        
    public static void Warning(object message)
    {
        Log.Warning($"{ModTag} {message}\n\n");
    }
        
    public static void Error(object message)
    {
        Log.Error($"{ModTag} {message}\n\n");
    }

    public static void ErrorOnce(object text, int key)
    {
        Log.ErrorOnce($"{ModTag} {text}\n\n", key);
    }
}