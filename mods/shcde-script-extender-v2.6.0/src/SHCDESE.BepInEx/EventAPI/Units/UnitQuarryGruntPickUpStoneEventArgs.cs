using System;

namespace SHCDESE.EventAPI.Units;

public class UnitQuarryGruntPickUpStoneEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitQuarryGruntPickUpStoneEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
