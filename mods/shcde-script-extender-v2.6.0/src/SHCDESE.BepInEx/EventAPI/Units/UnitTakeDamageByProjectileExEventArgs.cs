using System;

namespace SHCDESE.EventAPI.Units;

public class UnitTakeDamageByProjectileExEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 AttackedUnitId { get; set; }
    public Int32 AttackingUnitId { get; set; }
    public Int32 ProjectileId { get; set; }
    public Int32 Damage { get; set; }

    public UnitTakeDamageByProjectileExEventArgs(EventHookPhase phase, Int32 attackedUnitId, Int32 attackingUnitId, Int32 projectileId, Int32 damage)
    {
        Phase = phase;
        AttackedUnitId = attackedUnitId;
        AttackingUnitId = attackingUnitId;
        ProjectileId = projectileId;
        Damage = damage;
    }
}
