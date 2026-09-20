using System;

namespace SHCDESE.EventAPI.Units;

public class UnitPitcherDropOffRawPitchEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitPitcherDropOffRawPitchEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
