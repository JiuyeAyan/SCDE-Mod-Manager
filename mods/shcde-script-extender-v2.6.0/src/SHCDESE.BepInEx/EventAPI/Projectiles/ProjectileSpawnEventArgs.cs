using SHCDESE.Interop.Enums;
using System;

namespace SHCDESE.EventAPI.Projectiles;

public class ProjectileSpawnEventArgs : EventHookBase
{
    // --- Input/Output Parameters ---
    public int SourceUnitId { get; set; }
    public Int16 PlayerSourceId { get; set; }
    public int UnitPlayerSourceId { get; set; }
    public int SourceWorldTileX { get; set; }
    public int SourceWorldTileY { get; set; }
    public int SourceElevation { get; set; }
    public int TargetWorldTileX { get; set; }
    public int TargetWorldTileY { get; set; }
    public int TargetElevation { get; set; }
    public ProjectileType ProjectileType { get; set; }
    public int AttackedUnitId { get; set; }

    // --- Return Value ---
    public Int64 ReturnValue { get; set; } = 0;

    public ProjectileSpawnEventArgs(
        EventHookPhase phase,
        int sourceUnitId,
        Int16 playerSourceId,
        int unitPlayerSourceId,
        int sourceWorldTileX,
        int sourceWorldTileY,
        int sourceElevation,
        int targetWorldTileX,
        int targetWorldTileY,
        int targetElevation,
        ProjectileType projectileType_arg,
        int attackedUnitId)
    {
        Phase = phase;
        SourceUnitId = sourceUnitId;
        PlayerSourceId = playerSourceId;
        UnitPlayerSourceId = unitPlayerSourceId;
        SourceWorldTileX = sourceWorldTileX;
        SourceWorldTileY = sourceWorldTileY;
        SourceElevation = sourceElevation;
        TargetWorldTileX = targetWorldTileX;
        TargetWorldTileY = targetWorldTileY;
        TargetElevation = targetElevation;
        ProjectileType = projectileType_arg;
        AttackedUnitId = attackedUnitId;
    }
}