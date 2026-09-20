using System;

namespace SHCDESE.EventAPI.Units;

public class UnitBlacksmithDropOffIronEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitBlacksmithDropOffIronEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
