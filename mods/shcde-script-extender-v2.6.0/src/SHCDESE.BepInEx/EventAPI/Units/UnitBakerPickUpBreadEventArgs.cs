using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBakerPickUpBreadEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBakerPickUpBreadEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
