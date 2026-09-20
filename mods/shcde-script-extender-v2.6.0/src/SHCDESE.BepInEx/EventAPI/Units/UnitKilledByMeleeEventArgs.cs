using System;

namespace SHCDESE.EventAPI.Units;

public class UnitKilledByMeleeEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 AttackingUnitId { get; set; }
    public Int32 AttackedUnitId { get; set; }

    public UnitKilledByMeleeEventArgs(EventHookPhase phase, Int32 attackingUnitId, Int32 attackedUnitId)
    {
        Phase = phase;
        AttackingUnitId = attackingUnitId;
        AttackedUnitId = attackedUnitId;
    }
}
