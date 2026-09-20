using System;

namespace SHCDESE.EventAPI.Units;

public class UnitFletcherDropOffProduceEventArg : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitFletcherDropOffProduceEventArg(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
