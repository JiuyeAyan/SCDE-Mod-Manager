using System;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// A high-precision, deterministic game clock that advances using integer-only arithmetic.
/// This is the canonical source of time for all deterministic systems.
/// </summary>
public static class DeterministicClock
{
    public const long GAME_TIME_UNITS_PER_SECOND = 1_000_000L;

    /// <summary>
    /// The base tick rate (ticks/sec at 1× speed). 
    /// Matches the engine default: engineFrameTime = 0.025 → 40 ticks/sec.
    /// </summary>
    public const int BASE_TICKS_PER_SECOND = 40;

    /// <summary>
    /// Fixed units added per tick, calibrated to the BASE rate.
    /// This never changes — faster game speed = more ticks/sec = faster accumulation.
    /// </summary>
    public const long UNITS_PER_TICK = GAME_TIME_UNITS_PER_SECOND / BASE_TICKS_PER_SECOND; // 25_000

    /// <summary>
    /// Gets the total accumulated game time in high-precision units.
    /// 1 real-world second is represented by 1,000,000 time units.
    /// </summary>
    public static long TotalGameTimeUnits { get; private set; }

    /// <summary>
    /// Advances the clock by exactly one sim ticks worth of game time.
    /// Called once per sim tick from the synchronized hook context.
    /// </summary>
    public static void Tick()
    {
        TotalGameTimeUnits += UNITS_PER_TICK;
    }
}
