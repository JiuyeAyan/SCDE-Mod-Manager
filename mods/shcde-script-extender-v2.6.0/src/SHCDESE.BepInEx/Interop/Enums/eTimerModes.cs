using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Defines the execution context for a timer.
/// </summary>
public enum eTimerModes
{
    /// <summary>
    /// Timer progresses based on real-world time, ignoring game pauses.
    /// </summary>
    GlobalTime,

    /// <summary>
    /// Timer respects the in-game pause state and progresses based on game ticks.
    /// </summary>
    InGameTime
}