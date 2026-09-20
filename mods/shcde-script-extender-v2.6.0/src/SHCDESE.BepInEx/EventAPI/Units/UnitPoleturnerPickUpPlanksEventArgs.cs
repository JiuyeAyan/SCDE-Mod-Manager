using System;

namespace SHCDESE.EventAPI.Units;

public class UnitPoleturnerPickUpPlanksEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitPoleturnerPickUpPlanksEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
