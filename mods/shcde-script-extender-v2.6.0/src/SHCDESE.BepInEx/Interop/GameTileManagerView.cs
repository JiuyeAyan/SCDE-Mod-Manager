using System;
using SHCDESE.Interop.Enums;
namespace SHCDESE.Interop;

/// <summary>
/// Provides a structured, safe view over the raw memory buffer of the game's tile manager.
/// This is not complete, and is likely missing plenty of existing grids that have yet to be found.
/// </summary>
/// <remarks>
/// This class maps the game's contiguous block of memory for tile data into distinct,
/// type-safe <see cref="Span{T}"/> properties. Each property corresponds to a specific grid
/// of data for the entire map (e.g., tile height, ownership, properties).
/// Accessing tile data through this class is neccessary due to the TileManager being too large for simple C# structures (1MB limit)
/// </remarks>
public unsafe class GameTileManagerView
{
    private readonly void* _ptr;
    private const int MapWidth = 800;
    private const int MapHeight = 800;
    private const int MapSize = MapWidth * MapHeight;
    public const int NativePackedTileCapacity = 320_800;
    public const int MoatWorkTaskSlotCapacity = 64_000;

    // dynamic= is mostly ignored in flattened view, or can be otherwise recalculated by the game on-demand
    internal const UInt64 GFXGridOffset = 0x140900; // OK  (int32)
    // ^ [ground seems be in 00730XXX range, cliffs: 00201XXX, walls: 000C0XXX, visible cliffside: 003XXXXX, stoneresource: 000C00XX,
    // ^ sea: 00A60XXX, beach-to-sea: 000200XX, sea-edge: 000001XX, river: 00050XXX, hole-building: 00060XXX, building: 00070XXX, chapel1:00310XXX,
    // ^ tower:003100XX, workshop: 000800XX, keep: 003402XX, goodsyard: 000F000X, goodsyard-midsection: 000C0061]
    // ^ This seems directly related to rendering in relation to the spriteatlas.
    internal const UInt64 AlphaGFXGridOffset = 0x279D80; // OK  (int32)
    internal const UInt64 ConstructionGridOffset = 0x3B3200; // OK  (int32)
    internal const UInt64 PillarGFXGridOffset = 0x4EC680; // OK (int32) (dynamic, note: atlas related? Mostly changing for water, uneven terrain and cliffs)
    internal const UInt64 WallGFXGridOffset = 0x625B00; // OK  (int32)
    internal const UInt64 UnknownGrid_0x75EF80_Offset = 0x75EF80; // TODO  (int16)
    internal const UInt64 TileRandomNoiseGridOffset = 0x7FB5C0; // OK  (uint16)
    internal const UInt64 LogicGridOffset = 0x898400; // OK   (int32)
    internal const UInt64 Logic2GridOffset = 0x9D2500; // OK   (byte)
    internal const UInt64 UnknownGridReal2Offset = 0xA20D40; // TODO (byte)
    internal const UInt64 OrganismGridOffset = 0xA6F260; // OK   (uint16)
    internal const UInt64 StructureGridOffset = 0xB0BCA0; // OK   (int16)
    internal const UInt64 StructureWasGridOffset = 0xBA86E0; // OK   (byte)
    internal const UInt64 TileUnitIdGridOffset = 0xBF6C00; // OK   (int16)
    internal const UInt64 FlyGridOffset = 0xC93640; // OK  (int16) (note: contains all projectile ids on the tiles they are currently at. Some decorational items count as well as "projectiles" such as braziers)
    internal const UInt64 UnknownGrid_0xD30080_Offset = 0xD30080; // TODO  (byte)
    internal const UInt64 HeightGridOffset = 0xD7E5A0; // OK   (byte)
    internal const UInt64 DefaultHeightGridOffset = 0xDCCAC0; // OK   (byte) 
    internal const UInt64 WallOwnerGridOffset = 0xE1AFE0; // OK   (byte)
    internal const UInt64 LuminesenceGridOffset = 0xE69500; // OK  (byte)  (note: seems related to shadow-maps of buildings or misc stuff)
    internal const UInt64 ShowHiGridOffset = 0xEB7A20; // OK (byte)  (dynamic, aka show_hi_layer)
    // ^ Setting to 0 causes UVs to be flipped? 
    internal const UInt64 MiscDisplayGridOffset = 0xF05F40; // OK (uint16) (dynamic, aka misc_display_layer)
    // ^ flat-ground: 16, buildings: 2048, building-edge: 2056, keep-edge: 6148, rocks/boulders: 2064
    // ^ additionally, values such as 0, 1, 2, 17, 18, 2050, 2064, 2065, 2066 can be found? (Bitflags likely, again)
    // ^ also seems to be 0 for the "Background-edge" of certain tall objects, (walls, etc)
    // ^, wrong values cause the entire tile to stop being rendered.
    // ^ also takes in account braziers? wall + brazier = 0x1812, without brazier = 0x812
    internal const UInt64 DamageGridOffset = 0xFA2980; // OK   (byte)
    internal const UInt64 MacroGridOffset = 0xFF0EA0; // OK  (int16)
    internal const UInt64 PathConnectionGridOffset = 0x108D8E0;// OK   (uint16) (note: counts (+1) isolated pockets of map regions aka inaccessible = pocket from the TOP RIGHT of the map;
    internal const UInt64 PathEdgeMaskGridOffset = 0x112A320;// OK (byte)
    // ^ not sure for what purpose tho) [UCP equivalent.: https://github.com/sourcehold/sourcehold-maps/wiki/Section-1021]
    internal const UInt64 OccupancyGridOffset = 0x1178840;// IOK (byte)  (note: describes tiles where a player unit stands, bitflag-based playerid description.)
    // ^ Player1 = 1, Player2 = 2, Player3 = 4, Player4 = 8, Player5 = 16, Player6 = 32, Player7 = 64, Player8 = 128
    internal const UInt64 AIZoneGridOffset = 0x13001E0;// TODO (byte)  (note: nothing?)
    internal const UInt64 UnknownGrid_0x134E700_Offset = 0x134E700;// TODO  (byte)
    internal const UInt64 AIDangerGridOffset = 0x139CC20;// OK  (byte)
    // ^ seems to be like so: after each time a unit dies as far as the game is concerned, there seems to spawn a 3x3 grid around the death location (post corpse despawn)
    // that is additive to previous deaths in the same location:
    // +1 +1 +1 +1 +1
    // +1 +2 +2 +2 +1
    // +1 +2 +3 +2 +1
    // +1 +2 +2 +2 +1
    // +1 +1 +1 +1 +1
    // where 3 = unit exact death position.
    // So if 10 units die at some X Y coordinate, the center of that would be (3*10=30)
    // It is not known what purpose this serves.
    internal const UInt64 UnknownGrid_0x13EB140_Offset = 0x13EB140;// TODO  (byte) (note: seems to be related to pathing of units.
    // ^ perhaps some pathfinding optimization where subsequent units with same / close target with follow the unit? aka SupComRTS level pathfinding.
    // none seems to be 0, and 1 - 6 seem to be non-identifiers but just pathmarkers.
    // apparently not all units draw such a line. Units that do are:
    // Arab. Horsearcher, Arab Firethrower: 6
    // No other known unit draws these lines. These also decay after some time from 6 -> 5 -> 4 -> 3 -> 2 -> 1 -> 0
    // It is not known what purpose this serves.
    internal const UInt64 UnknownStruct_0x16FA480_Offset = 0x16FA480;// TODO  (byte[18] or struct) Size is 5.774.400
    internal const UInt64 AIVBlockGridOffset = 0x1C7C0C0;// NEW  (byte) (note: AIV Related. Shows a per-ai number for the entire layout of a AIV)
    // ^ data format is same as TileUnitPresenceMaskGrid: CompactPlayerBitMask
    // ^ 0 = Player or none? 
    internal const UInt64 DelayGridOffset = 0x1CCBEF0;// OK (byte)  aka delay_layer
    // ^ For every object (non entity) it goes upwards towards the top left growing from the left and top edge of the building.
    // ^ a 1x1 tree would produce this: (adjusted for isometric view)
    //   5 5
    //   5 4 4
    //     4 3 3
    //       3 2 2
    //         2 1 1
    //           1 t
    // where t is tree.
    // interestingly, some vegetation such as small ones (cacti) are excempt from this completely.
    // But all of the objects that do generate this, have 5 "layers" of this.
    // It is not known what purpose this serves.
    internal const UInt64 GatePathGridOffset = 0x1D1A730;// OK  (byte)
    internal const UInt64 UnknownGrid_0x1D68F70_Offset = 0x1D68F70;// TODO (int32)
    internal const UInt64 MoatWorkTaskIndexGridOffset = 0x1EA23F0;// OK (uint16, packed 320800-tile grid)
    internal const UInt64 MoatWorkTaskSlotsOffset = 0x1F3EE30;// OK (MoatWorkTask[64000])
    internal const UInt64 MoatWorkTaskSlotLimitOffset = 0x2038E30;// OK (int32 exclusive high-water mark)

    internal const UInt64 BlockPlacementOffset = 0x204E6FC;

    internal const UInt64 PlaceableWallsAmountOffset = 0x204E758;

    internal const UInt64 CurrentMapSizeOffset = 0x204E7E4;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTileManagerView"/> class.
    /// </summary>
    /// <param name="fromAddress">The starting memory address of the game's tile manager structure.</param>
    public GameTileManagerView(UInt64 fromAddress)
    {
        _ptr = (void*)fromAddress;
    }

    /// <summary>
    /// Is the current building placement blocked or not.
    /// </summary>
    public bool IsPlacementBlocked
    {
        get
        {
            UInt16 value = *(UInt16*)((UInt64)_ptr + BlockPlacementOffset);
            return value == 1;
        }
        set
        {
            *(UInt16*)((UInt64)_ptr + BlockPlacementOffset) = value == true ? (ushort)1 : (ushort)0;
        }
    }

    public bool UsePlacementBlockedOverride { get; set; } = false;
    public bool PlacementBlockedOverrideValue { get; set; } = false;

    /// <summary>
    /// The current amount of walls that the player is able to place.
    /// For use in short context windows.
    /// </summary>
    public int PlaceableWallsAmount
    {
        get
        {
            return *(int*)((UInt64)_ptr + PlaceableWallsAmountOffset);
        }
        set
        {
            *(int*)((UInt64)_ptr + PlaceableWallsAmountOffset) = value;
        }
    }

    /// <summary>
    /// The current map size.
    /// </summary>
    public int CurrentMapSize
    {
        get
        {
            return *(int*)((UInt64)_ptr + CurrentMapSizeOffset);
        }
    }

    /// <summary>
    /// aka pillar_gfx_layer
    /// </summary>
    public Span<int> PillarGFXGrid
    {
        get
        {
            return new Span<int>((void*)((UInt64)_ptr + PillarGFXGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing an unknown grid of 16-bit integer data.
    /// </summary>
    public Span<byte> UnknownGrid2
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + UnknownGridReal2Offset), MapSize);
        }
    }

    /// <summary>
    /// aka show_hi_layer
    /// </summary>
    public Span<byte> ShowHiGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + ShowHiGridOffset), MapSize);
        }
    }

    /// <summary>
    /// aka misc_display_layer
    /// </summary>
    public Span<UInt16> MiscDisplayGrid
    {
        get
        {
            return new Span<UInt16>((void*)((UInt64)_ptr + MiscDisplayGridOffset), MapSize);
        }
    }
    /// <summary>
    /// This grid is a flood-fill reachability index, essentially a connected-component label grid computed from the top-right corner of the map. 
    /// Each tile gets assigned the ID of which "pocket" (isolated walkable region) it belongs to, where the counting/flood-fill originates from the top-right.
    /// aka path_connection_layer
    /// </summary>
    public Span<UInt16> PathConnectionGrid
    {
        get
        {
            return new Span<UInt16>((void*)((UInt64)_ptr + PathConnectionGridOffset), NativePackedTileCapacity);
        }
    }

    public Span<byte> PathEdgeMaskGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + PathEdgeMaskGridOffset), NativePackedTileCapacity);
        }
    }

    /// <summary>
    /// Describes the direct path for gates (entry -> exit /vice-versa)
    /// </summary>
    public Span<byte> GatePathGrid
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + GatePathGridOffset), NativePackedTileCapacity); }
    }

    /// <summary>
    /// Seems to describe the player location + margin with "1" and all other areas with "0"
    /// aka ai_zone_layer
    /// </summary>
    public Span<byte> AIZoneGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + AIZoneGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Bitfield presence/occupancy mask, each bit represents one of the 8 players, and the byte on a tile encodes which players currently have a unit standing on that tile.
    /// For example, if Player1 and Player2 have a unit on Tile 400, 400, the grid index for that tile would read (1 | 2 = 3)
    /// aka occupancy_layer
    /// </summary>
    public Span<CompactPlayerBitMask> OccupancyGrid
    {
        get
        {
            return new Span<CompactPlayerBitMask>((void*)((UInt64)_ptr + OccupancyGridOffset), MapSize);
        }
    }

    /// <summary>
    /// aka delay_layer
    /// </summary>
    public Span<byte> DelayGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + DelayGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of tile property flags.
    /// formerly known as PropertyFlagsGrid
    /// aka logic_layer
    /// </summary>
    /// <remarks>
    /// Each value is a bitfield corresponding to <see cref="TilePropertyFlag"/>, defining the physical properties
    /// of the tile such as collision, resource type, and buildability.
    /// </remarks>
    public Span<Int32> LogicGrid
    {
        get
        {
            return new Span<Int32>((void*)((UInt64)_ptr + LogicGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of vegetation types.
    /// formerly known as TileVegetationIdLookup
    /// aka organism_layer
    /// </summary>
    /// <remarks>
    /// Each value corresponds to a <see cref="VegetationType"/>, defining the specific type of tree or shrub on the tile.
    /// This is only relevant if the tile's property flags indicate the presence of vegetation.
    /// </remarks>
    public Span<UInt16> OrganismGrid
    {
        get
        {
            return new Span<UInt16>((void*)((UInt64)_ptr + OrganismGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of visual tile types.
    /// formerly known as TileTypeGrid
    /// aka logic2_layer
    /// </summary>
    /// <remarks>
    /// Each value corresponds to a <see cref="TileType"/>, defining the visual texture of the tile (e.g., dirt, sand, grass).
    /// </remarks>
    public Span<byte> Logic2Grid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + Logic2GridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of wall tile ownership.
    /// aka wall_owner_layer
    /// </summary>
    /// <remarks>
    /// Each value is the ID of the player who owns the corresponding tile.
    /// </remarks>
    public Span<byte> WallOwnerGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + WallOwnerGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of building IDs.
    /// aka structure_layer
    /// </summary>
    /// <remarks>
    /// Each value is the ID of the building occupying the corresponding tile. A value of 0 indicates no building.
    /// </remarks>
    public Span<UInt16> StructureGrid
    {
        get
        {
            return new Span<UInt16>((void*)((UInt64)_ptr + StructureGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of unit IDs.
    /// </summary>
    /// <remarks>
    /// Each value is the ID of the unit occupying the corresponding tile. A value of 0 indicates no unit.
    /// </remarks>
    public Span<UInt16> TileUnitIdGrid
    {
        get
        {
            return new Span<UInt16>((void*)((UInt64)_ptr + TileUnitIdGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of secondary tile states.
    /// formerly known as TileStateGrid
    /// aka damage_layer
    /// </summary>
    /// <remarks>
    /// Used for information such as growth progress for farm plots or signaling a damaged wall (cannot be walked on), etc.
    /// </remarks>
    public Span<byte> DamageGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + DamageGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing the grid of tile height levels.
    /// aka height_layer
    /// </summary>
    /// <remarks>
    /// Each value represents the discrete elevation level of the corresponding tile.
    /// </remarks>
    public Span<byte> HeightGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + HeightGridOffset), MapSize);
        }
    }

    /// <summary>
    /// Gets a span representing an the default height of some tiles (Re-used for Default health for Wall-tiles)
    /// aka default_height_layer
    /// </summary>
    public Span<byte> DefaultHeightGrid
    {
        get
        {
            return new Span<byte>((void*)((UInt64)_ptr + DefaultHeightGridOffset), MapSize);
        }
    }

    /// <summary>
    /// aka gfx_layer
    /// </summary>
    public Span<Int32> GFXGrid
    {
        get { return new Span<Int32>((void*)((UInt64)_ptr + GFXGridOffset), MapSize); }
    }

    /// <summary>
    /// A per-tile grid storing a packed 32-bit sprite descriptor for each occupied building tile.
    /// The high 16 bits contain the GM file index (see Enums.GM), identifying the sprite atlas.
    /// The low 16 bits contain the image index within that atlas.
    /// aka alpha_gfx_layer
    /// </summary>
    public Span<Int32> AlphaGFXGrid
    {
        get { return new Span<Int32>((void*)((UInt64)_ptr + AlphaGFXGridOffset), MapSize); }
    }

    /// <summary>
    /// aka construction_layer
    /// </summary>
    public Span<Int32> ConstructionGrid
    {
        get { return new Span<Int32>((void*)((UInt64)_ptr + ConstructionGridOffset), MapSize); }
    }

    /// <summary>
    /// aka wall_gfx_layer
    /// </summary>
    public Span<Int32> WallGFXGrid
    {
        get { return new Span<Int32>((void*)((UInt64)_ptr + WallGFXGridOffset), MapSize); }
    }

    /// <summary>
    /// Unknown Int16 Grid (Offset 0x75EF80)
    /// </summary>
    public Span<Int16> UnknownGrid_0x75EF80
    {
        get { return new Span<Int16>((void*)((UInt64)_ptr + UnknownGrid_0x75EF80_Offset), MapSize); }
    }

    /// <summary>
    /// Random Noise Variation Grid (Offset 0x7FB5C0).
    /// </summary>
    public Span<UInt16> TileRandomNoiseGrid
    {
        get { return new Span<UInt16>((void*)((UInt64)_ptr + TileRandomNoiseGridOffset), MapSize); }
    }

    /// <summary>
    /// Describes a Building eStruct enum for a building occupying a tile.
    /// aka structure_was_layer
    /// </summary>
    public Span<byte> StructureWasGrid
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + StructureWasGridOffset), MapSize); }
    }

    /// <summary>
    /// Describes projectile ids over certain tiles. Decorational items such as braziers count in this as well (+ flags)
    /// Birds etc count in this as well.
    /// aka fly_layer
    /// </summary>
    public Span<Int16> FlyGrid
    {
        get { return new Span<Int16>((void*)((UInt64)_ptr + FlyGridOffset), MapSize); }
    }

    /// <summary>
    /// Unknown Byte Grid (Offset 0xD30080)
    /// </summary>
    public Span<byte> UnknownGrid_0xD30080
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + UnknownGrid_0xD30080_Offset), MapSize); }
    }

    /// <summary>
    /// Describes the shadow thrown by certain structures onto surrounding tiles, or light level. Idk.
    /// aka luminesence_layer
    /// </summary>
    public Span<byte> LuminesenceGrid
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + LuminesenceGridOffset), MapSize); }
    }

    /// <summary>
    /// aka macro_layer
    /// </summary>
    public Span<Int16> MacroGrid
    {
        get { return new Span<Int16>((void*)((UInt64)_ptr + MacroGridOffset), MapSize); }
    }

    /// <summary>
    /// Unknown Byte Grid (Offset 0x134E700)
    /// </summary>
    public Span<byte> UnknownGrid_0x134E700
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + UnknownGrid_0x134E700_Offset), MapSize); }
    }

    /// <summary>
    /// aka ai_danger_layer
    /// </summary>
    public Span<byte> AIDangerGrid
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + AIDangerGridOffset), MapSize); }
    }

    /// <summary>
    /// Unknown Byte Grid (Offset 0x13EB140)
    /// </summary>
    public Span<byte> UnknownGrid_0x13EB140
    {
        get { return new Span<byte>((void*)((UInt64)_ptr + UnknownGrid_0x13EB140_Offset), MapSize); }
    }

    /// <summary>
    /// Describes tiles which are part of a AIV layout that are owned by the specified player id(s) via mask.
    /// aka aiv_block_layer
    /// </summary>
    public Span<CompactPlayerBitMask> AIVBlockGrid
    {
        get { return new Span<CompactPlayerBitMask>((void*)((UInt64)_ptr + AIVBlockGridOffset), MapSize); }
    }

    /// <summary>
    /// Unknown Int32 Grid (Offset 0x1D68F70)
    /// </summary>
    public Span<Int32> UnknownGrid_0x1D68F70
    {
        get { return new Span<Int32>((void*)((UInt64)_ptr + UnknownGrid_0x1D68F70_Offset), MapSize); }
    }

    /// <summary>
    /// Per-tile moat-work task index grid (Offset 0x1EA23F0).
    /// Zero means that the packed tile has no active moat-work task.
    /// </summary>
    public Span<UInt16> MoatWorkTaskIndexGrid
    {
        get { return new Span<UInt16>((void*)((UInt64)_ptr + MoatWorkTaskIndexGridOffset), NativePackedTileCapacity); }
    }

    /// <summary>
    /// Live moat-work task slot storage. Slot zero is the no-task sentinel.
    /// </summary>
    public Span<MoatWorkTask> MoatWorkTasks
    {
        get { return new Span<MoatWorkTask>((void*)((UInt64)_ptr + MoatWorkTaskSlotsOffset), MoatWorkTaskSlotCapacity); }
    }

    internal MoatWorkTask* MoatWorkTaskSlotsPointer
    {
        get { return (MoatWorkTask*)((UInt64)_ptr + MoatWorkTaskSlotsOffset); }
    }

    /// <summary>
    /// Live exclusive high-water mark used when scanning <see cref="MoatWorkTasks"/>.
    /// Writing this value without updating the task/index lifecycle can corrupt moat work state.
    /// </summary>
    public ref Int32 MoatWorkTaskSlotLimit
    {
        get { return ref *(Int32*)((UInt64)_ptr + MoatWorkTaskSlotLimitOffset); }
    }
}
