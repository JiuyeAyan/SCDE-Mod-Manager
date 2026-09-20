using System;

namespace SHCDESE.EventAPI.Units;

public class UnitKilledByProjectileEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int64 AttackedUnitId { get; set; }
    public Int64 ProjectileId { get; set; }

    public UnitKilledByProjectileEventArgs(EventHookPhase phase, Int64 attackedUnitId, Int64 projectileId)
    {
        Phase = phase;
        AttackedUnitId = attackedUnitId;
        ProjectileId = projectileId;
    }
}
