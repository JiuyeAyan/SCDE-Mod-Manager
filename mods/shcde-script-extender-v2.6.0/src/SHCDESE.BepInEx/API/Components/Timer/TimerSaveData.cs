using MessagePack;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// A serializable data structure that represents the state of a single active timer.
/// This is used for saving and loading game states.
/// </summary>
[MessagePackObject(true)]
public class TimerSaveData
{
    /// <summary>
    /// Gets or sets the unique handle ID of the timer, used to identify it.
    /// </summary>
    public string HandleId { get; set; }

    /// <summary>
    /// Gets or sets the globally unique, registered name of the Lua function to call.
    /// </summary>
    public string CallbackName { get; set; }

    /// <summary>
    /// Gets or sets the type of the timer (e.g., Delayed or Repeated).
    /// </summary>
    public TimerType Type { get; set; }

    /// <summary>
    /// Gets or sets the timer's progress in high-precision game time units.
    /// For a Delayed timer, this is the time remaining.
    /// For a Repeated timer, this is the time accumulated since the last execution.
    /// </summary>
    public long GameTimeRemainingOrElapsed { get; set; }

    /// <summary>
    /// Gets or sets the timer's original full duration, measured in high-precision game time units.
    /// </summary>
    public long OriginalIntervalInGameTime { get; set; }

    /// <summary>
    /// Gets or sets the timer mode used for the timer.
    /// </summary>
    public eTimerModes TimerMode { get; set; }
}

/// <summary>
/// A container for a list of <see cref="TimerSaveData"/> objects, used for clean MessagePack serialization.
/// </summary>
[MessagePackObject(true)]
public class TimerSaveDataHolder
{
    /// <summary>
    /// Gets or sets the list of saved timer data.
    /// </summary>
    public List<TimerSaveData> TimerSaveData { get; set; }
}