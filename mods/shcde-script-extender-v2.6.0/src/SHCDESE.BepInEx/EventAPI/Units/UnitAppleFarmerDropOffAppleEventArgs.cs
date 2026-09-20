using System;

namespace SHCDESE.EventAPI.Units;

public class UnitAppleFarmerDropOffAppleEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitAppleFarmerDropOffAppleEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
