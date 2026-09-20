using System;

namespace SHCDESE.EventAPI.Units;

public class UnitCattleFarmerDropOffCheeseEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitCattleFarmerDropOffCheeseEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
