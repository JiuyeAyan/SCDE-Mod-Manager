using System;

namespace SHCDESE.EventAPI.Units;

public class UnitHempFarmerDropOffHempEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitHempFarmerDropOffHempEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
