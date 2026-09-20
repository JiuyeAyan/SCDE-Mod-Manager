using System;

namespace SHCDESE.EventAPI.Units;

public class UnitWheatFarmerDropOffWheatEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitWheatFarmerDropOffWheatEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
