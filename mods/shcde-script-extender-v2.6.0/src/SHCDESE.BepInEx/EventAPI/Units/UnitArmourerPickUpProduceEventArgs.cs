using System;

namespace SHCDESE.EventAPI.Units;

public class UnitArmourerPickUpProduceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitArmourerPickUpProduceEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
