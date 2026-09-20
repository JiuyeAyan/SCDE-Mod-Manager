using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBrewerPickUpHempEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBrewerPickUpHempEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
