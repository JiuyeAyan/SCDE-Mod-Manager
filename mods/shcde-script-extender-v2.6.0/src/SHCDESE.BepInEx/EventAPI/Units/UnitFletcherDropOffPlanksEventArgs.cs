using System;

namespace SHCDESE.EventAPI.Units;

public class UnitFletcherDropOffPlanksEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitFletcherDropOffPlanksEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
