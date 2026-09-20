using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMillerDropOffFlourEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitMillerDropOffFlourEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
