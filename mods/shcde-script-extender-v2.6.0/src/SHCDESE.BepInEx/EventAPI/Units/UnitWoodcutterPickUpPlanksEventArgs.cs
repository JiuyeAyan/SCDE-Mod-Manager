using System;

namespace SHCDESE.EventAPI.Units;

public class UnitWoodcutterPickUpPlanksEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitWoodcutterPickUpPlanksEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
