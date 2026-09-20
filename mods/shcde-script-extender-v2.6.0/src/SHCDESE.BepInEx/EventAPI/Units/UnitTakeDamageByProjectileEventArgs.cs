using System;

namespace SHCDESE.EventAPI.Units;

public class UnitTakeDamageByProjectileEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public Int32 AttackedUnitId { get; set; }
    public Int32 ProjectileId { get; set; }
    public Int32 UnknownBool { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public UnitTakeDamageByProjectileEventArgs(EventHookPhase phase, Int32 attackedUnitId, Int32 projectileId, Int32 unknownBool)
    {
        Phase = phase;
        AttackedUnitId = attackedUnitId;
        ProjectileId = projectileId;
        UnknownBool = unknownBool;
    }
}
