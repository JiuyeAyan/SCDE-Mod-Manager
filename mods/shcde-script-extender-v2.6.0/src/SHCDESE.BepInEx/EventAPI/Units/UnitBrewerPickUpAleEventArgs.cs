using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBrewerPickUpAleEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBrewerPickUpAleEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
