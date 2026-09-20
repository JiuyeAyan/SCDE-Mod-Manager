using MessagePack;
using Noesis;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;

namespace SHCDESE.API.Components.MapEditor;

/// <summary>
/// Data Transfer Object for the SEMA (SE - Map Area) format.
/// </summary>
[MessagePackObject]
public class SEMAData
{
    public const int CurrentFormatVersion = 2;

    [Key(0)] public int FormatVersion { get; set; }
    [Key(1)] public int Width { get; set; }
    [Key(2)] public int Height { get; set; }
    [Key(3)] public int[] Logic { get; set; } = [];       // aka PropertyFlags
    [Key(4)] public ushort[] Organism { get; set; } = []; // legacy vegetation IDs; never restored
    [Key(5)] public byte[] Logic2 { get; set; } = [];     // aka TileTypes
    [Key(6)] public byte[] Heights { get; set; } = [];
    [Key(7)] public byte[] DefaultHeights { get; set; } = [];
    [Key(8)] public byte[] Damage { get; set; } = [];     // aka States
    [Key(9)] public byte[] WallOwners { get; set; } = [];
    [Key(10)] public ushort[] RandomNoise { get; set; } = [];
    [Key(11)] public ushort[] Structure { get; set; } = []; // legacy building IDs; never restored
    [Key(12)] public byte[] StructureWas { get; set; } = []; // legacy structure descriptor; never restored
    [Key(13)] public ushort[] TileUnitId { get; set; } = []; // legacy unit IDs; never restored
    [Key(14)] public short[] Fly { get; set; } = []; // legacy projectile IDs; never restored
    [Key(15)] public short[] Macro { get; set; } = [];
    [Key(16)] public ushort[] PathConnection { get; set; } = [];
    [Key(17)] public byte[] Delay { get; set; } = [];
    [Key(18)] public byte[] GatePath { get; set; } = [];
    [Key(19)] public Dictionary<int, SEMATribe> Tribes { get; set; } = [];
    [Key(20)] public Dictionary<int, SEMAUnit> Units { get; set; } = [];
    [Key(21)] public Dictionary<int, SEMABuilding> Buildings { get; set; } = [];
    [Key(22)] public Dictionary<int, SEMAVegetation> Vegetations { get; set; } = [];
    [Key(23)] public Dictionary<int, SEMAPitchTile> PitchTiles { get; set; } = [];
}

[MessagePackObject]
public class SEMATribe
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int GlobalId { get; set; }
    [Key(2)] public int LeaderUnitId { get; set; }
    [Key(3)] public List<SEMAUnit> Units { get; set; } = [];
    [Key(4)] public SEMATribeOrderContext? OrderContext { get; set; }
    [Key(5)] public int PlayerId { get; set; }
    [Key(6)] public TribeStance Stance { get; set; }
    [Key(7)] public TribeMoveType MoveType { get; set; }
}

[MessagePackObject]
public class SEMAUnit
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int GlobalId { get; set; }
    [Key(2)] public eChimps UnitType { get; set; }
    [Key(3)] public int Health { get; set; }
    [Key(4)] public int MaxHealth { get; set; }
    [Key(5)] public Dircs Direction { get; set; }
    [Key(6)] public int TribeId { get; set; }
    [Key(7)] public int PlayerId { get; set; }
    [Key(8)] public int ColorPlayerId { get; set; }
    [Key(9)] public UnmanagedVector2<UInt16> LocalTilePosition { get; set; }
    [Key(10)] public UnmanagedVector2<UInt16> WorldTilePosition { get; set; }
    [Key(11)] public int HeightElevation { get; set; }
}

[MessagePackObject]
public class SEMATribeOrderContext
{
    [Key(0)] public TribeAICommand Command { get; set; }
    [Key(1)] public UnmanagedVector2<UInt16> LocalTile { get; set; }
    [Key(2)] public int TileId { get; set; }
    [Key(3)] public Int32 EntityId { get; set; }
    [Key(4)] public Int32 EntityGlobalId { get; set; }
    [Key(5)] public Int32 Metadata { get; set; }
    [Key(6)] public UnmanagedVector2<UInt16>[] PatrolPoints { get; set; } = [];
    [Key(7)] public int CurrentPatrolPoint { get; set; }
    [Key(8)] public TribePatrolMode PatrolMode { get; set; }
    [Key(9)] public bool AnimalAttackNearestUnit { get; set; }

    public static SEMATribeOrderContext FromMemory(ref GameUnit unit, ref GameTribe tribe)
    {
        SEMATribeOrderContext context = new SEMATribeOrderContext();
        Span<GameUnit> units = GameUnitManagerAPI.Instance.GetUnitsAsSpan();
        Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();

        // cmd: Move Here
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.MoveHerePosition)
        {
            context.Command = TribeAICommand.MoveHerePosition;
            context.LocalTile = new UnmanagedVector2<UInt16>(tribe.r_PatrolPoint1TileX, tribe.r_PatrolPoint1TileY);
            return context;
        }

        // cmd: Attack Unit
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.AttackUnit)
        {
            context.Command = TribeAICommand.AttackUnit;
            context.EntityId = unit.r_AI_ContextTargetUnitId;
            context.EntityGlobalId = (int)unit.r_AI_ContextTargetUnitGlobalId;
            return context;
        }

        // cmd: Attack Tile (Ranged)
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.AttackTilePosition)
        {
            context.Command = TribeAICommand.AttackTilePosition;
            context.LocalTile = new UnmanagedVector2<UInt16>(unit.r_ContextTargetTileX, unit.r_ContextTargetTileY);
            return context;
        }

        // cmd: Dig Moat Tile
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.DigMoatTileId)
        {
            context.Command = TribeAICommand.DigMoatTileId;
            context.LocalTile = new UnmanagedVector2<UInt16>(unit.r_ContextTargetTileX, unit.r_ContextTargetTileY);
            return context;
        }

        // cmd: AttackBuilding, AttackWallTileId, AttachLadderToWall
        if (unit.r_AI_LastIssuedTribeCommand == 0)
        {
            if (unit.r_AI_ContextTargetBuildingTileId != 0)
            {
                context.TileId = (int)unit.r_AI_ContextTargetBuildingTileId;
                if (!GameTileManagerAPI.Instance.IsValidTileId(context.TileId))
                    return context;
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(context.TileId);
                if (buildingId == 0)
                {
                    // cmd: AttachLadderToWall
                    if (unit.r_UnitChimp == eChimps.CHIMP_TYPE_LADDERMAN)
                    {
                        context.Command = TribeAICommand.AttachLadderToWall;
                        return context;
                    }
                    else
                    {
                        // cmd: AttackWallTileId
                        context.Command = TribeAICommand.AttackWallTileId;
                        return context;
                    }
                }
                else
                {
                    context.Command = TribeAICommand.AttackBuilding;
                    // cmd: AttackBuilding
                    if (buildingId > 0 && buildingId <= buildings.Length)
                    {
                        ref GameBuilding building = ref buildings[buildingId - 1];
                        if (building.r_AliveState == AliveState.IsAlive)
                        {
                            context.EntityId = buildingId;
                            context.EntityGlobalId = (int)building.r_GlobalId;
                        }
                    }
                    return context;
                }
            }

            // cmd: ManPitchCauldronOrBuildTent
            // special cases: oil smelter
            if (unit.r_AIState == 9)
            {
                context.Command = TribeAICommand.ManPitchCauldronOrBuildTent;
                int buildingId = unit.r_LinkedProductionBuildingId;
                if (buildingId > 0 && buildingId <= buildings.Length)
                {
                    ref GameBuilding building = ref buildings[buildingId - 1];
                    if (building.r_AliveState == AliveState.IsAlive)
                    {
                        context.EntityId = buildingId;
                        context.EntityGlobalId = (int)building.r_GlobalId;
                    }
                }
                return context;
            }
            // special case: tent (or other)
            else if (unit.r_AIState == 8)
            {
                context.Command = TribeAICommand.ManPitchCauldronOrBuildTent;
                if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(unit.r_TargetTilePositionX, unit.r_TargetTilePositionY))
                    return context;
                int targetTileId = GameTileManagerAPI.Instance.GetTileId(unit.r_TargetTilePositionX, unit.r_TargetTilePositionY);
                int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(targetTileId);
                if (buildingId > 0 && buildingId <= buildings.Length)
                {
                    ref GameBuilding building = ref buildings[buildingId - 1];
                    if (building.r_AliveState == AliveState.IsAlive)
                    {
                        context.EntityId = buildingId;
                        context.EntityGlobalId = (int)building.r_GlobalId;
                    }
                }
                return context;
            }

        }

        // cmd: ManSiegeEquipment
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.ManSiegeEquipment)
        {
            context.Command = TribeAICommand.ManSiegeEquipment;
            context.EntityId = unit.r_AI_ContextTargetUnitId;
            if (context.EntityId > 0 && context.EntityId <= units.Length)
            {
                ref GameUnit targetUnit = ref units[context.EntityId - 1];
                if (targetUnit.r_AliveState == AliveState.IsAlive)
                    context.EntityGlobalId = (int)targetUnit.r_GlobalId;
            }
            return context;
        }

        // cmd: ThrowLava
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.ThrowLava)
        {
            context.Command = TribeAICommand.ThrowLava;
            context.LocalTile = new UnmanagedVector2<UInt16>(unit.r_ContextTargetTileX, unit.r_ContextTargetTileY);
            return context;
        }

        // cmd: BuildTunnel
        if (unit.r_AI_LastIssuedTribeCommand == (int)TribeAICommand.BuildTunnel)
        {
            context.Command = TribeAICommand.BuildTunnel;
            if (!GameTileManagerAPI.Instance.IsTileInsideMapBounds(unit.r_TargetTilePositionX, unit.r_TargetTilePositionY))
                return context;
            int targetTileId = GameTileManagerAPI.Instance.GetTileId(unit.r_TargetTilePositionX, unit.r_TargetTilePositionY);
            int buildingId = GameTileManagerAPI.Instance.GetTileBuildingId(targetTileId);
            if (buildingId > 0 && buildingId <= buildings.Length)
            {
                ref GameBuilding building = ref buildings[buildingId - 1];
                if (building.r_AliveState == AliveState.IsAlive)
                {
                    context.EntityId = buildingId;
                    context.EntityGlobalId = (int)building.r_GlobalId;
                }
            }
            return context;
        }

        // cmd: UnitDissolve
        if (unit.r_AIState == 110)
        {
            context.Command = TribeAICommand.UnitDissolve;
            return context;
        }
        else if (unit.r_AIState == 1) // cmd: UnitStop
        {
            context.Command = TribeAICommand.UnitStop;
            return context;
        }

        // TODO: ForceAttackBuilding (do we even need this?)

        return context;
    }
}

[MessagePackObject]
public class SEMABuilding
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int GlobalId { get; set; }
    [Key(2)] public eStructs Structure { get; set; }
    [Key(3)] public Int16 Health { get; set; }
    [Key(4)] public UInt16 MaxHealth { get; set; }
    [Key(5)] public int PlayerId { get; set; }
    [Key(6)] public int ColorPlayerId { get; set; }
    [Key(7)] public UnmanagedVector2<UInt16> LocalTilePosition { get; set; }
    [Key(8)] public Int16 HeightElevation { get; set; }
    [Key(9)] public int BuildingScale { get; set; }
    [Key(10)] public int SpriteVariationIndex { get; set; }
}

[MessagePackObject]
public class SEMAVegetation
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int GlobalId { get; set; }
    [Key(2)] public VegetationType Vegetation { get; set; }
    [Key(3)] public UnmanagedVector2<UInt16> LocalTilePosition { get; set; }
    [Key(4)] public Int16 ResourceState { get; set; }
    [Key(5)] public UInt32 GrowthStage { get; set; }
    [Key(6)] public UInt32 GrowthProgress { get; set; }
    [Key(7)] public UInt16 Health { get; set; }
}

[MessagePackObject]
public class SEMAPitchTile
{
    [Key(0)] public int GlobalId { get; set; }
    [Key(1)] public int PlayerId { get; set; }
    [Key(2)] public UnmanagedVector2<UInt16> LocalTilePosition { get; set; }
    [Key(3)] public UInt16 RandomSeed { get; set; }
    [Key(4)] public PitchState State { get; set; }
    [Key(5)] public Int16 FireTimer { get; set; }
}

[MessagePackObject]
public class SEMAProjectile
{
    [Key(0)] public int Id { get; set; }
    [Key(1)] public int GlobalId { get; set; }
    [Key(2)] public int GameMaterial { get; set; }
    [Key(3)] public int UnitPlayerSourceId { get; set; }
    [Key(4)] public int PlayerSourceId { get; set; }
    [Key(5)] public ProjectileType ProjectileType { get; set; }
    [Key(6)] public UnmanagedVector2<UInt16> CurrentLocalTilePosition { get; set; }
    [Key(7)] public int CurrentHeight { get; set; }
    [Key(8)] public UnmanagedVector2<UInt16> SourceLocalTilePosition { get; set; }
    [Key(9)] public int SourceHeight { get; set; }
    [Key(10)] public UnmanagedVector2<UInt16> TargetLocalTilePosition { get; set; }
    [Key(11)] public int TargetHeight { get; set; }
    [Key(12)] public int ArrowState { get; set; }
    [Key(13)] public Int16 VelocityModifier { get; set; }
    [Key(14)] public float VelocityGained { get; set; }
    [Key(15)] public float VelocityX { get; set; }
    [Key(16)] public float VelocityY { get; set; }
    [Key(17)] public int SpriteRotation { get; set; }
    [Key(18)] public float Velocity { get; set; }
    [Key(19)] public int SourceUnitId { get; set; }
    [Key(20)] public int TargetUnitId { get; set; }
    [Key(21)] public int AfterimageSprite { get; set; }
}
