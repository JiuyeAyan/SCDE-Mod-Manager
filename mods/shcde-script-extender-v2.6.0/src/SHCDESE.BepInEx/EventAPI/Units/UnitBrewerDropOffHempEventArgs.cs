using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBrewerDropOffHempEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBrewerDropOffHempEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
