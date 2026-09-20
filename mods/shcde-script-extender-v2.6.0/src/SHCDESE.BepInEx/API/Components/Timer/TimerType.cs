using System;

namespace SHCDESE.API.Components.Timer;

/// <summary>
/// Defines whether a timer is a one-shot or repeating.
/// </summary>
public enum TimerType : byte
{
    /// <summary>
    /// A one-shot timer that executes once after its interval has passed.
    /// </summary>
    Delayed,

    /// <summary>
    /// A recurring timer that executes every time its interval has passed.
    /// </summary>
    Repeated
}