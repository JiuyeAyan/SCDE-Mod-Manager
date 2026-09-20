using System;

namespace SHCDESE.EventAPI.Units;

public class UnitMillerPickUpWheatEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitMillerPickUpWheatEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
