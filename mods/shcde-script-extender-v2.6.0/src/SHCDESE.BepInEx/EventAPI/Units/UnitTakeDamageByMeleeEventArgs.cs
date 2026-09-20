using System;

namespace SHCDESE.EventAPI.Units;

public class UnitTakeDamageByMeleeEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 AttackingUnitId { get; set; }
    public Int32 DamagedUnitId { get; set; }
    public Int32 Damage { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitTakeDamageByMeleeEventArgs(EventHookPhase phase, Int32 attackingUnitId, Int32 damagedUnitId, Int32 value)
    {
        Phase = phase;
        AttackingUnitId = attackingUnitId;
        DamagedUnitId = damagedUnitId;
        Damage = value;
    }
}
