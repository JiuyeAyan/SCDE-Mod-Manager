using System;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// A lightweight, allocation-free snapshot of game time captured at a specific moment.
/// Use this to check how much time has elapsed since the snapshot was taken.
/// </summary>
/// <remarks>
/// This is a <c>readonly struct</c> to avoid heap allocations when used in high-frequency contexts.
/// Obtain an instance via <see cref="GameTimeManagerAPI.CaptureTimeStamp"/>.
/// </remarks>
public readonly struct GameTimeStamp
{
    /// <summary>
    /// The value of <see cref="DeterministicClock.TotalGameTimeUnits"/> at the moment this stamp was captured.
    /// </summary>
    public long CapturedGameTimeUnits { get; }

    /// <summary>
    /// The game tick at the moment this stamp was captured.
    /// </summary>
    public int CapturedGameTick { get; }

    /// <summary>
    /// Returns true if this stamp was never initialized (i.e. it is a default struct value).
    /// </summary>
    public bool IsEmpty => CapturedGameTimeUnits == 0 && CapturedGameTick == 0;

    internal GameTimeStamp(long capturedGameTimeUnits, int capturedGameTick)
    {
        CapturedGameTimeUnits = capturedGameTimeUnits;
        CapturedGameTick = capturedGameTick;
    }

    /// <summary>
    /// Returns how many high-precision game time units have elapsed since this stamp was captured.
    /// </summary>
    /// <param name="currentGameTimeUnits">The current value of <see cref="DeterministicClock.TotalGameTimeUnits"/>.</param>
    /// <returns>Elapsed game time units. Returns 0 if the stamp is empty or current time is behind.</returns>
    public long GetElapsedGameTimeUnits(long currentGameTimeUnits)
    {
        if (IsEmpty) return 0;
        long elapsed = currentGameTimeUnits - CapturedGameTimeUnits;
        return elapsed > 0 ? elapsed : 0;
    }

    /// <summary>
    /// Returns how many milliseconds have elapsed since this stamp was captured.
    /// </summary>
    /// <param name="currentGameTimeUnits">The current value of <see cref="DeterministicClock.TotalGameTimeUnits"/>.</param>
    public long GetElapsedMilliseconds(long currentGameTimeUnits) => GetElapsedGameTimeUnits(currentGameTimeUnits) / 1_000L;

    public override string ToString() => $"GameTimeStamp(Tick={CapturedGameTick}, Units={CapturedGameTimeUnits})";
}
