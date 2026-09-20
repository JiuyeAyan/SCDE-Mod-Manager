using System;

namespace SHCDESE.EventAPI.Units;

public class UnitQuarryOxDropOffStoneEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitQuarryOxDropOffStoneEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
