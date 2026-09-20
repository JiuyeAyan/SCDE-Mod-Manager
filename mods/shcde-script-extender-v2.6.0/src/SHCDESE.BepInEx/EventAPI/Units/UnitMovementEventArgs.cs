using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMovementEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public UInt32 UnitId { get; }

    public UnitMovementEventArgs(EventHookPhase phase, UInt32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
