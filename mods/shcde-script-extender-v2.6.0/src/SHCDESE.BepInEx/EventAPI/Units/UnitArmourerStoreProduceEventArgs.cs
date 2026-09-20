using System;

namespace SHCDESE.EventAPI.Units;

public class UnitArmourerStoreProduceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitArmourerStoreProduceEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
