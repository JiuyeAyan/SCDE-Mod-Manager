using System;

namespace SHCDESE.Interop.Enums;

/// <summary>
/// Represents the state of a single tile in Stronghold Crusader, based on reverse-engineered property values.
/// Can be treated as a bit field for combined states.
/// </summary>
[Flags]
public enum TilePropertyFlag : UInt32
{
    // ===============================================================================================
    // ATOMIC FLAGS (Core building blocks based on individual bits)
    // These are the fundamental properties that can be combined to describe any tile.
    // ===============================================================================================

    /// <summary>
    /// Represents a tile with non-standard height properties. (0x00000000)
    /// </summary>
    /// <remarks>
    /// Initially thought to be related to height, this seems to apply to tiles with a varying elevation modifier, like "mountains", which often look like coarse sand.
    /// It does not appear to be related to the absolute Y-level, as Y1 and Y2 tiles do not share this property. The exact meaning is unclear, but it may indicate non-standard geometry.
    /// </remarks>
    None = 0,

    // --- Low-order Bits ---

    /// <summary>
    /// Sea or Ocean tile. Impassable and Unbuildable. (0x00000001)
    /// </summary>
    Sea = 1 << 0,

    /// <summary>
    /// A component specific to Goodsyard (Stockpile) tiles. (0x00000002)
    /// </summary>
    GoodsyardRelated = 1 << 1,

    /// <summary>
    /// A component specific to all farm plot tiles. (0x00000004)
    /// </summary>
    IsFarm = 1 << 2,

    /// <summary>
    /// A placed pitch trap tile, laid by an engineer. (0x00000008)
    /// </summary>
    PitchTrap = 1 << 3,

    /// <summary>
    /// The true map border (out of screen usually, directly behind MapBorder). (0x00000010)
    /// </summary>
    RealityEdge = 1 << 4,

    /// <summary>
    /// The map border (out of screen usually). (0x00000020)
    /// </summary>
    MapBorder = 1 << 5,

    // << 6 ?

    /// <summary>
    /// An impassable edge. (0x00000080)
    /// </summary>
    /// <remarks>
    /// Used for the cliff-edge of an elevated area or as part of Rock-tiles or just general tiles that are impassable.
    /// Also used for Tree Tiles.
    /// </remarks>
    ImpassableEdge = 1 << 7,

    // --- Building & Structure Bits ---

    /// <summary>
    /// Base flag for walls. This tile is impassable. (0x00000100)
    /// </summary>
    IsWall = 1 << 8,

    /// <summary>
    /// A component of crenelated walls. (0x00000200)
    /// </summary>
    CrenelationComponent = 1 << 9,

    /// <summary>
    /// Occupied by a building structure. This tile is impassable. (0x00000400)
    /// </summary>
    IsBuilding = 1 << 10,

    /// <summary>
    /// A component of stairs. (0x00000800)
    /// </summary>
    IsStairs = 1 << 11,

    /// <summary>
    /// A tree or other vegetation. (0x00001000)
    /// </summary>
    IsTree = 1 << 12,

    /// <summary>
    /// Reserved space around a tree. (0x00002000)
    /// </summary>
    TreeProximity = 1 << 13,

    /// <summary>
    /// A tile designated for digging a moat. (0x00004000)
    /// </summary>
    PlannedMoat = 1 << 14,

    /// <summary>
    /// Base flag for most passable land tiles. (0x00008000)
    /// </summary>
    IsLand = 1 << 15,

    // --- High-order Bits ---

    /// <summary>
    /// Modifier for low-height walls. (0x00010000)
    /// </summary>
    IsLowWall = 1 << 16,

    /// <summary>
    /// Contains stone resource. (0x00020000)
    /// </summary>
    HasStone = 1 << 17,

    // 1 << 18?

    /// <summary>
    /// Contains iron ore resource. (0x00080000)
    /// </summary>
    HasIron = 1 << 19,

    /// <summary>
    /// A river tile. Impassable and Unbuildable. (0x00100000)
    /// </summary>
    River = 1 << 20,

    /// <summary>
    /// A ford tile. Passable and Unbuildable. (0x00200000)
    /// </summary>
    Ford = 1 << 21,

    /// <summary>
    /// A modifier for crenelated walls. (0x00400000)
    /// </summary>
    CrenelationModifier = 1 << 22,

    // 1 << 23?

    /// <summary>
    /// A wheat farm tile. (0x01000000)
    /// </summary>
    IsWheat = 1 << 24,

    /// <summary>
    /// A hops farm tile. (0x02000000)
    /// </summary>
    IsHops = 1 << 25,

    /// <summary>
    /// An apple tree from an apple farm. (0x04000000)
    /// </summary>
    IsAppleFarm = 1 << 26,

    /// <summary>
    /// A fence tile for a farm. (0x08000000)
    /// </summary>
    IsFarmFence = 1 << 27,

    /// <summary>
    /// The roof or top of a tower or the keep. This tile is impassable. (0x10000000)
    /// </summary>
    IsElevated = 1 << 28,

    /// <summary>
    /// A swamp tile. Passable but Unbuildable. (0x20000000)
    /// </summary>
    IsSwamp = 1 << 29,

    /// <summary>
    /// A dug moat tile. (0x40000000)
    /// </summary>
    IsMoat = 1 << 30,

    /// <summary>
    /// A Pitch resource tile. (0x80000000)
    /// </summary>
    IsPitch = 1u << 31,


    // ===============================================================================================
    // COMPOSITE VALUES
    // They are defined as combinations of the atomic flags above.
    // ===============================================================================================

    /// <summary>
    /// A free, empty tile. (0x00008000)
    /// </summary>
    /// <remarks>
    /// Examples: Dirt, Coarse Sand.
    /// </remarks>
    Free = IsLand,

    /// <summary>
    /// A tile occupied by a building. Impassable. (0x00008400)
    /// </summary>
    /// <remarks>
    /// Examples: Hovel, Church.
    /// </remarks>
    BuildingOccupied = IsLand | IsBuilding,

    /// <summary>
    /// A tile containing stone resource. Passable and Buildable. (0x00028000)
    /// </summary>
    StoneResource = IsLand | HasStone,

    /// <summary>
    /// A tile belonging to the Goodsyard (Stockpile). Passable but Unbuildable. (0x00000502)
    /// </summary>
    Goodsyard = GoodsyardRelated | IsWall | IsBuilding,

    /// <summary>
    /// A connection tile for the Goodsyard (Stockpile). Passable but Unbuildable. (0x00000102)
    /// </summary>
    GoodsyardConnection = GoodsyardRelated | IsWall,

    /// <summary>
    /// A low-height wall. Impassable. (0x00010100)
    /// </summary>
    LowWall = IsLowWall | IsWall,

    /// <summary>
    /// A normal-height wall. Impassable. (0x00000100)
    /// </summary>
    /// <remarks>
    /// Examples: Standard stone walls, all gatehouses.
    /// </remarks>
    NormalWall = IsWall,

    /// <summary>
    /// A crenelated wall. Impassable. (0x00400300)
    /// </summary>
    CrenelatedWall = CrenelationModifier | CrenelationComponent | IsWall,

    /// <summary>
    /// A flight of stairs on a wall or tower. Impassable. (0x00000900)
    /// </summary>
    Stairs = IsStairs | IsWall,

    /// <summary>
    /// An elevated position on a structure. Impassable. (0x10008000)
    /// </summary>
    /// <remarks>
    /// This flag is typically found on the roof tiles of structures.
    /// Examples: Towers, Keep.
    /// </remarks>
    ElevatedPosition = IsElevated | IsLand,

    /// <summary>
    /// Reserved space in the proximity of a tree. (0x0000A000)
    /// </summary>
    /// <remarks>
    /// This is the square around a tree where other trees cannot be built, but it has no collider for ordinary buildings.
    /// </remarks>
    TreeProximitySpace = IsLand | TreeProximity,

    /// <summary>
    /// A space occupied by vegetation. (0x0000B000)
    /// </summary>
    /// <remarks>
    /// Example: A tree.
    /// </remarks>
    Vegetation = IsLand | TreeProximity | IsTree,

    /// <summary>
    /// A tile containing iron ore resource. Passable. (0x00088000)
    /// </summary>
    IronResource = IsLand | HasIron,

    /// <summary>
    /// A swamp tile. Passable but Unbuildable. (0x20008000)
    /// </summary>
    Swamp = IsLand | IsSwamp,

    /// <summary>
    /// A pitch resource tile. Passable. (0xA0008000)
    /// </summary>
    /// <remarks>
    /// This tile is Unbuildable, with the specific exception of the Pitch Rig (also known as Tar-Refinery).
    /// </remarks>
    PitchResource = IsLand | IsSwamp | IsPitch,

    /// <summary>
    /// A moat that has been dug. (0x40008000)
    /// </summary>
    /// <remarks>
    /// Created by units like Spearmen.
    /// </remarks>
    Moat = IsLand | IsMoat,

    /// <summary>
    /// A tile designated to become a moat. (0x0000C000)
    /// </summary>
    /// <remarks>
    /// This marks where a unit is assigned to dig.
    /// </remarks>
    MoatPlanned = IsLand | PlannedMoat,

    /// <summary>
    /// A fence tile belonging to a farm. (0x08008004)
    /// </summary>
    /// <remarks>
    /// Example: The fence of a Dairy (cow/cheese) Farm.
    /// </remarks>
    FarmFenceTile = IsFarm | IsLand | IsFarmFence,

    /// <summary>
    /// A wheat tile belonging to a Wheat Farm. (0x01008004)
    /// </summary>
    WheatTile = IsFarm | IsLand | IsWheat,

    /// <summary>
    /// A hops tile belonging to a Hops Farm. (0x02008004)
    /// </summary>
    HopsTile = IsFarm | IsLand | IsHops,

    /// <summary>
    /// An apple tree tile belonging to an Apple Orchard. (0x0400B004)
    /// </summary>
    AppleTreeTile = IsFarm | IsLand | TreeProximity | IsTree | IsAppleFarm
}