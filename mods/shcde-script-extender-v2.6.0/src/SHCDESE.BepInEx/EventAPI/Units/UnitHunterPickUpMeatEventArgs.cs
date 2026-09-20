using System;

namespace SHCDESE.EventAPI.Units;

public class UnitHunterPickUpMeatEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitHunterPickUpMeatEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
