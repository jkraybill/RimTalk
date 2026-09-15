using System;

namespace RimTalk.Error;

/// <summary>
/// A request that received no bytes at all before the first-byte ceiling. Nothing was
/// streamed, so the request can be asked again without repeating a line. Carries the real
/// seconds waited and whether the game was paused when the request began and when it failed,
/// because a failure while paused is only shown once the game ticks again (rim-universe #105).
/// </summary>
public class FirstByteTimeoutException : TimeoutException
{
    public double SecondsWaited { get; }
    public bool StartedPaused { get; }
    public bool EndedPaused { get; }

    public FirstByteTimeoutException(float ceilingSeconds, double secondsWaited, bool startedPaused, bool endedPaused)
        : base($"Connection timed out (Waited {ceilingSeconds}s for first token)")
    {
        SecondsWaited = secondsWaited;
        StartedPaused = startedPaused;
        EndedPaused = endedPaused;
    }
}
