using System;

namespace SHCDESE.EventAPI.Units;

public class UnitCattleFarmerPickUpCheeseEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitCattleFarmerPickUpCheeseEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
