using System;

namespace SHCDESE.EventAPI.Units;

public class UnitHunterDropOffMeatEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitHunterDropOffMeatEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
