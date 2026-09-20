using System;

namespace SHCDESE.EventAPI.Units;

public class UnitPoleturnerDropOffProduceEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitPoleturnerDropOffProduceEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
