using System;

namespace SHCDESE.EventAPI.Units;

public class UnitInnkeeperDropOffAleEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitInnkeeperDropOffAleEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
