using System;

namespace SHCDESE.EventAPI.Units;

public class UnitTannerDropOffCowHidesEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitTannerDropOffCowHidesEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
