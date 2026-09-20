using System;

namespace SHCDESE.EventAPI;

public abstract class EventHookBase : EventArgs
{
    // --- State ---
    public EventHookPhase Phase { get; protected set; }

    // --- Control Flow, Return Value ---
    public bool SkipOriginalFunction { get; set; } = false;
}
