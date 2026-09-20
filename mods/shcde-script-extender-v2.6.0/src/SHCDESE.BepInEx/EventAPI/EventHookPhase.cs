using System;

namespace SHCDESE.EventAPI;

/// <summary>
/// Controls whether the event is before or after the associated operation.
/// </summary>
public enum EventHookPhase : int
{
    /// <summary>
    /// The event is being raised *before* the original game function is called.
    /// </summary>
    Pre,
    /// <summary>
    /// The event is being raised *after* the original game function was called.
    /// </summary>
    Post
}
