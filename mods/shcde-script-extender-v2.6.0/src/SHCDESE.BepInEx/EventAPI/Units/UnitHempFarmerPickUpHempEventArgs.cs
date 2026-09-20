using System;

namespace SHCDESE.EventAPI.Units;

public class UnitHempFarmerPickUpHempEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitHempFarmerPickUpHempEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
