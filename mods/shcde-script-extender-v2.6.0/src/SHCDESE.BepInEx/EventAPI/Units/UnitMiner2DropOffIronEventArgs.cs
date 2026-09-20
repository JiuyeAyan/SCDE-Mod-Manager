using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMiner2DropOffIronEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitMiner2DropOffIronEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
