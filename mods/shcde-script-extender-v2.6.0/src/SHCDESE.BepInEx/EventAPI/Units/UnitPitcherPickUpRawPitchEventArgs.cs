using System;

namespace SHCDESE.EventAPI.Units;

public class UnitPitcherPickUpRawPitchEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 UnitId { get; set; }

    public UnitPitcherPickUpRawPitchEventArgs(EventHookPhase phase, Int32 unitId)
    {
        Phase = phase;
        UnitId = unitId;
    }
}
