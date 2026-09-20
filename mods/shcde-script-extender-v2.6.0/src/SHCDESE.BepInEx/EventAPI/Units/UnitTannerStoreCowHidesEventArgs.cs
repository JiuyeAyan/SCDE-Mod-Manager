using System;

namespace SHCDESE.EventAPI.Units;

public class UnitTannerStoreCowHidesEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitTannerStoreCowHidesEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
