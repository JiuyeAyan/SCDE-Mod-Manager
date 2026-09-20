using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBlacksmithDropOffProduceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBlacksmithDropOffProduceEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
