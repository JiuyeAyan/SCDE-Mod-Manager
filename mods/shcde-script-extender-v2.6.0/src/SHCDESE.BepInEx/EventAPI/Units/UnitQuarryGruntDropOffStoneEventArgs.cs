using System;

namespace SHCDESE.EventAPI.Units;

public class UnitQuarryGruntDropOffStoneEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitQuarryGruntDropOffStoneEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
