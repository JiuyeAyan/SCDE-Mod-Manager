using System;

namespace SHCDESE.EventAPI.Units;

public class UnitArmourerPickUpIronEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitArmourerPickUpIronEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
