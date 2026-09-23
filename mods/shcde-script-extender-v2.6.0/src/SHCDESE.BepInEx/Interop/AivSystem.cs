using SHCDESE.Interop.Enums;
using SHCDESE.Lua.CodeGen;
using System;
using System.Runtime.InteropServices;

namespace SHCDESE.Interop;

/// <summary>
/// One native construction step in a decoded AIV castle layout.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct AivBuildStep
{
    public const int SIZE = 0x0C;

    [LuaExposed] public AivBuildStepState State;
    [LuaExposed] public byte RebuildDelay;
    [LuaExposed] public eMappers BuildingType;
    [LuaExposed] public UInt16 TileCount;
    [LuaExposed] public UInt16 Unknown06;

    /// <summary>
    /// Absolute packed map-tile ID for a single placement, or an index into the owning village's ordered map-tile buffer for grouped wall, crenel, moat, and pitch steps.
    /// </summary>
    [LuaExposed] public Int32 MapTileIdOrBufferIndex;
}

/// <summary>
/// Live state for one native AIV village slot.
/// Slot zero is reserved; normal live slots are one through eight.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct AivVillageState
{
    public const int SIZE = 0x6D98;
    public const int BUILD_STEP_CAPACITY = 1_000;
    public const int ORDERED_MAP_TILE_CAPACITY = 4_000;

    [LuaExposed] public Int32 OwnerPlayerId;
    [LuaExposed] public AILords AILord;
    [LuaExposed] public AivRotation Rotation;
    [LuaExposed] public Int32 SelectedVariantIndex;
    [LuaExposed] public AivLayoutSelectionState LayoutSelectionState;

    [LuaExposed] public Int32 UnlockedBuildStep;
    [LuaExposed] public Int32 LowGoldBuildDelayElapsed;
    [LuaExposed] public Int32 BuildRate;
    [LuaExposed] public Int32 MaximumBuildStep;

    [LuaExposed] public Int32 LayoutOriginX;
    [LuaExposed] public Int32 LayoutOriginY;
    [LuaExposed] public Int32 KeepX;
    [LuaExposed] public Int32 KeepY;

    public fixed byte BuildStepsBuffer[BUILD_STEP_CAPACITY * AivBuildStep.SIZE];
    public fixed Int32 OrderedMapTileIds[ORDERED_MAP_TILE_CAPACITY];

    [LuaExposed] public Int32 OrderedMapTileCount;
}

/// <summary>
/// One 5x5-map-tile cell in the live 160x160 AIV coarse map.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct AivCoarseCell
{
    public const int SIZE = 0x30;

    [LuaExposed] public UInt32 CoarseSearchGeneration;
    [LuaExposed] public byte ForeignPathComponentTileCount;
    [LuaExposed] public byte CoarseSearchDepth;
    [LuaExposed] public byte Unknown06;
    [LuaExposed] public byte TreeObstructionWeight;

    [LuaExposed] public byte StoneTileCount;
    [LuaExposed] public byte IronTileCount;
    [LuaExposed] public byte PitchTileCount;
    [LuaExposed] public byte SwampTileCount;

    [LuaExposed] public byte MinimumHeight;
    [LuaExposed] public byte MaximumHeight;
    [LuaExposed] public byte HeightRangeExceeds12;
    [LuaExposed] public byte StructureOrReservationCount;
    [LuaExposed] public byte OutsideUsableMap;

    [LuaExposed] public byte UnknownFlags91Count;
    [LuaExposed] public byte UnknownFlags90Count;
    [LuaExposed] public byte WoodcutterRetryDelay;
    [LuaExposed] public byte Unknown14;
    [LuaExposed] public byte OccupyingPlayerId;
    [LuaExposed] public byte ImpassableEdgeTileCount;
    [LuaExposed] public byte WoodcutterRemovalCount;
    [LuaExposed] public byte CombinedUnknownFlags;

    public fixed byte Unknown19[23];
}

/// <summary>
/// Complete native AIV live-state and working-buffer system.
/// This type is intentionally sequential: unknown storage is retained in its observed order.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct AivSystem
{
    public const int SIZE = 0x1BBF58;
    public const int VILLAGE_SLOT_COUNT = 9;
    public const int FIRST_LIVE_VILLAGE_SLOT = 1;
    public const int LAST_LIVE_VILLAGE_SLOT = 8;
    public const int LIVE_VILLAGE_SLOT_COUNT = 8;
    public const int LAYOUT_GRID_WIDTH = 100;
    public const int LAYOUT_GRID_HEIGHT = 100;
    public const int LAYOUT_GRID_CELL_COUNT = LAYOUT_GRID_WIDTH * LAYOUT_GRID_HEIGHT;
    public const int MISC_ITEM_TYPE_CAPACITY = 32;
    public const int MISC_ITEM_POSITIONS_PER_TYPE = 10;
    public const int MISC_ITEM_POSITION_BUFFER_LENGTH = MISC_ITEM_TYPE_CAPACITY * MISC_ITEM_POSITIONS_PER_TYPE;
    public const int MISC_ITEM_POSITION_CAPACITY = 10;
    public const int PAUSE_FRAME_CAPACITY = 50;
    public const int UNKNOWN_COARSE_STATE_SIZE = 800;
    public const int COARSE_GRID_WIDTH = 160;
    public const int COARSE_GRID_HEIGHT = 160;
    public const int COARSE_GRID_CELL_COUNT = COARSE_GRID_WIDTH * COARSE_GRID_HEIGHT;

    [LuaExposed] public Int32 Unknown000000;

    public AivVillageState ReservedVillageSlot;
    public AivVillageState VillageSlot1;
    public AivVillageState VillageSlot2;
    public AivVillageState VillageSlot3;
    public AivVillageState VillageSlot4;
    public AivVillageState VillageSlot5;
    public AivVillageState VillageSlot6;
    public AivVillageState VillageSlot7;
    public AivVillageState VillageSlot8;

    [LuaExposed] public Int32 KeepPlacementX;
    [LuaExposed] public Int32 KeepPlacementY;
    [LuaExposed] public Int32 Unknown03DA64;
    [LuaExposed] public Int32 Unknown03DA68;
    // Fixed buffers require primitive element types; the API projects these as Span<eMappers>.
    public fixed Int16 BuildingTypeGrid[LAYOUT_GRID_CELL_COUNT];
    public fixed Int32 BuildStepGrid[LAYOUT_GRID_CELL_COUNT];
    public fixed Int16 RotatedBuildingTypeGrid[LAYOUT_GRID_CELL_COUNT];
    public fixed Int32 RotatedBuildStepGrid[LAYOUT_GRID_CELL_COUNT];

    public fixed Int32 PauseFrameIndices[PAUSE_FRAME_CAPACITY];
    [LuaExposed] public Int32 PauseDelay;
    // AivMiscItemType selects a ten-position group; each stored element is a packed local tile ID.
    public fixed Int32 MiscItemPositionBuffer[MISC_ITEM_POSITION_BUFFER_LENGTH];

    [LuaExposed] public Int32 PlacementEvaluatedTileCount;
    [LuaExposed] public Int32 PlacementObstructedTileCount;
    [LuaExposed] public Int32 TotalTreeObstructionWeight;
    [LuaExposed] public Int32 DominantPathComponentId;

    [LuaExposed] public Int32 AiUpdatePhase;
    [LuaExposed] public Int32 CoarseSearchGeneration;
    public fixed byte UnknownCoarseGridState[UNKNOWN_COARSE_STATE_SIZE];

    public fixed byte CoarseGridBuffer[COARSE_GRID_CELL_COUNT * AivCoarseCell.SIZE];

    [LuaExposed] public Int32 CoarseSearchDepth;
    [LuaExposed] public Int32 CoarseSearchQueueReadIndex;
    [LuaExposed] public Int32 CoarseSearchQueueWriteIndex;
    public fixed Int32 CoarseSearchQueueX[COARSE_GRID_CELL_COUNT];
    public fixed Int32 CoarseSearchQueueY[COARSE_GRID_CELL_COUNT];
    [LuaExposed] public Int32 CoarseSearchResultX;
    [LuaExposed] public Int32 CoarseSearchResultY;

    public fixed byte PlacementVisitedMask[LAYOUT_GRID_CELL_COUNT];
    [LuaExposed] public Int32 ActiveVillageCount;
}
