using System;

namespace SHCDESE.EventAPI.Trigger;

public class TriggerEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TriggerHandle { get; }
    public int EntityId { get; }
    public TriggerEventArgs(EventHookPhase phase, int triggerHandle, int entityId) 
    {
        Phase = phase;
        TriggerHandle = triggerHandle;
        EntityId = entityId; 
    }
}