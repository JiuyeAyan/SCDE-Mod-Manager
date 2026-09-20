using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMillerPickUpFlourEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitMillerPickUpFlourEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
