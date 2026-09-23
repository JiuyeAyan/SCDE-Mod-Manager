using MessagePack;
using Noesis;
using R3;
using RedBird.Core.Memory;
using SHCDESE.API.Components.MapEditor;
using SHCDESE.API.Components.Network;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using SHCDESE.Extensions;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with the games map and tile data.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for querying and modifying
/// individual tile properties, such as height, ownership, and physical flags. It uses a
/// <para><see cref="GameTileManagerView"/> to directly access game memory.</para>
/// <para>TileID / XY Cheat Sheet:</para>
/// <para>TileID = X + MapSize.X * Y </para>
/// <para>X = TileID / MapSize.X </para>
/// <para>Y = TileID % MapSize.X</para>
/// </remarks>
[LuaApiNamespace("Tile")]
public unsafe sealed class GameTileManagerAPI
{
#pragma warning disable 0618
    private static readonly Lazy<GameTileManagerAPI> _lazy = new(() => new GameTileManagerAPI());
    public static GameTileManagerAPI Instance => _lazy.Value;

    /// <summary>
    /// The maximum width of the game map in tiles.
    /// </summary>
    public const int MAX_WIDTH = 800;


    /// <summary>
    /// The maximum height of the game map in tiles.
    /// </summary>
    public const int MAX_HEIGHT = 800;

    /// <summary>
    /// The amount of retries during retrieval of a random tile
    /// </summary>
    private const int RANDOM_TILE_SELECTION_RETRY_AMOUNT = 30;

    private static readonly MessagePackSerializerOptions SemaSerializerOptions =
        MessagePackSerializerOptions.Standard.WithSecurity(MessagePackSecurity.UntrustedData);

    /// <summary>
    /// Gets the underlying view that provides direct memory access to the tile data grids.
    /// </summary>
    public GameTileManagerView TileManager { get; private set; }

    /// <summary>
    /// The core TileManager structure in memory.
    /// </summary>
    private readonly UInt64 _tileManagerVA = 0;
    private UInt32* _currentMapSize = null;
    private IntPtr _gp_pathfindingContext;

    /// <summary>
    /// A pointer to the games internal map row lookup table (LUT) used to convert 2D tile coordinates (X, Y) into a 1D array index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Theory:</b><br/>
    /// The game uses an isometric map projection, not a simple top-down grid. Due to the staggered, diamond-like layout
    /// of an isometric map, the standard formula for calculating a tile's index (`index = y * width + x`) is not applicable.
    /// Calculating the correct offset for each row on-the-fly would be computationally expensive and too slow for real-time access.
    /// </para>
    /// <para>
    /// <b>Functionality:</b><br/>
    /// To solve this performance issue, the game engine pre-calculates the starting index for every single map row (from Y=0 to Y=799). 
    /// These starting indices are stored in this lookup table. This turns a complex geometric calculation
    /// into a single, highly-performant memory lookup, which is a critical optimization for all tile data access.
    /// </para>
    /// <para>
    /// <b>Usage in Formulas:</b><br/>
    /// This table is used in functions like <see cref="GetTileId(int, int)"/> with the formula:
    /// <code>index = tileX + MapRowLookupTable[3 * tileY]</code>
    /// In this formula, `MapRowLookupTable[3 * tileY]` instantly retrieves the starting index of the desired row `tileY`. The `tileX` value is then simply added as an offset to pinpoint the exact tile within that row.
    /// </para>
    /// <para>
    /// <b>Memory Layout and the `* 3` Multiplier:</b><br/>
    /// The multiplication by 3 indicates that the table is not a simple array of integers. Instead, it is an array of structures,
    /// where each structure contains 3 integers (12 bytes) for each map row. The formula `[3 * tileY]` is pointer arithmetic that accesses the
    /// <b>first</b> integer in the 3-integer block for the given `tileY`, which is the row's starting index. The other two integers
    /// per row are used for other internal engine purposes, which are not as of yet documented.
    /// </para>
    /// </remarks>
    /// <seealso cref="GetTileId(int, int)"/>

    public int* MapRowLookupTable = null;

    /// <summary>
    /// Used to retrieve Tile-Y from TileId
    /// Same deal as <see cref="MapRowLookupTable"/>
    /// </summary>
    public UInt16* MapColumnLookupTable = null;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameTileManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameTileManagerAPI()
    {
        _tileManagerVA = GameGlobalsManager.Instance.GameTileManagerVA;

        TileManager = new GameTileManagerView(_tileManagerVA);
        MapRowLookupTable = (int*)(GameGlobalsManager.Instance.MapRowLookupTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle);
        MapColumnLookupTable = (UInt16*)(GameGlobalsManager.Instance.MapColumnLookupTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle);
        _currentMapSize = (UInt32*)(GameGlobalsManager.Instance.CurrentMapSizeVA);
        _gp_pathfindingContext = (IntPtr)(GameGlobalsManager.Instance.PathfindingContextVA);

        LogHelper.Information($"TileManager: {_tileManagerVA.ToString("X16")}");
        LogHelper.Information($"TileHeightGridVA: {(_tileManagerVA + GameTileManagerView.HeightGridOffset).ToString("X16")}");
        LogHelper.Information($"TilePropertyFlagsVA: {(_tileManagerVA + GameTileManagerView.LogicGridOffset).ToString("X16")}");
        LogHelper.Information($"VegetationTypeLookupVA: {(_tileManagerVA + GameTileManagerView.OrganismGridOffset).ToString("X16")}");
        LogHelper.Information($"TilePlayerOwnerIdVA: {(_tileManagerVA + GameTileManagerView.WallOwnerGridOffset).ToString("X16")}");
        LogHelper.Information($"TileBuildingIdGridVA: {(_tileManagerVA + GameTileManagerView.StructureGridOffset).ToString("X16")}");
        LogHelper.Information($"TileUnitIdGridVA: {(_tileManagerVA + GameTileManagerView.TileUnitIdGridOffset).ToString("X16")}");
        LogHelper.Information($"TileStateGridVA: {(_tileManagerVA + GameTileManagerView.DamageGridOffset).ToString("X16")}");
        LogHelper.Information($"TileTypeVA: {(_tileManagerVA + GameTileManagerView.Logic2GridOffset).ToString("X16")}");
        LogHelper.Information($"_currentMapSize: {new IntPtr(_currentMapSize).ToString("X16")}");
        LogHelper.Information($"_gp_pathfindingContext: {_gp_pathfindingContext.ToString("X16")}");
    }

    /// <summary>
    /// Get the TileManager Virtual Address
    /// (Internal use only)
    /// </summary>
    /// <returns>Tile Manager VA</returns>
    public IntPtr GetTileManager()
    {
        return (IntPtr)_tileManagerVA;
    }

    /// <summary>
    /// Get the (maybe) PathfindingContext Virtual Address
    /// (Internal use only)
    /// </summary>
    /// <returns>PathfindingContext VA</returns>
    internal IntPtr GetPathfindingContext()
    {
        return _gp_pathfindingContext;
    }

    #region Live native arrays

    /// <summary>
    /// The returned spans are direct writable views of the native TileManager arrays.
    /// They must not be retained across a game or map lifecycle transition.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetPackedNeighborTileDeltas() => TileManager.PackedNeighborTileDeltas;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetPackedRowTileCounts() => TileManager.PackedRowTileCounts;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int16> GetPackedTileCoordinateLookup0() => TileManager.PackedTileCoordinateLookup0;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int16> GetPackedTileCoordinateLookup1() => TileManager.PackedTileCoordinateLookup1;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetGfxLayer() => TileManager.GFXGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetAlphaGfxLayer() => TileManager.AlphaGFXGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetConstructionGfxLayer() => TileManager.ConstructionGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetPillarGfxLayer() => TileManager.PillarGFXGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetWallGfxLayer() => TileManager.WallGFXGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int16> GetFloatingLayer() => TileManager.FloatingGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetRandomLayer() => TileManager.TileRandomNoiseGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetLogicLayer() => TileManager.LogicGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetLogic2Layer() => TileManager.Logic2Grid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetChangedLayer() => TileManager.ChangedGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetOrganismLayer() => TileManager.OrganismGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetStructureLayer() => TileManager.StructureGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetStructureWasLayer() => TileManager.StructureWasGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetChimpLayer() => TileManager.TileUnitIdGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int16> GetFlyLayer() => TileManager.FlyGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetHeightLayer() => TileManager.HeightGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDefaultHeightLayer() => TileManager.DefaultHeightGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetWallOwnerLayer() => TileManager.WallOwnerGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetLuminescenceLayer() => TileManager.LuminesenceGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetShowHiLayer() => TileManager.ShowHiGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetMiscDisplayLayer() => TileManager.MiscDisplayGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDamageLayer() => TileManager.DamageGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int16> GetMacroLayer() => TileManager.MacroGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetPathConnectionLayer() => TileManager.PathConnectionGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetPathLinkageLayer() => TileManager.PathEdgeMaskGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<CompactPlayerBitMask> GetOccupancyLayer() => TileManager.OccupancyGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetCertainPathLayer() => TileManager.CertainPathGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetWalkLayer() => TileManager.WalkGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetAiZoneLayer() => TileManager.AIZoneGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetAiInfoLayer() => TileManager.AIInfoGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetAiDangerLayer() => TileManager.AIDangerGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetAiProximityLayer() => TileManager.AIProximityGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownDzSpreadIdLayer() => TileManager.TownDzSpreadIdGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownNullConnectsLayer() => TileManager.TownNullConnectsGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownDzSpreadCountLayer() => TileManager.TownDzSpreadCountGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownStoneValueLayer() => TileManager.TownStoneValueGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownStructureLayer() => TileManager.TownStructureGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownOasisLayer() => TileManager.TownOasisGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownFarmLayer() => TileManager.TownFarmGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetTownIronLayer() => TileManager.TownIronGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetProblemBuildLayer() => TileManager.ProblemBuildGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<CompactPlayerBitMask> GetAIVBlockLayer() => TileManager.AIVBlockGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetAIVBlockZone() => TileManager.AIVBlockZone;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetDelayLayer() => TileManager.DelayGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetGatePathLayer() => TileManager.GatePathGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetMoatWorkTaskIndexLayer() => TileManager.MoatWorkTaskIndexGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<MoatWorkTask> GetMoatWorkTasks() => TileManager.MoatWorkTasks;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GamePitchDescriptor> GetPitchSlots() => TileManager.PitchSlots;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetPitchSlotLookup() => TileManager.PitchSlotLookup;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetLayerInvalidationPending() => TileManager.LayerInvalidationPending;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetRotatedDirectionMap() => TileManager.RotatedDirectionMap;

    #endregion

    #region Moat work tasks

    [LuaApiExport("GetMoatWorkTaskIndexAtTile")]
    public int GetMoatWorkTaskIndexAtTile(int tileX, int tileY)
    {
        return TryGetPackedTileId(tileX, tileY, out int tileId)
            ? GetMoatWorkTaskIndexLayer()[tileId]
            : 0;
    }

    [LuaApiExport("GetMoatWorkTaskSlotLimit")]
    public int GetMoatWorkTaskSlotLimit()
    {
        Int32 slotLimit = TileManager.MoatWorkTaskSlotLimit;
        return (UInt32)slotLimit <= GameTileManagerView.MoatWorkTaskSlotCapacity ? slotLimit : 0;
    }

    [LuaApiExport("GetMoatWorkTaskActiveCount")]
    public int GetMoatWorkTaskActiveCount()
    {
        Int32 activeCount = TileManager.MoatWorkTaskActiveCount;
        return (UInt32)activeCount <= GameTileManagerView.MoatWorkTaskSlotCapacity ? activeCount : 0;
    }

    public bool TryGetMoatWorkTaskByIndex(int taskIndex, out NativePointer<MoatWorkTask> moatWorkTask)
    {
        moatWorkTask = new NativePointer<MoatWorkTask>((MoatWorkTask*)null);
        Int32 slotLimit = GetMoatWorkTaskSlotLimit();
        if (taskIndex <= 0 || taskIndex >= slotLimit)
            return false;

        MoatWorkTask* task = TileManager.MoatWorkTaskSlotsPointer + taskIndex;
        if (task->r_OwnerPlayerId == 0)
            return false;

        moatWorkTask = new NativePointer<MoatWorkTask>(task);
        return true;
    }

    public bool TryGetMoatWorkTaskAtTile(int tileX, int tileY, out NativePointer<MoatWorkTask> moatWorkTask)
    {
        moatWorkTask = new NativePointer<MoatWorkTask>((MoatWorkTask*)null);
        if (!TryGetPackedTileId(tileX, tileY, out int tileId))
            return false;

        Int32 taskIndex = GetMoatWorkTaskIndexLayer()[tileId];
        if (!TryGetMoatWorkTaskByIndex(taskIndex, out moatWorkTask))
            return false;

        return moatWorkTask.Pointer->r_TileId == tileId;
    }

    #endregion

    /// <summary>
    /// There may be higher values idk
    /// 35 = UNKNOWN
    /// 34 = UNKNOWN
    /// ^ Quite colorful. No clue.
    /// 33 = PathConnectionGrid
    /// 32 = UNKNOWN
    /// 31 = town_iron
    /// 30 = town_farm
    /// 29 = town_oasis
    /// 28 = town_structure
    /// ^ Player and custom lords are respected.
    /// ^ Seems to describe locations of some buildings (lumberjacks, apple farms, quarries, etc)
    /// 27 = town_stone_value
    /// ^ Seems to describe locations of stone resources, perhaps higher value chunks = better location?
    /// 26 = town_dz_spread_count + 1
    /// 25 = town_null_connects + 1
    /// ^ Seems to describe many areas, but mostly "edges" along cliffs etc or unpassable terrain
    /// 24 = town_dz_spread_id + 1
    /// ^ Player and custom lords are respected.
    /// 23 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are respected.
    /// ^ Seemingly nothing else besides AIV layout?
    /// 22 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are respected.
    /// ^ Seemingly nothing else besides AIV layout?
    /// 21 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are respected.
    /// ^ Seemingly nothing else besides AIV layout?
    /// 20 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are respected.
    /// ^ Seemingly nothing else besides AIV layout?
    /// 19 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are respected.
    /// ^ Sparse chunks of "3" can be found in random locations, except for one consistency: rivers.
    /// ^ Rivers which are non walkable are covered in these "3" chunks. Perhaps some kind of "non-walkability" hint to the AI?
    /// 18 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are respected.
    /// ^ Player and misc area is identified by area filled with "3" with some weird chunks marked with "4"
    /// 17 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are not properly identified again.
    /// ^ Seems to do the same thing as 14, with "1" areas for building occupied areas, and sometimes "3" for wildlife groups.
    /// 16 = UNCERTAIN || Related to AI || 
    /// ^ Seems to respect player and custom lord. Player is part of the "6" default value spread, while custom lord (on slot2) is "2" for their AIV layout.
    /// ^ Default map value seems to be "6" mostly with some "1" chunks as outliers.
    /// 15 = UNCERTAIN || Related to AI || 
    /// ^ Player and custom lords are not properly identified again.
    /// ^ Chunks of "1"s can be found, most of the surrounding area is identified with "7"
    /// ^ For some reason, wildlife tribes are marked with a "6" chunk.
    /// 14 = UNCERTAIN || Related to AI ||
    /// ^ Player and custom lords are not properly identified again.
    /// ^ Seems to describe unit/building proximity chunkbased? In this format:
    /// ^ 10x10 inner area of "2" around origin with a outer 30x30 area around it with "4" for UNITS
    /// ^ 10x10 inner area of "3" around origin with a outer 30x30 area around it with "4" for BUILDINGS
    /// ^ Animals / wildlife are exempt.
    /// 13 = UNCERTAIN || Related to AI || Seems to be some sort of "Map Control" description grid of limited info.
    /// ^ Player and custom lords are not properly identified again. Both have "2" as default value for their layout and proximity zone.
    /// ^ Outlier value this time seems to be "1" only
    /// ^ value "2" seems to be spawned in a variable sized grid around units in "chunks" encompassing them or multiple ones.
    /// ^ therefore best guess is this is used for map control purposes in relation to AI.
    /// 12 = UNCERTAIN || Related to AI || Default value seems to be 5, with some "1" zone markers (similiar to 12), but this one seems to follow CompactPlayerIdMask more closely
    /// ^ as custom lords are once again respected. Player2 Custom Lord AIV area gets set as 2 accordingly. The player gets no zone at all, aside from "5"
    /// 11 = UNCERTAIN || Related to AI || Follows mostly the same rules as 10 BUT the occasional zones seem to be inverted for this one. Covering areas the other one did not, and vice versa.
    /// ^ the only non-expected value here is "1" being used to describe the zones. These zones seem to be including buildings, and seemingly random size-variant locations.
    /// 10 = UNCERTAIN || Related to AI || 2 = Player OR non-standard AI Lord? Custom lords seem also to have 2 for their areas. CompactPlayerIdMask seems to be involved, but with more data.
    /// ^ 2 also seems to be the "default" value for the global map. There seem to be certain "zones" with values such as 1, 5, 7 throwing some doubt into if CompactPlayerIdMask really is used here.
    /// 9 = NONE?
    /// 8 = NONE?
    /// 7 = NONE?
    /// 6 = NONE?
    /// 5 = Activator?
    /// 4 = DelayLayer
    /// 3 = OccupancyGrid
    /// 2 = StructureWasGrid
    /// 1 = StructureGrid
    /// </summary>
    /// <remarks>
    /// In order to reliably use this function, you need to select a number from 0 to 33+) run the function once
    /// switch mode to 5 then run it again. No idea why it has to be like this, didnt bother looking into it much more.
    /// For example:
    /// Tile_SetDebugRendering(14, true)
    /// Tile_SetDebugRendering(5, true)
    /// </remarks>
    /// <param name="mode"></param>
    /// <param name="force"></param>
    [LuaApiExport("SetDebugRendering")]
    internal void SetDebugRendering(int mode, bool force = false)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameMap.instance.setupDebugRenderLayerMap(force);
            GameMap.instance.setDebugRendering(mode, force);
        });
    }

    /// <summary>
    /// Attempts to update the visual representation of resource tiles associated with the specified building.
    /// For use with buildings, you also have to call <see cref="GameBuildingManagerAPI.UpdateVisualResourceGoods(int)"/> in most cases.
    /// </summary>
    /// <param name="buildingId">The unique identifier of the building.</param>
    /// <returns>true if the resource tile visuals were successfully updated for the specified building; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("UpdateResourceVisuals")]
    public bool TryUpdateTileResourceVisualsForBuilding(int buildingId)
    {
        return BulkTileDetours.c_game_update_visual_resourcetile!((IntPtr)_tileManagerVA, buildingId) == 1;
    }

    //
    // Common Functions
    //

    /// <summary>
    /// Get a tile id through its x and y coordinate.
    /// </summary>
    /// <param name="tileX">Tile Y</param>
    /// <param name="tileY">Tile X</param>
    /// <returns>Tile Id</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("GetId")]
    public int GetTileId(int tileX, int tileY)
    {
        return tileX + MapRowLookupTable[3 * tileY];
    }

    /// <summary>
    /// Converts a 1D tile identifier back into its 2D X coordinates, requiring the original Y coordinate to resolve the isometric projection.
    /// </summary>
    /// <param name="tileId">The unique integer ID of the tile.</param>
    /// <param name="tileY">The known Y-coordinate of the tile.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> containing the fully resolved X and Y coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("GetTileXFromTileIdAndY")]
    public int GetTileXFromTileIdAndY(int tileId, int tileY)
    {
        uint rowStartId = ((uint*)MapRowLookupTable)[3 * tileY];
        uint uTileId = (uint)tileId;
        uint tileX = uTileId - rowStartId;
        return (int)tileX;
    }

    /// <summary>
    /// Converts a 1D tile identifier back into its 2D X/Y coordinates by searching the isometric lookup table.
    /// </summary>
    /// <param name="tileId">The unique integer ID of the tile.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> containing the fully resolved X and Y coordinates.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("GetTileVectorFromId")]
    public UnmanagedVector2<UInt16> GetTileVectorFromId(int tileId)
    {
        UInt16 tileY = (UInt16)MapColumnLookupTable[tileId];
        UInt16 tileX = (UInt16)(tileId - MapRowLookupTable[3 * tileY]);
        return new UnmanagedVector2<UInt16>(tileX, tileY);
    }

    /// <summary>
    /// Returns the current map size.
    /// For 400x400 it would return 400.
    /// </summary>
    /// <returns>Map Size</returns>
    [LuaApiExport("GetCurrentMapSize")]
    public int GetCurrentMapSize()
    {
        return (int)*_currentMapSize;
    }

    /// <summary>
    /// Attempts to refresh a given tile area visually.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("RefreshTileAreaVisuals")]
    public void RefreshTileAreaVisuals(int tileX, int tileY)
    {
        BulkMapEditorDetours.c_game_tile_refresh_visual(GetPathfindingContext(), 4, tileX, tileY);
    }

    /// <summary>
    /// Gets the property flags for a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The <see cref="TilePropertyFlag"/> for the specified tile.</returns>
    /// <remarks>
    /// This value is a bitfield that describes the physical properties of the tile,
    /// such as whether it's land, water, occupied by a building, or contains a resource.
    /// You can use bitwise operations to check for multiple properties at once.
    /// </remarks>
    /// <seealso cref="SetTilePropertyFlag(int, TilePropertyFlag)"/>
    /// <seealso cref="TilePropertyFlag"/>
    [LuaApiExport("GetPropertyFlag")]
    public TilePropertyFlag GetTilePropertyFlag(int tileId)
    {
        return (TilePropertyFlag)TileManager.LogicGrid[tileId];
    }

    /// <summary>
    /// Sets the property flags for a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="flag">The <see cref="TilePropertyFlag"/> value to set.</param>
    /// <remarks>
    /// Be cautious when modifying these flags, as they directly affect game logic
    /// like pathfinding, resource placement, and building permissions.
    /// </remarks>
    /// <seealso cref="GetTilePropertyFlag(int)"/>
    /// <seealso cref="TilePropertyFlag"/>
    [LuaApiExport("SetPropertyFlag")]
    public void SetTilePropertyFlag(int tileId, TilePropertyFlag flag)
    {
        LogHelper.Debug($"Set Property from TileId=[{tileId}], new=[{(flag)}] old=[{TileManager.LogicGrid[tileId]}]");
        TileManager.LogicGrid[tileId] = (int)flag;
    }

    /// <summary>
    /// Adds a property flag to a tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="flag">The <see cref="TilePropertyFlag"/> value to add.</param>
    [LuaApiExport("AddPropertyFlag")]
    public void AddTilePropertyFlag(int tileId, TilePropertyFlag flag)
    {
        TilePropertyFlag current = GetTilePropertyFlag(tileId);
        SetTilePropertyFlag(tileId, current | flag);
        LogHelper.Debug($"Added Property to TileId=[{tileId}], new=[{(TilePropertyFlag)(current | flag)}] old=[{current}]");
    }

    /// <summary>
    /// Checks if a tile has any of the specified property flags.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to check.</param>
    /// <param name="flag">The <see cref="TilePropertyFlag"/> bitmask to check for.</param>
    /// <returns>true if the tile has at least one of the specified flags; otherwise, false.</returns>
    /// <remarks>
    /// This method uses a bitwise AND operation to check for the presence of flags.
    /// For example, to check if a tile is either a moat or a planned moat, you could use:
    /// <code>HasTilePropertyFlag(tileId, TilePropertyFlag.IsMoat | TilePropertyFlag.PlannedMoat)</code>
    /// </remarks>
    [LuaApiExport("HasPropertyFlag")]
    public bool HasTilePropertyFlag(int tileId, TilePropertyFlag flag)
    {
        return (GetTilePropertyFlag(tileId) & flag) != 0;
    }

    /// <summary>
    /// Remove a property flag from a tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="flag">The <see cref="TilePropertyFlag"/> value to remove.</param>
    [LuaApiExport("RemovePropertyFlag")]
    public void RemoveTilePropertyFlag(int tileId, TilePropertyFlag flag)
    {
        TilePropertyFlag current = GetTilePropertyFlag(tileId);
        SetTilePropertyFlag(tileId, current & ~flag);
        LogHelper.Debug($"Removed Property from TileId=[{tileId}], new=[{(TilePropertyFlag)(current & ~flag)}] old=[{current}]");
    }

    /// <summary>
    /// Gets the id of vegetation on a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The vegetation id for the specified tile.</returns>
    [LuaApiExport("GetVegetationId")]
    public int GetTileVegetationId(int tileId)
    {
        LogHelper.Debug($"Get Property from TileId=[{tileId}], get=[{TileManager.OrganismGrid[tileId]}]");
        return TileManager.OrganismGrid[tileId];
    }

    /// <summary>
    /// Sets the id of vegetation on a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="vegetationId">The id of the supposed vegetation.</param>
    [LuaApiExport("SetVegetationId")]
    public void SetTileVegetationId(int tileId, int vegetationId)
    {
        TileManager.OrganismGrid[tileId] = (UInt16)vegetationId;
    }

    /// <summary>
    /// Gets the height level of a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The height value of the tile.</returns>
    /// <seealso cref="SetTileHeight(int, byte)"/>
    [LuaApiExport("GetHeight")]
    public byte GetTileHeight(int tileId)
    {
        return TileManager.HeightGrid[tileId];
    }

    /// <summary>
    /// Sets the height level of a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="height">The height value to set.</param>
    /// <seealso cref="GetTileHeight(int)"/>
    [LuaApiExport("SetHeight")]
    public void SetTileHeight(int tileId, byte height)
    {
        TileManager.HeightGrid[tileId] = height;
    }

    /// <summary>
    /// Gets the secondary state of a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The state value of the tile.</returns>
    /// <remarks>
    /// This value is used for game-specific states, such as the growth progress of farm plots,
    /// or to indicate if a wall tile is damaged and impassable.
    /// </remarks>
    /// <seealso cref="SetTileState(int, byte)"/>
    [LuaApiExport("GetState")]
    public byte GetTileState(int tileId)
    {
        return TileManager.DamageGrid[tileId];
    }

    /// <summary>
    /// Sets the secondary state of a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="state">The state value to set.</param>
    /// <seealso cref="GetTileState(int)"/>
    [LuaApiExport("SetState")]
    public void SetTileState(int tileId, byte state)
    {
        TileManager.DamageGrid[tileId] = state;
    }

    /// <summary>
    /// Gets the default height(or health) of a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The new default height/health value of the tile.</returns>
    /// <seealso cref="SetTileDefaultHeight(int, byte)"/>
    [LuaApiExport("GetDefaultHeight")]
    public byte GetTileDefaultHeight(int tileId)
    {
        return TileManager.DefaultHeightGrid[tileId];
    }

    /// <summary>
    /// Sets the default height(or health) of a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="defaultHeight">The default height/health value to set.</param>
    /// <seealso cref="GetTileDefaultHeight(int)"/>
    [LuaApiExport("SetDefaultHeight")]
    public void SetTileDefaultHeight(int tileId, byte defaultHeight)
    {
        TileManager.DefaultHeightGrid[tileId] = defaultHeight;
    }

    /// <summary>
    /// Gets the ID of the player who owns the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The ID of the owning player.</returns>
    /// <remarks>A value of 0 typically indicates that the tile is unowned. Typically used for Player-made walls</remarks>
    /// <seealso cref="SetTilePlayerOwnerId(int, byte)"/>
    [LuaApiExport("GetOwnerId")]
    public byte GetTilePlayerOwnerId(int tileId)
    {
        return TileManager.WallOwnerGrid[tileId];
    }

    /// <summary>
    /// Sets the player ownership for a specific tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="playerId">The ID of the player to set as the owner.</param>
    /// <seealso cref="GetTilePlayerOwnerId(int)"/>
    [LuaApiExport("SetOwnerId")]
    public void SetTilePlayerOwnerId(int tileId, byte playerId)
    {
        TileManager.WallOwnerGrid[tileId] = playerId;
    }

    /// <summary>
    /// Gets the ID of the building occupying the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The ID of the building on the tile. Returns 0 if no building is present.</returns>
    /// <seealso cref="SetTileBuildingId(int, UInt16)"/>
    [LuaApiExport("GetBuildingId")]
    public UInt16 GetTileBuildingId(int tileId)
    {
        UInt16 val = TileManager.StructureGrid[tileId];
        return val;
    }

    /// <summary>
    /// Sets the ID of the building occupying the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="buildingId">The ID of the building to set. Use 0 to clear.</param>
    /// <remarks>
    /// This should be synchronized with the tile's <see cref="TilePropertyFlag"/> to ensure
    /// the tile is correctly marked as occupied.
    /// </remarks>
    /// <seealso cref="GetTileBuildingId(int)"/>
    [LuaApiExport("SetBuildingId")]
    public void SetTileBuildingId(int tileId, UInt16 buildingId)
    {
        TileManager.StructureGrid[tileId] = buildingId;
    }

    /// <summary>
    /// Gets the ID of the unit currently occupying the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The ID of the unit on the tile. Returns 0 if no unit is present.</returns>
    /// <seealso cref="SetTileUnitId(int, ushort)"/>
    [LuaApiExport("GetUnitId")]
    public UInt16 GetTileUnitId(int tileId)
    {
        return TileManager.TileUnitIdGrid[tileId];
    }

    /// <summary>
    /// Sets the ID of the unit occupying the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="unitId">The ID of the unit to set. Use 0 to clear.</param>
    /// <seealso cref="GetTileUnitId(int)"/>
    [LuaApiExport("SetUnitId")]
    public void SetTileUnitId(int tileId, UInt16 unitId)
    {
        TileManager.TileUnitIdGrid[tileId] = unitId;
    }

    /// <summary>
    /// Gets the visual texture type of the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>The <see cref="TileType"/> of the tile.</returns>
    /// <remarks>
    /// This value controls the tile's appearance (e.g., dirt, sand, grass). It is separate from
    /// the tile's physical properties, which are defined by <see cref="TilePropertyFlag"/>.
    /// </remarks>
    /// <seealso cref="SetTileType(int, TileType)"/>
    /// <seealso cref="TileType"/>
    [LuaApiExport("GetType")]
    public TileType GetTileType(int tileId)
    {
        return (TileType)TileManager.Logic2Grid[tileId];
    }

    /// <summary>
    /// Sets the visual texture type of the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="tileType">The <see cref="TileType"/> to set.</param>
    /// <seealso cref="GetTileType(int)"/>
    /// <seealso cref="TileType"/>
    [LuaApiExport("SetType")]
    public void SetTileType(int tileId, TileType tileType)
    {
        TileManager.Logic2Grid[tileId] = (byte)tileType;
    }

    /// <summary>
    /// Returns the aiv block data entry of the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to query.</param>
    /// <returns>CompactPlayerBitMask representation of the entry at tileId</returns>
    [LuaApiExport("GetAIVBlockData")]
    public CompactPlayerBitMask GetAIVBlockData(int tileId)
    {
        return TileManager.AIVBlockGrid[tileId];
    }

    /// <summary>
    /// Sets the aiv block data entry of the tile.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to modify.</param>
    /// <param name="value">The <see cref="CompactPlayerBitMask"/> to set.</param>
    /// <seealso cref="GetAIVBlockData(int)"/>
    /// <seealso cref="CompactPlayerBitMask"/>
    [LuaApiExport("SetAIVBlockData")]
    public void SetAIVBlockData(int tileId, CompactPlayerBitMask value)
    {
        TileManager.AIVBlockGrid[tileId] = value;
    }

    /// <summary>
    /// Checks if a given tile is walkable and does not have a building on it.
    /// </summary>
    /// <param name="tileId">The unique identifier of the tile to check.</param>
    /// <returns>true if the tile is valid for spawning; otherwise, false.</returns>
    [LuaApiExport("IsWalkableAndUnoccupied")]
    public bool IsTileWalkableAndUnoccupied(int tileId)
    {
        if (tileId < 0)
        {
            LogHelper.Warning($"Tried to access invalid tileId: {tileId}");
            return false;
        }

        // A tile is occupied if a building is on it.
        if (GetTileBuildingId(tileId) > 0)
        {
            return false;
        }

        TilePropertyFlag flags = GetTilePropertyFlag(tileId);

        // A tile is walkable if it has the IsLand flag and or none flag...
        bool isLand = (flags & TilePropertyFlag.IsLand) == TilePropertyFlag.IsLand;
        bool isNone = (flags & TilePropertyFlag.None) == TilePropertyFlag.None;

        // ...and does NOT have any of the impassable flags.
        bool isObstructed = (flags & TilePropertyMasks.ImpassableMask) != 0;

        return (isLand || isNone) && !isObstructed;
    }

    /// <summary>
    /// Checks if a generic XY coordinate is strictly within the playable diamond-shaped map area.
    /// Used to prevent accessing "void" tiles which tends to cause crashes.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("IsInsideMapBounds")]
    public bool IsTileInsideMapBounds(int x, int y)
    {
        // Optimization: Check global grid bounds first (0-800)
        if (x < 0 || x >= MAX_WIDTH || y < 0 || y >= MAX_HEIGHT)
            return false;

        // The playable map is a Diamond (Rotated Square) centered in the 800x800 grid.
        // Center is (400, 400).
        // The extent is determined by MapSize (e.g. 400, 700).
        // The condition for being inside a diamond is Manhattan Distance <= Radius.
        int mapSize = GetCurrentMapSize();
        if (mapSize <= 0)
            return false;

        int center = MAX_WIDTH / 2; // 400
        int radius = mapSize / 2;

        int dx = Math.Abs(x - center);
        int dy = Math.Abs(y - center);

        return (dx + dy) <= radius;
    }

    /// <summary>
    /// Helper to determine if a TileID is safe to use for array access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("IsValid")]
    public bool IsValidTileId(int tileId)
    {
        return (UInt32)tileId < GameTileManagerView.NativePackedTileCapacity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryGetPackedTileId(int tileX, int tileY, out int tileId)
    {
        tileId = 0;
        if (!IsTileInsideMapBounds(tileX, tileY))
            return false;

        tileId = GetTileId(tileX, tileY);
        return IsValidTileId(tileId);
    }

    /// <summary>
    /// Finds the nearest unoccupied and walkable tile by searching in an expanding square (spiral search).
    /// </summary>
    /// <param name="tileX">The starting X coordinate for the search.</param>
    /// <param name="tileY">The starting Y coordinate for the search.</param>
    /// <param name="maxRange">The max range to search.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> containing the coordinates of the nearest valid tile.</returns>
    /// <remarks>
    /// If no valid tile is found within a 50-tile radius, it returns the original starting coordinates as a fallback.
    /// </remarks>
    [LuaApiExport("GetNearestUnoccupiedTile")]
    public UnmanagedVector2<UInt16> GetNearestUnoccupiedTile(int tileX, int tileY, int maxRange = 50)
    {
        // Clamp input coordinates to ensure they are within the valid map area.
        int startX = Math.Max(0, Math.Min(tileX, MAX_WIDTH - 1));
        int startY = Math.Max(0, Math.Min(tileY, MAX_HEIGHT - 1));

        // First, check if the starting tile itself is valid.
        if (IsTileWalkableAndUnoccupied(GetTileId(startX, startY)))
        {
            return new UnmanagedVector2<UInt16>((UInt16)startX, (UInt16)startY);
        }

        // Begin the expanding search, from a radius of 1 up to maxRange.
        for (int r = 1; r <= maxRange; r++)
        {
            // --- Iterate over the perimeter of a square with radius 'r' ---

            // Check Top and Bottom rows
            for (int i = -r; i <= r; i++)
            {
                int currentX = startX + i;

                // Top row
                int currentYTop = startY - r;
                if (currentX >= 0 && currentX < MAX_WIDTH && currentYTop >= 0 && currentYTop < MAX_HEIGHT)
                {
                    if (IsTileWalkableAndUnoccupied(GetTileId(currentX, currentYTop)))
                    {
                        return new UnmanagedVector2<UInt16>((UInt16)currentX, (UInt16)currentYTop);
                    }
                }

                // Bottom row
                int currentYBottom = startY + r;
                if (currentX >= 0 && currentX < MAX_WIDTH && currentYBottom >= 0 && currentYBottom < MAX_HEIGHT)
                {
                    if (IsTileWalkableAndUnoccupied(GetTileId(currentX, currentYBottom)))
                    {
                        return new UnmanagedVector2<UInt16>((UInt16)currentX, (UInt16)currentYBottom);
                    }
                }
            }

            // Check Left and Right columns (excluding corners, which were already checked)
            for (int i = -r + 1; i < r; i++)
            {
                int currentY = startY + i;

                // Left column
                int currentXLeft = startX - r;
                if (currentXLeft >= 0 && currentXLeft < MAX_WIDTH && currentY >= 0 && currentY < MAX_HEIGHT)
                {
                    if (IsTileWalkableAndUnoccupied(GetTileId(currentXLeft, currentY)))
                    {
                        return new UnmanagedVector2<UInt16>((UInt16)currentXLeft, (UInt16)currentY);
                    }
                }

                // Right column
                int currentXRight = startX + r;
                if (currentXRight >= 0 && currentXRight < MAX_WIDTH && currentY >= 0 && currentY < MAX_HEIGHT)
                {
                    if (IsTileWalkableAndUnoccupied(GetTileId(currentXRight, currentY)))
                    {
                        return new UnmanagedVector2<UInt16>((UInt16)currentXRight, (UInt16)currentY);
                    }
                }
            }
        }

        // Fallback: If no suitable tile was found within the maxRange, return the original starting position.
        return new UnmanagedVector2<UInt16>((UInt16)startX, (UInt16)startY);
    }

    /// <summary>
    /// Retrieves an array of TileIDs for all tiles within a specified rectangular area.
    /// The coordinates are automatically clamped to the valid map boundaries.
    /// </summary>
    /// <param name="tileX">The starting X coordinate of the rectangle.</param>
    /// <param name="tileY">The starting Y coordinate of the rectangle.</param>
    /// <param name="width">The width of the rectangle.</param>
    /// <param name="height">The height of the rectangle.</param>
    /// <returns>An list of integer TileIDs within the specified rectangle.</returns>
    public List<int> GetTilesInRect(int tileX, int tileY, int width, int height)
    {
        List<int> results = new List<int>(width * height);

        // Clamp the loop boundaries to ensure they are within the maps valid area.
        // This prevents any out-of-bounds errors.
        int startX = Math.Max(0, tileX);
        int startY = Math.Max(0, tileY);

        // The end coordinates are exclusive, so we dont subtract 1.
        int endX = Math.Min(MAX_WIDTH, tileX + width);
        int endY = Math.Min(MAX_HEIGHT, tileY + height);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                // For each valid coordinate, get its ID and add it to the list.
                results.Add(GetTileId(x, y));
            }
        }

        return results;
    }

    /// <summary>
    /// Retrieves an array of TileIDs for all tiles within a circular range of a central point.
    /// </summary>
    /// <param name="centerX">The X coordinate of the center point.</param>
    /// <param name="centerY">The Y coordinate of the center point.</param>
    /// <param name="range">The radius of the circle (in tiles).</param>
    /// <returns>An list of integer TileIDs within the specified range.</returns>
    public List<int> GetTilesInRange(int centerX, int centerY, int range)
    {
        List<int> results = new List<int>();

        // Use squared distances to avoid costly square root calculations.
        // Use long to prevent integer overflow with large ranges.
        long rangeSquared = (long)range * range;

        // Define a bounding box around the circle to minimize the number of tiles to check.
        // Clamp the bounding box to the valid map area.
        int startX = Math.Max(0, centerX - range);
        int startY = Math.Max(0, centerY - range);
        int endX = Math.Min(MAX_WIDTH, centerX + range + 1);
        int endY = Math.Min(MAX_HEIGHT, centerY + range + 1);

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                // Calculate the squared distance from the center to the current tile.
                long dX = x - centerX;
                long dY = y - centerY;

                // If the tile is within the circles radius...
                if ((dX * dX + dY * dY) <= rangeSquared)
                {
                    // ...get its ID and add it to the list.
                    results.Add(GetTileId(x, y));
                }
            }
        }

        return results;
    }

    /// <summary>
    /// Selects a random tile within a rectangle that satisfies a specific condition.
    /// </summary>
    /// <param name="tileX">Top-left X.</param>
    /// <param name="tileY">Top-left Y.</param>
    /// <param name="width">Width.</param>
    /// <param name="height">Height.</param>
    /// <param name="condition">Optional lambda. Returns the TileId. Return true to accept the tile.</param>
    /// <returns>A valid tile coordinate, or the starting coordinate if none found.</returns>
    public UnmanagedVector2<UInt16> GetRandomTileInRect(int tileX, int tileY, int width, int height, Predicate<int> condition = null)
    {
        int mapSize = GetCurrentMapSize();

        int startX = Math.Max(0, tileX);
        int startY = Math.Max(0, tileY);
        int endX = Math.Min(mapSize, tileX + width);
        int endY = Math.Min(mapSize, tileY + height);

        // Fast Path: Rejection Sampling
        // This avoids iterating the whole grid for common requests.
        for (int i = 0; i < RANDOM_TILE_SELECTION_RETRY_AMOUNT; i++)
        {
            int rX = DeterministicRandom.Next(startX, endX);
            int rY = DeterministicRandom.Next(startY, endY);

            if (!IsTileInsideMapBounds(rX, rY))
                continue;
            int tileId = GetTileId(rX, rY);
            if (!IsValidTileId(tileId))
                continue;

            // If no condition is provided, we just accept the first random spot.
            if (condition == null)
            {
                return new UnmanagedVector2<UInt16>((UInt16)rX, (UInt16)rY);
            }

            if (condition(tileId))
            {
                return new UnmanagedVector2<UInt16>((UInt16)rX, (UInt16)rY);
            }
        }

        // Slow Path: Reservoir Sampling
        // If we are here, the condition is likely very strict or the area is crowded.
        // We iterate the area and pick a random valid tile efficiently without allocating a List.
        UnmanagedVector2<UInt16> selected = new UnmanagedVector2<UInt16>((UInt16)tileX, (UInt16)tileY);
        int validCount = 0;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                // Strict bounds check prevents processing void tiles
                if (!IsTileInsideMapBounds(x, y))
                    continue;

                int tileId = GetTileId(x, y);
                if (!IsValidTileId(tileId))
                    continue;

                if (condition != null)
                {
                    if (!condition(tileId))
                        continue;
                }

                // We found a valid candidate
                validCount++;

                // Reservoir Sampling
                if (DeterministicRandom.Next(validCount) == 0)
                {
                    selected = new UnmanagedVector2<UInt16>((UInt16)x, (UInt16)y);
                }
            }
        }

        // If we didn't find anything valid (foundAny is false), we return the original input X/Y.
        return selected;
    }

    /// <summary>
    /// Selects a random tile within a sphere that satisfies a specific condition.
    /// </summary>
    /// <param name="centerX">Center X.</param>
    /// <param name="centerY">Center Y.</param>
    /// <param name="radius">Radius.</param>
    /// <param name="condition">Optional lambda. Returns the TileId. Return true to accept the tile.</param>
    /// <returns>A valid tile coordinate, or the center coordinate if none found.</returns>
    public UnmanagedVector2<UInt16> GetRandomTileInSphere(int centerX, int centerY, int radius, Predicate<int> condition = null)
    {
        long radiusSquared = (long)radius * radius;

        int mapSize = GetCurrentMapSize();
        LogHelper.Verbose($"MapSize: {mapSize}, centerX: {centerX}, centerY={centerX}, radius={radius}");

        int startX = Math.Max(0, centerX - radius);
        int endX = Math.Min(mapSize, centerX + radius + 1);
        int startY = Math.Max(0, centerY - radius);
        int endY = Math.Min(mapSize, centerY + radius + 1);
        LogHelper.Verbose($"startX: {startX}");
        LogHelper.Verbose($"endX: {endX}");
        LogHelper.Verbose($"startY: {startY}");
        LogHelper.Verbose($"endY: {endY}");

        // Fast Path: Rejection Sampling
        for (int i = 0; i < RANDOM_TILE_SELECTION_RETRY_AMOUNT; i++)
        {
            int rX = DeterministicRandom.Next(startX, endX);
            int rY = DeterministicRandom.Next(startY, endY);
            LogHelper.Verbose($"rX={rX}, rY={rY}");

            long dX = rX - centerX;
            long dY = rY - centerY;
            LogHelper.Verbose($"dX={dX}, dY={dY}");

            // Check Diamond Map Bounds
            if (!IsTileInsideMapBounds(rX, rY)) continue;

            int tileId = GetTileId(rX, rY);
            if (!IsValidTileId(tileId)) continue;

            // Check condition
            if (condition == null || condition(GetTileId(rX, rY)))
            {
                LogHelper.Verbose($"Fast path found random tile: {rX}, {rY}");
                return new UnmanagedVector2<UInt16>((UInt16)rX, (UInt16)rY);
            }
        }

        // Slow Path: Reservoir Sampling
        UnmanagedVector2<UInt16> selected = new UnmanagedVector2<UInt16>((UInt16)centerX, (UInt16)centerY);
        int validCount = 0;

        for (int y = startY; y < endY; y++)
        {
            for (int x = startX; x < endX; x++)
            {
                long dX = x - centerX;
                long dY = y - centerY;

                // Geometry Check
                if ((dX * dX + dY * dY) > radiusSquared)
                    continue;

                // Check Diamond Map Bounds
                if (!IsTileInsideMapBounds(x, y)) continue;

                int tileId = GetTileId(x, y);
                if (!IsValidTileId(tileId)) continue;

                // Condition Check
                if (condition != null)
                {
                    if (!condition(tileId))
                        continue;
                }

                validCount++;

                if (DeterministicRandom.Next(validCount) == 0)
                {
                    selected = new UnmanagedVector2<UInt16>((UInt16)x, (UInt16)y);
                }
            }
        }
        LogHelper.Information($"Slow path found random tile: {selected}");
        return selected;
    }

    /// <summary>
    /// Validates an inclusive rectangular tile range against the map's dimensions and diamond-shaped playable bounds, and calculates its width and height.
    /// </summary>
    /// <param name="minX">The rectangle's minimum X coordinate.</param>
    /// <param name="minY">The rectangle's minimum Y coordinate.</param>
    /// <param name="maxX">The rectangle's maximum X coordinate, inclusive.</param>
    /// <param name="maxY">The rectangle's maximum Y coordinate, inclusive.</param>
    /// <param name="width">Receives the inclusive rectangle width, or zero when validation fails.</param>
    /// <param name="height">Receives the inclusive rectangle height, or zero when validation fails.</param>
    /// <returns><c>true</c> when the dimensions are valid and every coordinate resolves to a valid map tile; otherwise, <c>false</c>.</returns>
    public bool TryValidateTileRectangle(int minX, int minY, int maxX, int maxY, out int width, out int height)
    {
        width = 0;
        height = 0;
        if (minX > maxX || minY > maxY)
            return false;

        long width64 = (long)maxX - minX + 1;
        long height64 = (long)maxY - minY + 1;
        if (width64 <= 0 || height64 <= 0 || width64 > MAX_WIDTH || height64 > MAX_HEIGHT || width64 * height64 > MAX_WIDTH * MAX_HEIGHT)
        {
            LogHelper.Error("The requested rectangle has invalid dimensions.");
            return false;
        }

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                if (!IsTileInsideMapBounds(x, y))
                {
                    LogHelper.Error($"rectangle contains an out-of-map tile at ({x}, {y}).");
                    return false;
                }

                int tileId = GetTileId(x, y);
                if (!IsValidTileId(tileId))
                {
                    LogHelper.Error($"rectangle contains an invalid tile at ({x}, {y}).");
                    return false;
                }
            }
        }

        width = (int)width64;
        height = (int)height64;
        return true;
    }

    [LuaApiExport("IsInsideArea")]
    public static bool IsInsideArea(UnmanagedVector2<UInt16> position, int minX, int minY, int maxX, int maxY) =>
        position.X >= minX && position.X <= maxX && position.Y >= minY && position.Y <= maxY;

    #region "SEMA"
    /// <summary>
    /// Exports a rectangular area to a SEMA asset owned by a registered asset mod.
    /// Coordinates and entity references in files are area-relative.
    /// Loose mods receive the file in their resource tree; packed mods use a persistent overlay.
    /// </summary>
    /// <param name="startX">First X coordinate (inclusive).</param>
    /// <param name="startY">First Y coordinate (inclusive).</param>
    /// <param name="endX">Second X coordinate (inclusive).</param>
    /// <param name="endY">Second Y coordinate (inclusive).</param>
    /// <param name="assetProviderGuid">GUID of the registered asset mod that will own the file.</param>
    /// <param name="relativeAssetPath">Mod-relative .sema path, for example <c>MapAreas/castle.sema</c>.</param>
    /// <returns><c>true</c> when the complete area was serialized and saved.</returns>
    /// <example><code>Tile_ExportMapArea(398, 398, 402, 402, "My.Mod", "MapAreas/castle.sema")</code></example>
    public bool ExportMapArea(int startX, int startY, int endX, int endY, string assetProviderGuid, string relativeAssetPath)
    {
        int minX = Math.Min(startX, endX);
        int maxX = Math.Max(startX, endX);
        int minY = Math.Min(startY, endY);
        int maxY = Math.Max(startY, endY);
        if (!TryValidateTileRectangle(minX, minY, maxX, maxY, out int width, out int height))
            return false;

        try
        {
            int totalTiles = checked(width * height);
            SEMAData data = new SEMAData
            {
                FormatVersion = SEMAData.CurrentFormatVersion,
                Width = width,
                Height = height,
                Logic = new int[totalTiles],
                Organism = new ushort[totalTiles],
                Logic2 = new byte[totalTiles],
                Heights = new byte[totalTiles],
                DefaultHeights = new byte[totalTiles],
                Damage = new byte[totalTiles],
                WallOwners = new byte[totalTiles],
                RandomNoise = new ushort[totalTiles],
                Structure = new ushort[totalTiles],
                StructureWas = new byte[totalTiles],
                TileUnitId = new ushort[totalTiles],
                Fly = new short[totalTiles],
                Macro = new short[totalTiles],
                PathConnection = new ushort[totalTiles],
                Delay = new byte[totalTiles],
                GatePath = new byte[totalTiles]
            };

            int index = 0;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++, index++)
                {
                    int tileId = GetTileId(x, y);
                    data.Logic[index] = TileManager.LogicGrid[tileId];
                    data.Logic2[index] = TileManager.Logic2Grid[tileId];
                    data.Heights[index] = TileManager.HeightGrid[tileId];
                    data.DefaultHeights[index] = TileManager.DefaultHeightGrid[tileId];
                    data.Damage[index] = TileManager.DamageGrid[tileId];
                    data.WallOwners[index] = TileManager.WallOwnerGrid[tileId];
                    data.RandomNoise[index] = TileManager.TileRandomNoiseGrid[tileId];
                    data.Macro[index] = TileManager.MacroGrid[tileId];
                    data.PathConnection[index] = TileManager.PathConnectionGrid[tileId];
                    data.Delay[index] = TileManager.DelayGrid[tileId];
                    data.GatePath[index] = TileManager.GatePathGrid[tileId];
                }
            }

            ExportSemaUnitsAndTribes(data, minX, minY, maxX, maxY);
            ExportSemaBuildings(data, minX, minY, maxX, maxY);
            ExportSemaVegetation(data, minX, minY, maxX, maxY);
            ExportSemaPitch(data, minX, minY, maxX, maxY);

            byte[] bytes = MessagePackSerializer.Serialize(data, SemaSerializerOptions);
            if (!SEMAAssetStorage.TryWrite(assetProviderGuid, relativeAssetPath, bytes))
                return false;

            LogHelper.Information($"Exported SEMA v{data.FormatVersion} {width}x{height} area to asset [{relativeAssetPath}] for [{assetProviderGuid}].");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to export map area");
            return false;
        }
    }

    /// <summary>
    /// Imports a mod-scoped SEMA asset at the supplied top-left map coordinate.
    /// The complete target rectangle must be valid and, unless force mode is enabled, free of dynamic entities.
    /// Packaged resources are loaded through the asset manager.
    /// </summary>
    /// <param name="targetX">Destination X coordinate.</param>
    /// <param name="targetY">Destination Y coordinate.</param>
    /// <param name="assetProviderGuid">GUID of the registered asset mod that owns the file.</param>
    /// <param name="relativeAssetPath">Mod-relative .sema path.</param>
    /// <param name="force">Skips destination occupancy validation. Existing entities are not removed.</param>
    /// <returns><c>true</c> when the asset was validated and imported.</returns>
    /// <example><code>Tile_ImportMapArea(450, 400, "My.Mod", "MapAreas/castle.sema")</code></example>
    public bool ImportMapArea(int targetX, int targetY, string assetProviderGuid, string relativeAssetPath, bool force = false)
    {
        try
        {
            if (!SEMAAssetStorage.TryRead(assetProviderGuid, relativeAssetPath, out byte[] bytes))
                return false;

            SEMAData data = MessagePackSerializer.Deserialize<SEMAData>(bytes, SemaSerializerOptions);
            if (!ValidateSemaForImport(data, targetX, targetY, force))
                return false;

            bool isV2 = data.FormatVersion == SEMAData.CurrentFormatVersion;
            ImportSemaTiles(data, targetX, targetY);

            Dictionary<int, int> buildingIds = ImportSemaBuildings(data, targetX, targetY, isV2);
            ImportSemaVegetation(data, targetX, targetY, isV2);
            ImportSemaPitch(data, targetX, targetY, isV2);
            Dictionary<int, int> unitIds = ImportSemaUnits(data, targetX, targetY, isV2);
            ImportSemaTribes(data, targetX, targetY, isV2, unitIds, buildingIds);

            RefreshSemaArea(targetX, targetY, data.Width, data.Height);
            LogHelper.Information($"Imported SEMA v{data.FormatVersion} asset [{assetProviderGuid}:{relativeAssetPath}] at ({targetX}, {targetY}); {unitIds.Count} units and {buildingIds.Count} buildings recreated.");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to import SEMA asset [{assetProviderGuid}:{relativeAssetPath}]");
            return false;
        }
    }

    private static bool IsAliveForSemaExport(AliveState state) => state != AliveState.None && state != AliveState.MarkedForDeletion;

    private static UnmanagedVector2<UInt16> ToSemaRelative(UnmanagedVector2<UInt16> position, int minX, int minY) =>
        new((UInt16)(position.X - minX), (UInt16)(position.Y - minY));

    private void ExportSemaUnitsAndTribes(SEMAData data, int minX, int minY, int maxX, int maxY)
    {
        GameUnitManagerAPI unitApi = GameUnitManagerAPI.Instance;
        GameTribeManagerAPI tribeApi = GameTribeManagerAPI.Instance;
        Span<GameUnit> units = unitApi.GetUnitsAsSpan();

        for (int index = 0; index < units.Length; index++)
        {
            ref GameUnit unit = ref units[index];
            if (!IsAliveForSemaExport(unit.r_AliveState))
                continue;

            UnmanagedVector2<UInt16> position = *unit.CurrentTilePosition();
            if (!IsInsideArea(position, minX, minY, maxX, maxY))
                continue;

            int oldUnitId = index + 1;
            SEMAUnit semaUnit = new()
            {
                Id = oldUnitId,
                GlobalId = (int)unit.r_GlobalId,
                UnitType = unit.r_UnitChimp,
                Health = (int)unit.r_CurrentHealth,
                MaxHealth = (int)unit.r_MaxHealth,
                Direction = unit.r_Direction,
                TribeId = unit.r_TribeId,
                PlayerId = unit.r_ControllableForPlayerId,
                ColorPlayerId = (int)unit.r_SpritePlayerColorId,
                LocalTilePosition = ToSemaRelative(position, minX, minY),
                WorldTilePosition = new((UInt16)((position.X - minX) * 8), (UInt16)((position.Y - minY) * 8)),
                HeightElevation = unit.r_HeightElevation
            };
            data.Units[oldUnitId] = semaUnit;

            int oldTribeId = unit.r_TribeId;
            if (oldTribeId <= 0)
            {
                continue;
            }

            if (data.Tribes.TryGetValue(oldTribeId, out SEMATribe existingTribe))
            {
                existingTribe.Units.Add(semaUnit);
                continue;
            }

            if (!tribeApi.TryGetTribeById(oldTribeId, out GameTribe* tribe) || !IsAliveForSemaExport(tribe->r_AliveState))
                continue;

            SEMATribeOrderContext order = SEMATribeOrderContext.FromMemory(ref unit, ref *tribe);
            order.PatrolMode = tribe->r_PatrolMode;
            order.CurrentPatrolPoint = tribe->r_PatrolCurrentTargetIndex;
            order.PatrolPoints = tribeApi.GetPatrolPath(oldTribeId);
            NormalizeSemaOrderForExport(order, minX, minY, maxX, maxY);
            data.Tribes[oldTribeId] = new SEMATribe
            {
                Id = oldTribeId,
                GlobalId = (int)tribe->r_GlobalId,
                LeaderUnitId = tribe->r_LeaderUnitId,
                Units = new List<SEMAUnit> { semaUnit },
                OrderContext = order,
                PlayerId = tribe->r_PlayerIdOwner,
                Stance = tribe->r_TribeStance,
                MoveType = TribeMoveType.DefaultInSync
            };
        }
    }

    private void NormalizeSemaOrderForExport(SEMATribeOrderContext order, int minX, int minY, int maxX, int maxY)
    {
        for (int i = 0; i < order.PatrolPoints.Length; i++)
        {
            if (!IsInsideArea(order.PatrolPoints[i], minX, minY, maxX, maxY))
            {
                order.PatrolMode = TribePatrolMode.None;
                order.PatrolPoints = [];
                order.CurrentPatrolPoint = 0;
                break;
            }
            order.PatrolPoints[i] = ToSemaRelative(order.PatrolPoints[i], minX, minY);
        }

        if (order.Command == TribeAICommand.AttackWallTileId || order.Command == TribeAICommand.AttachLadderToWall)
        {
            if (order.TileId > 0 && IsValidTileId(order.TileId))
                order.LocalTile = GetTileVectorFromId(order.TileId);
        }

        switch (order.Command)
        {
            case TribeAICommand.MoveHerePosition:
            case TribeAICommand.AttackTilePosition:
            case TribeAICommand.DigMoatTileId:
            case TribeAICommand.ThrowLava:
            case TribeAICommand.AttackWallTileId:
            case TribeAICommand.AttachLadderToWall:
                if (IsInsideArea(order.LocalTile, minX, minY, maxX, maxY))
                    order.LocalTile = ToSemaRelative(order.LocalTile, minX, minY);
                else
                    order.Command = TribeAICommand.Unknown0;
                break;
        }
    }

    private void ExportSemaBuildings(SEMAData data, int minX, int minY, int maxX, int maxY)
    {
        Span<GameBuilding> buildings = GameBuildingManagerAPI.Instance.GetBuildingsAsSpan();
        GameBuildingManagerAPI buildingApi = GameBuildingManagerAPI.Instance;
        for (int index = 0; index < buildings.Length; index++)
        {
            ref GameBuilding building = ref buildings[index];
            if (!IsAliveForSemaExport(building.r_AliveState))
                continue;
            int oldId = index + 1;
            UnmanagedVector2<UInt16> position = *building.CurrentTilePosition();
            UnmanagedVector2<UInt16> endPosition = buildingApi.GetEndPosition(oldId);
            if (!IsInsideArea(position, minX, minY, maxX, maxY) || !IsInsideArea(endPosition, minX, minY, maxX, maxY))
                continue;

            data.Buildings[oldId] = new SEMABuilding
            {
                Id = oldId,
                GlobalId = (int)building.r_GlobalId,
                Structure = building.r_BuildingType,
                Health = building.r_CurrentHealth,
                MaxHealth = building.r_MaxHealth,
                PlayerId = building.r_PlayerIdOwner,
                ColorPlayerId = (int)building.r_SpritePlayerColorId,
                LocalTilePosition = ToSemaRelative(position, minX, minY),
                HeightElevation = (Int16)building.r_HeightElevation,
                BuildingScale = BuildingScales.GetScale(building.r_BuildingType.ConvertToEMappers()),
                SpriteVariationIndex = building.r_SpriteVariationIndex
            };
        }
    }

    private void ExportSemaVegetation(SEMAData data, int minX, int minY, int maxX, int maxY)
    {
        Span<GameVegetation> vegetation = GameVegetationManagerAPI.Instance.GetVegetationAsSpan();
        for (int index = 0; index < vegetation.Length; index++)
        {
            ref GameVegetation veg = ref vegetation[index];
            if (!IsAliveForSemaExport(veg.r_AliveState))
                continue;
            UnmanagedVector2<UInt16> position = *veg.CurrentTilePosition();
            if (!IsInsideArea(position, minX, minY, maxX, maxY))
                continue;

            int oldId = index + 1;
            data.Vegetations[oldId] = new SEMAVegetation
            {
                Id = oldId,
                GlobalId = (int)veg.r_GlobalId,
                Vegetation = veg.r_VegetationType,
                LocalTilePosition = ToSemaRelative(position, minX, minY),
                ResourceState = veg.r_ResourceState,
                GrowthStage = veg.r_GrowthStage,
                GrowthProgress = veg.r_GrowthProgress,
                Health = veg.r_Health
            };
        }
    }

    private static void ExportSemaPitch(SEMAData data, int minX, int minY, int maxX, int maxY)
    {
        Span<GamePitchDescriptor> pitches = GamePitchManagerAPI.Instance.GetPitchArrayAsSpan()[1..];
        for (int index = 0; index < pitches.Length; index++)
        {
            ref GamePitchDescriptor pitch = ref pitches[index];
            if (!pitch.IsAlive)
                continue;
            UnmanagedVector2<UInt16> position = *pitch.CurrentTilePosition();
            if (!IsInsideArea(position, minX, minY, maxX, maxY))
                continue;

            data.PitchTiles[index + 1] = new SEMAPitchTile
            {
                GlobalId = (int)pitch.GlobalId,
                PlayerId = pitch.OwnerId,
                LocalTilePosition = ToSemaRelative(position, minX, minY),
                RandomSeed = pitch.RandomSeed,
                State = pitch.State,
                FireTimer = pitch.FireTimer
            };
        }
    }

    private bool ValidateSemaForImport(SEMAData data, int targetX, int targetY, bool force)
    {
        if (data == null || (data.FormatVersion != 0 && data.FormatVersion != SEMAData.CurrentFormatVersion))
        {
            LogHelper.Error($"Unsupported SEMA format version [{data?.FormatVersion}].");
            return false;
        }

        long tileCount64 = (long)data.Width * data.Height;
        if (data.Width <= 0 || data.Height <= 0 || tileCount64 <= 0 || tileCount64 > MAX_WIDTH * MAX_HEIGHT)
        {
            LogHelper.Error($"Invalid SEMA dimensions [{data.Width}x{data.Height}].");
            return false;
        }

        int tileCount = (int)tileCount64;
        if (!HasSemaLength(data.Logic, tileCount) || !HasSemaLength(data.Logic2, tileCount) ||
            !HasSemaLength(data.Heights, tileCount) || !HasSemaLength(data.DefaultHeights, tileCount) ||
            !HasSemaLength(data.Damage, tileCount) || !HasSemaLength(data.WallOwners, tileCount) ||
            !HasSemaLength(data.RandomNoise, tileCount) || !HasSemaLength(data.Macro, tileCount) ||
            !HasSemaLength(data.PathConnection, tileCount) ||
            !HasSemaLength(data.Delay, tileCount) || !HasSemaLength(data.GatePath, tileCount))
        {
            LogHelper.Error("SEMA tile arrays do not match the declared dimensions.");
            return false;
        }

        long endX = (long)targetX + data.Width - 1;
        long endY = (long)targetY + data.Height - 1;
        if (endX > int.MaxValue || endY > int.MaxValue ||
            !TryValidateTileRectangle(targetX, targetY, (int)endX, (int)endY, out _, out _))
            return false;

        if (!force && !IsSemaDestinationClear(targetX, targetY, data.Width, data.Height))
            return false;

        data.Tribes ??= [];
        data.Units ??= [];
        data.Buildings ??= [];
        data.Vegetations ??= [];
        data.PitchTiles ??= [];
        return ValidateSemaEntityPositions(data, targetX, targetY);
    }

    private bool IsSemaDestinationClear(int targetX, int targetY, int width, int height)
    {
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int tileId = GetTileId(targetX + x, targetY + y);
                if (TileManager.OrganismGrid[tileId] != 0 || TileManager.StructureGrid[tileId] != 0 || TileManager.TileUnitIdGrid[tileId] != 0)
                {
                    LogHelper.Error($"SEMA destination is occupied at ({targetX + x}, {targetY + y}).");
                    return false;
                }
            }
        }

        int maxX = targetX + width - 1;
        int maxY = targetY + height - 1;
        Span<GamePitchDescriptor> pitches = GamePitchManagerAPI.Instance.GetPitchArrayAsSpan()[1..];
        for (int i = 0; i < pitches.Length; i++)
        {
            ref GamePitchDescriptor pitch = ref pitches[i];
            if (pitch.IsAlive && pitch.TileX >= targetX && pitch.TileX <= maxX && pitch.TileY >= targetY && pitch.TileY <= maxY)
            {
                LogHelper.Error($"SEMA destination contains pitch at ({pitch.TileX}, {pitch.TileY}).");
                return false;
            }
        }
        return true;
    }

    private static bool HasSemaLength(Array? values, int expectedLength) => values != null && values.Length == expectedLength;

    private bool ValidateSemaEntityPositions(SEMAData data, int targetX, int targetY)
    {
        bool isV2 = data.FormatVersion == SEMAData.CurrentFormatVersion;
        foreach (SEMAUnit unit in data.Units.Values)
            if (!ValidateSemaPosition(unit.LocalTilePosition, data, targetX, targetY, isV2)) return false;
        foreach (SEMABuilding building in data.Buildings.Values)
            if (!ValidateSemaPosition(building.LocalTilePosition, data, targetX, targetY, isV2)) return false;
        foreach (SEMAVegetation vegetation in data.Vegetations.Values)
            if (!ValidateSemaPosition(vegetation.LocalTilePosition, data, targetX, targetY, isV2)) return false;
        foreach (SEMAPitchTile pitch in data.PitchTiles.Values)
            if (!ValidateSemaPosition(pitch.LocalTilePosition, data, targetX, targetY, isV2)) return false;
        return true;
    }

    private bool ValidateSemaPosition(UnmanagedVector2<UInt16> position, SEMAData data, int targetX, int targetY, bool isV2)
    {
        if (isV2 && (position.X >= data.Width || position.Y >= data.Height))
        {
            LogHelper.Error($"SEMA entity offset ({position.X}, {position.Y}) is outside its {data.Width}x{data.Height} area.");
            return false;
        }
        return TryResolveSemaPosition(position, targetX, targetY, isV2, out _, out _);
    }

    private bool TryResolveSemaPosition(UnmanagedVector2<UInt16> position, int targetX, int targetY, bool isV2, out int mapX, out int mapY)
    {
        long x = isV2 ? (long)targetX + position.X : position.X;
        long y = isV2 ? (long)targetY + position.Y : position.Y;
        mapX = x >= int.MinValue && x <= int.MaxValue ? (int)x : -1;
        mapY = y >= int.MinValue && y <= int.MaxValue ? (int)y : -1;
        if (x < int.MinValue || x > int.MaxValue || y < int.MinValue || y > int.MaxValue || !IsTileInsideMapBounds(mapX, mapY))
        {
            LogHelper.Error($"SEMA entity position ({position.X}, {position.Y}) resolves outside the map.");
            return false;
        }
        return true;
    }

    private void ImportSemaTiles(SEMAData data, int targetX, int targetY)
    {
        int index = 0;
        for (int y = 0; y < data.Height; y++)
        {
            for (int x = 0; x < data.Width; x++, index++)
            {
                int tileId = GetTileId(targetX + x, targetY + y);
                TileManager.LogicGrid[tileId] = data.Logic[index];
                TileManager.Logic2Grid[tileId] = data.Logic2[index];
                TileManager.HeightGrid[tileId] = data.Heights[index];
                TileManager.DefaultHeightGrid[tileId] = data.DefaultHeights[index];
                TileManager.DamageGrid[tileId] = data.Damage[index];
                TileManager.WallOwnerGrid[tileId] = data.WallOwners[index];
                TileManager.TileRandomNoiseGrid[tileId] = data.RandomNoise[index];
                TileManager.MacroGrid[tileId] = data.Macro[index];
                TileManager.PathConnectionGrid[tileId] = data.PathConnection[index];
                TileManager.DelayGrid[tileId] = data.Delay[index];
                TileManager.GatePathGrid[tileId] = data.GatePath[index];
            }
        }
    }

    private Dictionary<int, int> ImportSemaBuildings(SEMAData data, int targetX, int targetY, bool isV2)
    {
        GameBuildingManagerAPI api = GameBuildingManagerAPI.Instance;
        Dictionary<int, int> idMap = [];
        foreach (KeyValuePair<int, SEMABuilding> pair in data.Buildings)
        {
            SEMABuilding building = pair.Value;
            if (!TryResolveSemaPosition(building.LocalTilePosition, targetX, targetY, isV2, out int x, out int y))
                continue;
            eMappers mapper = building.Structure.ConvertToEMappers();
            int scale = isV2 ? building.BuildingScale : BuildingScales.GetScale(mapper);
            if (scale < 0)
            {
                LogHelper.Warning($"Skipping unsupported SEMA building type [{building.Structure}].");
                continue;
            }
            int newId = (int)api.Create(building.PlayerId, x, y, building.HeightElevation, building.Structure, scale, building.ColorPlayerId, building.SpriteVariationIndex);
            if (newId <= 0 || !api.IsValidId(newId))
                continue;
            idMap[pair.Key] = newId;
            api.SetMaxHealth(newId, building.MaxHealth);
            api.SetCurrentHealth(newId, building.Health);
        }
        return idMap;
    }

    private void ImportSemaVegetation(SEMAData data, int targetX, int targetY, bool isV2)
    {
        GameVegetationManagerAPI api = GameVegetationManagerAPI.Instance;
        foreach (SEMAVegetation vegetation in data.Vegetations.Values)
        {
            if (!TryResolveSemaPosition(vegetation.LocalTilePosition, targetX, targetY, isV2, out int x, out int y))
                continue;
            int newId = (int)api.CreateEx((UInt16)x, (UInt16)y, vegetation.Vegetation, growthStage: (int)vegetation.GrowthStage);
            if (newId <= 0)
                continue;
            api.SetResourceState(newId, vegetation.ResourceState);
            api.SetGrowthStage(newId, vegetation.GrowthStage);
            api.SetGrowthProgress(newId, vegetation.GrowthProgress);
            api.SetCurrentHealth(newId, vegetation.Health);
        }
    }

    private void ImportSemaPitch(SEMAData data, int targetX, int targetY, bool isV2)
    {
        GamePitchManagerAPI api = GamePitchManagerAPI.Instance;
        foreach (SEMAPitchTile pitch in data.PitchTiles.Values)
        {
            if (!TryResolveSemaPosition(pitch.LocalTilePosition, targetX, targetY, isV2, out int x, out int y))
                continue;
            int newId = api.CreatePitch(x, y, pitch.PlayerId);
            if (newId <= 0 || !api.TryGetPitchById(newId, out GamePitchDescriptor* created))
                continue;
            created->RandomSeed = pitch.RandomSeed;
            created->State = pitch.State;
            created->FireTimer = pitch.FireTimer;
        }
    }

    private Dictionary<int, int> ImportSemaUnits(SEMAData data, int targetX, int targetY, bool isV2)
    {
        GameUnitManagerAPI api = GameUnitManagerAPI.Instance;
        Dictionary<int, int> idMap = [];
        foreach (KeyValuePair<int, SEMAUnit> pair in data.Units)
        {
            SEMAUnit unit = pair.Value;
            int newId;
            if (isV2)
            {
                if (!TryResolveSemaPosition(unit.LocalTilePosition, targetX, targetY, true, out int x, out int y))
                    continue;
                newId = (int)api.CreateUnitLocal(unit.PlayerId, unit.ColorPlayerId, x, y, unit.HeightElevation, unit.UnitType);
            }
            else
            {
                newId = (int)api.CreateUnitWorld(unit.PlayerId, unit.ColorPlayerId, unit.WorldTilePosition.X, unit.WorldTilePosition.Y, unit.HeightElevation, unit.UnitType);
            }

            if (newId <= 0 || !api.IsValidId(newId))
                continue;
            idMap[pair.Key] = newId;
            api.SetDirection(newId, unit.Direction);
            api.SetMaxHealth(newId, unit.MaxHealth);
            api.SetCurrentHealth(newId, unit.Health);
        }
        return idMap;
    }

    private void ImportSemaTribes(SEMAData data, int targetX, int targetY, bool isV2, Dictionary<int, int> unitIds, Dictionary<int, int> buildingIds)
    {
        GameTribeManagerAPI api = GameTribeManagerAPI.Instance;
        foreach (KeyValuePair<int, SEMATribe> pair in data.Tribes)
        {
            SEMATribe tribe = pair.Value;
            List<int> members = [];
            if (TryGetSemaRemappedId(unitIds, tribe.LeaderUnitId, isV2, out int leaderId))
                members.Add(leaderId);
            foreach (KeyValuePair<int, SEMAUnit> unitPair in data.Units)
            {
                if (unitPair.Value.TribeId == pair.Key && unitIds.TryGetValue(unitPair.Key, out int newUnitId) && !members.Contains(newUnitId))
                    members.Add(newUnitId);
            }
            if (members.Count == 0)
                continue;

            int newTribeId = (int)api.Create(tribe.PlayerId, false);
            if (newTribeId <= 0 || !api.IsValidId(newTribeId))
                continue;
            foreach (int unitId in members)
                api.AssignUnit(newTribeId, unitId);
            api.SetStance(newTribeId, tribe.Stance);
            RestoreSemaTribeOrder(api, newTribeId, tribe, targetX, targetY, isV2, unitIds, buildingIds);
        }
    }

    private void RestoreSemaTribeOrder(GameTribeManagerAPI api, int tribeId, SEMATribe tribe, int targetX, int targetY, bool isV2, Dictionary<int, int> unitIds, Dictionary<int, int> buildingIds)
    {
        SEMATribeOrderContext? order = tribe.OrderContext;
        if (order == null)
            return;
        api.SetAttackNearestUnit(tribeId, order.AnimalAttackNearestUnit);

        if (order.PatrolMode != TribePatrolMode.None && order.PatrolPoints != null && order.PatrolPoints.Length > 0)
        {
            UnmanagedVector2<UInt16>[] points = new UnmanagedVector2<UInt16>[order.PatrolPoints.Length];
            for (int i = 0; i < points.Length; i++)
            {
                if (!TryResolveSemaPosition(order.PatrolPoints[i], targetX, targetY, isV2, out int x, out int y))
                    return;
                points[i] = new UnmanagedVector2<UInt16>((UInt16)x, (UInt16)y);
            }
            int current = order.CurrentPatrolPoint >= 0 && order.CurrentPatrolPoint < points.Length ? order.CurrentPatrolPoint : 0;
            api.SetPatrolPath(tribeId, points, order.PatrolMode, current, tribe.MoveType);
            return;
        }

        int tileX = 0;
        int tileY = 0;
        bool hasTile = TryResolveSemaPosition(order.LocalTile, targetX, targetY, isV2, out tileX, out tileY);
        switch (order.Command)
        {
            case TribeAICommand.MoveHerePosition when hasTile:
                api.IssueMoveHereCommand(tribeId, tileX, tileY, false, 1, tribe.MoveType);
                break;
            case TribeAICommand.AttackTilePosition when hasTile:
                api.AttackTile(tribeId, tileX, tileY);
                break;
            case TribeAICommand.DigMoatTileId when hasTile:
                api.DigMoat(tribeId, tileX, tileY);
                break;
            case TribeAICommand.ThrowLava when hasTile:
                api.ThrowLava(tribeId, tileX, tileY);
                break;
            case TribeAICommand.AttackWallTileId when !isV2 && IsValidTileId(order.TileId):
                api.AttackWall(tribeId, order.TileId);
                break;
            case TribeAICommand.AttackWallTileId when hasTile:
                api.AttackWall(tribeId, GetTileId(tileX, tileY));
                break;
            case TribeAICommand.AttachLadderToWall when !isV2 && IsValidTileId(order.TileId):
                api.AttachLadderToWall(tribeId, order.TileId);
                break;
            case TribeAICommand.AttachLadderToWall when hasTile:
                api.AttachLadderToWall(tribeId, GetTileId(tileX, tileY));
                break;
            case TribeAICommand.AttackUnit when TryGetSemaRemappedId(unitIds, order.EntityId, isV2, out int unitId):
                api.AttackUnit(tribeId, unitId);
                break;
            case TribeAICommand.ManSiegeEquipment when TryGetSemaRemappedId(unitIds, order.EntityId, isV2, out int siegeId):
                api.ManSiegeEquipment(tribeId, siegeId);
                break;
            case TribeAICommand.DissolveSiegeEquipment when TryGetSemaRemappedId(unitIds, order.EntityId, isV2, out int dissolveId):
                api.DissolveSiegeEquipment(tribeId, dissolveId);
                break;
            case TribeAICommand.AttackBuilding when TryGetSemaRemappedId(buildingIds, order.EntityId, isV2, out int buildingId):
                api.AttackBuilding(tribeId, buildingId);
                break;
            case TribeAICommand.ManPitchCauldronOrBuildTent when TryGetSemaRemappedId(buildingIds, order.EntityId, isV2, out int productionId):
                api.ManPitchCauldronOrBuildTent(tribeId, productionId);
                break;
            case TribeAICommand.BuildTunnel when TryGetSemaRemappedId(buildingIds, order.EntityId, isV2, out int tunnelId):
                api.BuildTunnel(tribeId, tunnelId);
                break;
            case TribeAICommand.UnitDissolve:
                api.Dissolve(tribeId);
                break;
            case TribeAICommand.UnitStop:
                api.Stop(tribeId);
                break;
        }
    }

    private static bool TryGetSemaRemappedId(Dictionary<int, int> idMap, int oldId, bool isV2, out int newId)
    {
        if (idMap.TryGetValue(oldId, out newId))
            return true;
        return !isV2 && oldId > 0 && idMap.TryGetValue(oldId - 1, out newId);
    }

    private void RefreshSemaArea(int targetX, int targetY, int width, int height)
    {
        for (int y = 0; y < height; y += 8)
            for (int x = 0; x < width; x += 8)
                RefreshTileAreaVisuals(targetX + x, targetY + y);
        RefreshTileAreaVisuals(targetX + width - 1, targetY + height - 1);
    }
    #endregion

    #region A* Pathfinding

    /// <summary>
    /// A private helper class to store node data for the A* pathfinding algorithm.
    /// </summary>
    private sealed class PathNode : IEquatable<PathNode>
    {
        public ushort X { get; }
        public ushort Y { get; }

        /// <summary>Cost of the path from the start node to this node.</summary>
        public int GCost { get; set; }

        /// <summary>Heuristic cost from this node to the end node.</summary>
        public int HCost { get; set; }

        /// <summary>Total estimated cost (GCost + HCost).</summary>
        public int FCost => GCost + HCost;

        /// <summary>The node that came before this one on the path.</summary>
        public PathNode Parent { get; set; }

        public PathNode(ushort x, ushort y)
        {
            X = x;
            Y = y;
        }

        // IEquatable implementation to allow for efficient lookups in a HashSet.
        public bool Equals(PathNode other)
        {
            if (other is null) return false;
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj) => Equals(obj as PathNode);
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 23 + X.GetHashCode();
                hash = hash * 23 + Y.GetHashCode();
                return hash;
            }
        }
    }

    /// <summary>
    /// Finds the shortest path between two tiles using the A* algorithm.
    /// The path avoids impassable terrain, buildings, and prevents "corner cutting" through diagonal obstacles.
    /// </summary>
    /// <param name="startX">The starting X coordinate.</param>
    /// <param name="startY">The starting Y coordinate.</param>
    /// <param name="endX">The destination X coordinate.</param>
    /// <param name="endY">The destination Y coordinate.</param>
    /// <returns>
    /// A list of vectors representing the tile coordinates in the path from start to end.
    /// Returns an empty list if no path is found. The start point is not included in the path.
    /// </returns>
    [LuaApiExport("FindPath")]
    public List<UnmanagedVector2<UInt16>> FindPath(int startX, int startY, int endX, int endY)
    {
        // Handle the edge case where the start and end are the same.
        if (startX == endX && startY == endY)
        {
            return new List<UnmanagedVector2<UInt16>>();
        }

        PathNode startNode = new PathNode((ushort)startX, (ushort)startY);
        PathNode endNode = new PathNode((ushort)endX, (ushort)endY);

        List<PathNode> openSet = new List<PathNode>();
        HashSet<PathNode> closedSet = new HashSet<PathNode>();

        // Check if start or end points are blocked.
        if (!IsTileWalkableAndUnoccupied(GetTileId(startX, startY)) || !IsTileWalkableAndUnoccupied(GetTileId(endX, endY)))
        {
            return new List<UnmanagedVector2<UInt16>>();
        }

        openSet.Add(startNode);

        while (openSet.Count > 0)
        {
            PathNode currentNode = openSet[0];
            for (int i = 1; i < openSet.Count; i++)
            {
                if (openSet[i].FCost < currentNode.FCost ||
                   (openSet[i].FCost == currentNode.FCost && openSet[i].HCost < currentNode.HCost))
                {
                    currentNode = openSet[i];
                }
            }

            openSet.Remove(currentNode);
            closedSet.Add(currentNode);

            if (currentNode.Equals(endNode))
            {
                return RetracePath(startNode, currentNode);
            }

            // --- Neighbor Processing Logic ---
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;

                    int checkX = currentNode.X + x;
                    int checkY = currentNode.Y + y;

                    if (checkX < 0 || checkX >= MAX_WIDTH || checkY < 0 || checkY >= MAX_HEIGHT)
                    {
                        continue;
                    }

                    // ===================================================================
                    // Corner Cutting Prevention Logic
                    // ===================================================================
                    // If this is a diagonal move (x and y are both non-zero)...
                    if (x != 0 && y != 0)
                    {
                        // we must check the two adjacent cardinal tiles.
                        // If either of them is blocked, this diagonal path is invalid.
                        if (!IsTileWalkableAndUnoccupied(GetTileId(currentNode.X + x, currentNode.Y)) ||
                            !IsTileWalkableAndUnoccupied(GetTileId(currentNode.X, currentNode.Y + y)))
                        {
                            continue; // Skip this neighbor, the path is blocked.
                        }
                    }
                    // ===================================================================

                    // Check if the neighbor is on the closed list or is unwalkable itself.
                    PathNode tempNeighbor = new PathNode((ushort)checkX, (ushort)checkY);
                    if (closedSet.Contains(tempNeighbor) || !IsTileWalkableAndUnoccupied(GetTileId(checkX, checkY)))
                    {
                        continue;
                    }

                    int moveCostToNeighbor = currentNode.GCost + GetDistance(currentNode, tempNeighbor);

                    PathNode openSetNeighbor = null;
                    foreach (PathNode node in openSet)
                    {
                        if (node.Equals(tempNeighbor))
                        {
                            openSetNeighbor = node;
                            break;
                        }
                    }

                    // Case 1: The neighbor is already in the open set.
                    if (openSetNeighbor != null)
                    {
                        if (moveCostToNeighbor < openSetNeighbor.GCost)
                        {
                            openSetNeighbor.GCost = moveCostToNeighbor;
                            openSetNeighbor.Parent = currentNode;
                        }
                    }
                    else // Case 2: The neighbor is not in the open set.
                    {
                        PathNode neighbor = tempNeighbor;
                        neighbor.GCost = moveCostToNeighbor;
                        neighbor.HCost = GetDistance(neighbor, endNode);
                        neighbor.Parent = currentNode;
                        openSet.Add(neighbor);
                    }
                }
            }
        }

        // No Path Found
        return new List<UnmanagedVector2<UInt16>>();
    }

    /// <summary>
    /// Reconstructs the path by tracing back from the end node via its parents.
    /// </summary>
    private List<UnmanagedVector2<UInt16>> RetracePath(PathNode startNode, PathNode endNode)
    {
        List<UnmanagedVector2<ushort>> path = new List<UnmanagedVector2<UInt16>>();
        PathNode currentNode = endNode;

        while (currentNode != null && !currentNode.Equals(startNode))
        {
            path.Add(new UnmanagedVector2<UInt16>(currentNode.X, currentNode.Y));
            currentNode = currentNode.Parent;
        }

        // The path is currently from end-to-start, so we reverse it.
        path.Reverse();
        return path;
    }

    /// <summary>
    /// Calculates the heuristic distance between two nodes (Manhattan distance).
    /// Diagonal moves cost 14, and cardinal moves cost 10 to approximate sqrt(2)
    /// while avoiding floating-point math.
    /// </summary>
    private int GetDistance(PathNode nodeA, PathNode nodeB)
    {
        int dstX = Math.Abs(nodeA.X - nodeB.X);
        int dstY = Math.Abs(nodeA.Y - nodeB.Y);

        if (dstX > dstY)
            return 14 * dstY + 10 * (dstX - dstY);
        return 14 * dstX + 10 * (dstY - dstX);
    }

    #endregion

#pragma warning restore 0618
}
