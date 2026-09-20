using System;

namespace SHCDESE.EventAPI.Units;

public class UnitInnkeeperPickUpAleEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitInnkeeperPickUpAleEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
