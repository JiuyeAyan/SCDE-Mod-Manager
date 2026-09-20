using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBakerDropOffBreadEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBakerDropOffBreadEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
