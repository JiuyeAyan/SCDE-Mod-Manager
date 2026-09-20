using System;

namespace SHCDESE.EventAPI.Units;

public class UnitAppleFarmerPickUpAppleEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitAppleFarmerPickUpAppleEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
