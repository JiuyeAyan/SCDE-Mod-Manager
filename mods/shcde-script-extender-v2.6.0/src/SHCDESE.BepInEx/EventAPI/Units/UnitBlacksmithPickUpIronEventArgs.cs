using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBlacksmithPickUpIronEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBlacksmithPickUpIronEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
