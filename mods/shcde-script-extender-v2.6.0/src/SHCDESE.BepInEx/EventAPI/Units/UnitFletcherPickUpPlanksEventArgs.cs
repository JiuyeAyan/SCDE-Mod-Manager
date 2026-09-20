using System;

namespace SHCDESE.EventAPI.Units;

public class UnitFletcherPickUpPlanksEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitFletcherPickUpPlanksEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
