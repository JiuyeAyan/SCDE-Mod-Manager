using System;

namespace SHCDESE.EventAPI.Tribes;

public class TribeAssignUnitEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int TribeId { get; set; }
    public int UnitId { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public TribeAssignUnitEventArgs(EventHookPhase phase, int tribeId, int unitId)
    {
        Phase = phase;
        TribeId = tribeId;
        UnitId = unitId;
    }
}
