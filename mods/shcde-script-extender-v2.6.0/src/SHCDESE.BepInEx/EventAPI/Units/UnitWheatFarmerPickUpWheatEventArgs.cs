using System;

namespace SHCDESE.EventAPI.Units;

public class UnitWheatFarmerPickUpWheatEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitWheatFarmerPickUpWheatEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
