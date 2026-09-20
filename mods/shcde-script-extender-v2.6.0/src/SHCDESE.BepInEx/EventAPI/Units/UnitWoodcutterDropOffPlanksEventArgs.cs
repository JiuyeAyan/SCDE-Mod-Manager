using System;

namespace SHCDESE.EventAPI.Units;

public class UnitWoodcutterDropOffPlanksEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitWoodcutterDropOffPlanksEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
