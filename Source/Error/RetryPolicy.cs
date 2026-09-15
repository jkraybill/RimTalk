using System;

namespace RimTalk.Error;

/// <summary>
/// Which failures are asked again on the same provider, and how a failure is logged.
/// rim-universe #105: timeouts clustered after a long pause, and the log kept only a stack trace.
/// </summary>
public static class RetryPolicy
{
    /// <summary>Once, and only for a request that received nothing: it streamed no line, so a retry cannot repeat one.</summary>
    public static bool RetrySameProvider(Exception ex, int retriesSoFar) =>
        retriesSoFar == 0 && ex is FirstByteTimeoutException;

    /// <summary>The failure as the log should keep it: the kind, the message, and for a first-byte timeout the real wait and the pause state.</summary>
    public static string Describe(Exception ex)
    {
        if (ex == null) return "unknown failure";
        var head = $"{ex.GetType().Name}: {ex.Message}";
        return ex is FirstByteTimeoutException f
            ? $"{head}; {f.SecondsWaited.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture)} s real; " +
              $"paused at start: {(f.StartedPaused ? "yes" : "no")}, at failure: {(f.EndedPaused ? "yes" : "no")}"
            : head;
    }
}
