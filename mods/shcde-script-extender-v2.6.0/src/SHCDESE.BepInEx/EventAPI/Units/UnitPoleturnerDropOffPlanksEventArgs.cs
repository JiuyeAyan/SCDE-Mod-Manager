using System;

namespace SHCDESE.EventAPI.Units;

public class UnitPoleturnerDropOffPlanksEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitPoleturnerDropOffPlanksEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
