using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBrewerDropOffAleEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBrewerDropOffAleEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
