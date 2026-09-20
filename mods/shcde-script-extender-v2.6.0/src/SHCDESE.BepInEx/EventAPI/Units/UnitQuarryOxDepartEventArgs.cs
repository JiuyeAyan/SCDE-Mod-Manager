using System;

namespace SHCDESE.EventAPI.Units;

public class UnitQuarryOxDepartEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitQuarryOxDepartEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
