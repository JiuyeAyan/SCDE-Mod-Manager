using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBakerPickUpFlourEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBakerPickUpFlourEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
