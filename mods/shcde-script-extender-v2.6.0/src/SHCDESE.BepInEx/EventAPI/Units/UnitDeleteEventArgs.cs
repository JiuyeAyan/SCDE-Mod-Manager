using System;

namespace SHCDESE.EventAPI.Units;

public class UnitDeleteEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public UInt32 UnitId { get; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitDeleteEventArgs(EventHookPhase phase, UInt32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
