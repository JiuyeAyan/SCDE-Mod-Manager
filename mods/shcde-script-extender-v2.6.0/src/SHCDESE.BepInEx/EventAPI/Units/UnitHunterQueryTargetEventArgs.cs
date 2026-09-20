using System;

namespace SHCDESE.EventAPI.Units;

public class UnitHunterQueryTargetEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int QueryUnitId { get; }
    public int HunterUnitId { get; }

    // --- Return Value ---
    public bool? IsValidTarget { get; set; } = null!;

    public UnitHunterQueryTargetEventArgs(EventHookPhase phase, int queryUnitId, int hunterUnitId)
    {
        Phase = phase;
        QueryUnitId = queryUnitId;
        HunterUnitId = hunterUnitId;
    }
}
