using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMiner2PickUpIronEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitMiner2PickUpIronEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
