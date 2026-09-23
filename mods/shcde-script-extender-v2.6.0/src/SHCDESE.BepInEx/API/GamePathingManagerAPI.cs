using RedBird.Core.Memory;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SHCDESE.API;

/// <summary>
/// Provides live access to the game's local path-component grid, macro connections, moat-work tasks, and per-unit packed path plans.
/// </summary>
[LuaApiNamespace("Pathing")]
public unsafe sealed class GamePathingManagerAPI
{
    public const int FIRST_PATH_CONNECTION_RECORD_ID = 1;
    public const int LAST_PATH_CONNECTION_RECORD_ID = 199;
    public const int PACKED_PLAN_BYTES_PER_UNIT = GameUnitManager.PackedPathPlanBytesPerUnit;
    public const int MAX_PATH_PLAN_TRANSITIONS = GameUnitManager.PackedPathPlanTransitionsPerUnit;
    public const int PATHFINDING_UNIT_TYPE_COUNT = (Int32)eChimps.CHIMP_NUM_TYPES;
    public const int FIRST_PATH_CONNECTION_CLASS = (Int32)PathConnectionClass.LadderClimb;
    public const int LAST_PATH_CONNECTION_CLASS = (Int32)PathConnectionClass.Connection6;

    private const int PATH_CONNECTION_CLASS_COUNT = LAST_PATH_CONNECTION_CLASS - FIRST_PATH_CONNECTION_CLASS + 1;
    private const int SELECTED_UNIT_TYPE_COUNTER_COUNT = 35;
    private const int NATIVE_ASSASSIN_SELECTION_COUNTER_INDEX = 22;

    private static readonly Lazy<GamePathingManagerAPI> _lazy = new(() => new GamePathingManagerAPI());
    public static GamePathingManagerAPI Instance => _lazy.Value;

    private readonly IntPtr _pathfindingContext;
    private readonly GamePathfindingContextView _pathfindingContextView;
    private readonly GameTileManagerView _tileManagerView;
    private readonly byte* _unitManager;
    private readonly SimpleNativeArray<PathConnectionRecord> _pathConnectionRecordArray;
    private readonly PathfindingProfile* _unitTypePathfindingProfiles;
    private readonly Int32* _unitTypePathfindingConnectionClasses;
    private readonly Boolean[] _unitTypeNextTileSurfaceValidationBypasses = new Boolean[PATHFINDING_UNIT_TYPE_COUNT];
    private readonly Boolean[] _unitTypeAssassinOnlySelectionOverrides = new Boolean[SELECTED_UNIT_TYPE_COUNTER_COUNT];
    private Int32 _assassinOnlySelectionOverrideCount;

    private GamePathingManagerAPI()
    {
        _pathfindingContext = GameTileManagerAPI.Instance.GetPathfindingContext();
        _pathfindingContextView = new GamePathfindingContextView(_pathfindingContext);
        _tileManagerView = GameTileManagerAPI.Instance.TileManager;
        _unitManager = (Byte*)GameUnitManagerAPI.Instance.GetUnitManager().Pointer;
        _unitTypePathfindingProfiles = (PathfindingProfile*)(GameGlobalsManager.Instance.PathfindingProfilesUnitTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle);
        _unitTypePathfindingConnectionClasses = (Int32*)GameGlobalsManager.Instance.PathfindingConnectionClassesUnitTableVA;

        _pathConnectionRecordArray = new SimpleNativeArray<PathConnectionRecord>((byte*)_pathfindingContext + GamePathfindingContextView.PathConnectionRecordsOffset, GamePathfindingContextView.PathConnectionRecordCapacity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GamePathfindingContextView GetPathfindingContextView() => _pathfindingContextView;

    /// <summary>
    /// Returns the live writable native pathfinding-profile table indexed by <see cref="eChimps"/>.
    /// Writes affect subsequent path searches immediately; existing unit path plans are not rebuilt automatically.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<PathfindingProfile> GetUnitTypePathfindingProfiles()
    {
        return _unitTypePathfindingProfiles == null
            ? Span<PathfindingProfile>.Empty
            : new Span<PathfindingProfile>(_unitTypePathfindingProfiles, PATHFINDING_UNIT_TYPE_COUNT);
    }

    /// <summary>
    /// Returns the complete live writable native connection-class permission table.
    /// Rows are classes 1 through 6 and each row contains one Int32 Boolean value for every unit type.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetUnitTypePathfindingConnectionClasses()
    {
        return _unitTypePathfindingConnectionClasses == null
            ? Span<Int32>.Empty
            : new Span<Int32>(_unitTypePathfindingConnectionClasses, PATHFINDING_UNIT_TYPE_COUNT * PATH_CONNECTION_CLASS_COUNT);
    }

    /// <summary>
    /// Returns one live writable connection-class permission row indexed by <see cref="eChimps"/>.
    /// An empty span is returned for class zero or an unsupported native class.
    /// </summary>
    public Span<Int32> GetUnitTypePathfindingConnectionClassPermissions(PathConnectionClass connectionClass)
    {
        if (!IsValidPathConnectionClass(connectionClass) || _unitTypePathfindingConnectionClasses == null)
            return Span<Int32>.Empty;

        Int32 rowOffset = ((Int32)connectionClass - FIRST_PATH_CONNECTION_CLASS) * PATHFINDING_UNIT_TYPE_COUNT;
        return new Span<Int32>(_unitTypePathfindingConnectionClasses + rowOffset, PATHFINDING_UNIT_TYPE_COUNT);
    }

    /// <summary>
    /// Gets the live pathfinding profile used by a unit type.
    /// </summary>
    [LuaApiExport("GetUnitTypePathfindingProfile")]
    public PathfindingProfile GetUnitTypePathfindingProfile(eChimps unitType)
    {
        if (!IsValidUnitType(unitType) || _unitTypePathfindingProfiles == null)
            return default;

        return _unitTypePathfindingProfiles[(Int32)unitType];
    }

    /// <summary>
    /// Writes the native pathfinding profile used by a unit type.
    /// This does not modify connection-class permissions or the two managed validation overrides.
    /// </summary>
    [LuaApiExport("SetUnitTypePathfindingProfile")]
    public bool SetUnitTypePathfindingProfile(eChimps unitType, PathfindingProfile profile)
    {
        if (!IsValidUnitType(unitType)
            || (UInt32)profile > (UInt32)PathfindingProfile.Default
            || _unitTypePathfindingProfiles == null)
        {
            return false;
        }

        _unitTypePathfindingProfiles[(Int32)unitType] = profile;
        return true;
    }

    /// <summary>
    /// Returns whether the native unit-type table permits a macro-path connection class.
    /// </summary>
    [LuaApiExport("CanUnitTypeUsePathConnectionClass")]
    public bool CanUnitTypeUsePathConnectionClass(eChimps unitType, PathConnectionClass connectionClass)
    {
        if (!IsValidUnitType(unitType) || !IsValidPathConnectionClass(connectionClass))
            return false;

        Span<Int32> permissions = GetUnitTypePathfindingConnectionClassPermissions(connectionClass);
        return !permissions.IsEmpty && permissions[(Int32)unitType] != 0;
    }

    /// <summary>
    /// Writes one native unit-type/connection-class permission.
    /// </summary>
    [LuaApiExport("SetUnitTypeCanUsePathConnectionClass")]
    public bool SetUnitTypeCanUsePathConnectionClass(eChimps unitType, PathConnectionClass connectionClass, bool canUse)
    {
        if (!IsValidUnitType(unitType) || !IsValidPathConnectionClass(connectionClass))
            return false;

        Span<Int32> permissions = GetUnitTypePathfindingConnectionClassPermissions(connectionClass);
        if (permissions.IsEmpty)
            return false;

        permissions[(Int32)unitType] = canUse ? 1 : 0;
        return true;
    }

    /// <summary>
    /// Returns whether the native next-tile wall/elevated-surface rejection is bypassed for a unit type.
    /// This override can only turn a rejection from that specific validator into success.
    /// </summary>
    [LuaApiExport("GetUnitTypeNextTileSurfaceValidationBypass")]
    public bool GetUnitTypeNextTileSurfaceValidationBypass(eChimps unitType)
    {
        return IsValidUnitType(unitType) && _unitTypeNextTileSurfaceValidationBypasses[(Int32)unitType];
    }

    /// <summary>
    /// Enables or removes the next-tile wall/elevated-surface validation bypass for a unit type.
    /// Other transition, linkage, connection and behavior checks remain active.
    /// </summary>
    [LuaApiExport("SetUnitTypeNextTileSurfaceValidationBypass")]
    public bool SetUnitTypeNextTileSurfaceValidationBypass(eChimps unitType, bool bypass)
    {
        if (!IsValidUnitType(unitType))
            return false;

        _unitTypeNextTileSurfaceValidationBypasses[(Int32)unitType] = bypass;
        return true;
    }

    /// <summary>
    /// Returns whether this unit type is accepted by the managed assassin-only selection override.
    /// </summary>
    [LuaApiExport("GetUnitTypeAssassinOnlySelectionOverride")]
    public bool GetUnitTypeAssassinOnlySelectionOverride(eChimps unitType)
    {
        Int32 counterIndex = GetSelectedUnitTypeCounterIndex(unitType);
        return counterIndex >= 0 && _unitTypeAssassinOnlySelectionOverrides[counterIndex];
    }

    /// <summary>
    /// Allows a unit type to pass checks that normally require an assassin-only selection.
    /// Only the 35 unit types represented by the native selected-unit counter table are supported.
    /// The hook reports a custom success when every populated counter belongs to either Arabian
    /// assassins or unit types with this override.
    /// </summary>
    [LuaApiExport("SetUnitTypeAssassinOnlySelectionOverride")]
    public bool SetUnitTypeAssassinOnlySelectionOverride(eChimps unitType, bool enabled)
    {
        Int32 counterIndex = GetSelectedUnitTypeCounterIndex(unitType);
        if (counterIndex < 0)
            return false;

        if (_unitTypeAssassinOnlySelectionOverrides[counterIndex] == enabled)
            return true;

        _unitTypeAssassinOnlySelectionOverrides[counterIndex] = enabled;
        _assassinOnlySelectionOverrideCount += enabled ? 1 : -1;
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool ShouldBypassNextTileSurfaceValidation(Int32 unitId)
    {
        if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit) || unit == null)
            return false;

        eChimps unitType = unit->r_UnitChimp;
        return IsValidUnitType(unitType) && _unitTypeNextTileSurfaceValidationBypasses[(Int32)unitType];
    }

    /// <summary>
    /// Extends the native assassin-only selection predicate using the same 35-entry selected-unit-type
    /// counter table populated by c_game_unit_refresh_selected_unit_type_amounts.
    /// </summary>
    internal bool DoesSelectionContainOnlyAssassinsOrOverrides(GameUnitManager* unitManager)
    {
        if (_assassinOnlySelectionOverrideCount == 0 || unitManager == null)
            return false;

        UInt32* selectedUnitTypeCounts = &unitManager->r_SelectedArchersAmount;
        bool hasSelectedOverride = false;
        for (Int32 counterIndex = 0; counterIndex < SELECTED_UNIT_TYPE_COUNTER_COUNT; counterIndex++)
        {
            if (selectedUnitTypeCounts[counterIndex] == 0)
                continue;

            if (counterIndex == NATIVE_ASSASSIN_SELECTION_COUNTER_INDEX)
                continue;

            if (!_unitTypeAssassinOnlySelectionOverrides[counterIndex])
                return false;

            hasSelectedOverride = true;
        }

        return hasSelectedOverride;
    }

    /// <summary>
    /// Returns the live writable UInt16 PCL grid containing exactly 320,800 packed tiles.
    /// Direct writes do not update component counts or the macro-connection graph.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetPathComponentGrid() => _tileManagerView.PathConnectionGrid;

    /// <summary>
    /// Returns the live writable eight-direction edge-mask grid.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<byte> GetPathEdgeMaskGrid() => _tileManagerView.PathEdgeMaskGrid;

    /// <summary>
    /// Returns the live writable stock component-size table. Its only valid indices are 0..999.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetNativeComponentTileCounts() => _pathfindingContextView.ComponentTileCounts;

    /// <summary>
    /// Returns the live writable stock component visitation table. Its only valid indices are 0..999.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Int32> GetNativeComponentVisitGenerations() => _pathfindingContextView.ComponentVisitGenerations;

    /// <summary>
    /// Returns all live native connection records, including reserved record zero.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<PathConnectionRecord> GetPathConnectionArray() => _pathConnectionRecordArray;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<PathConnectionRecord> GetPathConnectionRecords() => _pathfindingContextView.PathConnectionRecords;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<UInt16> GetMoatWorkTaskIndexGrid() => _tileManagerView.MoatWorkTaskIndexGrid;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<MoatWorkTask> GetMoatWorkTasks() => _tileManagerView.MoatWorkTasks;

    /// <summary>
    /// Returns the complete live 1,000-byte packed plan buffer for a unit, or an empty span for an invalid unit ID.
    /// Direction nibbles beyond GameUnit.r_PathPlanLength are inactive but remain writable native storage.
    /// </summary>
    public Span<byte> GetUnitPathPlanBuffer(int unitId)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan)
            ? pathPlan.PackedBytes
            : Span<byte>.Empty;
    }

    /// <summary>
    /// Returns the live portion of the packed buffer occupied by the current plan.
    /// Its byte length is (r_PathPlanLength + 1) / 2.
    /// </summary>
    public Span<byte> GetActiveUnitPathPlanBuffer(int unitId)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan)
            ? pathPlan.ActivePackedBytes
            : Span<byte>.Empty;
    }

    /// <summary>
    /// Creates a small live-memory view over one unit record and its separate packed path-plan buffer.
    /// Native plan storage is indexed by unit ID and only has slots 0..9999; slot zero is reserved.
    /// </summary>
    public bool TryGetUnitPathPlanView(int unitId, out GameUnitPathPlanView pathPlan)
    {
        pathPlan = null!;
        if (unitId < GameUnitManager.FirstLiveUnitId
            || unitId > GameUnitManager.LastLiveUnitId
            || _unitManager == null
            || !GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit)
            || unit == null)
        {
            return false;
        }

        Byte* packedPlan = _unitManager
            + GameUnitManager.PackedPathPlanBlockOffset
            + GameUnitManager.PackedPathPlanBytesPerUnit * unitId;

        pathPlan = new GameUnitPathPlanView(unit, packedPlan);
        return true;
    }

    [LuaApiExport("GetUnitPathPlanLength")]
    public int GetUnitPathPlanLength(int unitId)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) ? pathPlan.Length : -1;
    }

    [LuaApiExport("SetUnitPathPlanLength")]
    public bool SetUnitPathPlanLength(int unitId, int transitionCount, bool clampCurrentIndex = true)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan)
            && pathPlan.TrySetLength(transitionCount, clampCurrentIndex);
    }

    [LuaApiExport("GetUnitPathPlanCurrentIndex")]
    public int GetUnitPathPlanCurrentIndex(int unitId)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) ? pathPlan.CurrentIndex : -1;
    }

    [LuaApiExport("SetUnitPathPlanCurrentIndex")]
    public bool SetUnitPathPlanCurrentIndex(int unitId, int transitionIndex)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan)
            && pathPlan.TrySetCurrentIndex(transitionIndex);
    }

    [LuaApiExport("ClearUnitPathPlan")]
    public bool ClearUnitPathPlan(int unitId)
    {
        if (!TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan))
            return false;

        pathPlan.Clear();
        return true;
    }

    /// <summary>Replaces the unit's live packed plan and sets its current transition cursor.</summary>
    public bool ReplaceUnitPathPlan(int unitId, ReadOnlySpan<PackedPathDirection> directions, int currentIndex = 0)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) && pathPlan.TryReplace(directions, currentIndex);
    }

    /// <summary>Reads a direction from physical plan capacity, even if it is beyond the active length.</summary>
    public bool TryGetRawPackedPathDirection(int unitId, int transitionIndex, out PackedPathDirection direction)
    {
        direction = default;
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) && pathPlan.TryGetRawDirection(transitionIndex, out direction);
    }

    /// <summary>Writes a direction in physical plan capacity, even if it is beyond the active length.</summary>
    public bool SetRawPackedPathDirection(int unitId, int transitionIndex, PackedPathDirection direction)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) && pathPlan.TrySetRawDirection(transitionIndex, direction);
    }

    [LuaApiExport("GetComponentId")]
    public int GetPathComponentId(int tileX, int tileY)
    {
        return TryGetPackedTileId(tileX, tileY, out int tileId) ? GetPathComponentGrid()[tileId] : 0;
    }

    public UInt16 GetPathComponentIdByTileId(int tileId)
    {
        return IsValidPackedTileId(tileId) ? GetPathComponentGrid()[tileId] : (UInt16)0;
    }
    public bool TryGetPathComponentId(int tileX, int tileY, out UInt16 componentId)
    {
        componentId = 0;
        if (!TryGetPackedTileId(tileX, tileY, out int tileId))
            return false;

        componentId = GetPathComponentGrid()[tileId];
        return true;
    }

    /// <summary>
    /// Returns a live decoded direction view, or null for an invalid unit ID.
    /// Indexer reads and writes operate on the game's packed native plan buffer.
    /// </summary>
    public GameUnitPathPlanView? GetUnitPathPlanDirections(int unitId)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) ? pathPlan : null;
    }

    public bool TryGetPathComponentIdByTileId(int tileId, out UInt16 componentId)
    {
        componentId = 0;
        if (!IsValidPackedTileId(tileId))
            return false;

        componentId = GetPathComponentGrid()[tileId];
        return true;
    }

    /// <summary>
    /// Writes one live PCL label. This deliberately does not repair counts, connections, or other derived state.
    /// </summary>
    [LuaApiExport("SetComponentId")]
    public bool SetPathComponentId(int tileX, int tileY, int componentId)
    {
        if ((UInt32)componentId > UInt16.MaxValue
            || !TryGetPackedTileId(tileX, tileY, out int tileId))
        {
            return false;
        }

        GetPathComponentGrid()[tileId] = (UInt16)componentId;
        return true;
    }

    [LuaApiExport("GetEdgeMask")]
    public PathEdgeMask GetPathEdgeMask(int tileX, int tileY)
    {
        if (!TryGetPackedTileId(tileX, tileY, out int tileId))
            return PathEdgeMask.None;

        return (PathEdgeMask)GetPathEdgeMaskGrid()[tileId];
    }

    /// <summary>
    /// Writes one live tile edge mask.
    /// Prefer RecalculateTileAndNeighbours after changing terrain/building state.
    /// </summary>
    [LuaApiExport("SetEdgeMask")]
    public bool SetPathEdgeMask(int tileX, int tileY, PathEdgeMask edgeMask)
    {
        if (!TryGetPackedTileId(tileX, tileY, out int tileId))
            return false;

        GetPathEdgeMaskGrid()[tileId] = (Byte)edgeMask;
        return true;
    }

    [LuaApiExport("HasEdge")]
    public bool HasPathEdge(int tileX, int tileY, PackedPathDirection direction)
    {
        if ((UInt32)direction > (UInt32)PackedPathDirection.NorthWest)
            return false;

        PathEdgeMask directionMask = (PathEdgeMask)(1 << (Int32)direction);
        return (GetPathEdgeMask(tileX, tileY) & directionMask) != 0;
    }

    [LuaApiExport("GetComponentGeneration")]
    public UInt32 GetComponentGeneration() => _pathfindingContextView.ComponentGeneration;

    [LuaApiExport("GetNextComponentId")]
    public int GetNextComponentId() => _pathfindingContextView.NextComponentId;

    /// <summary>
    /// Returns the exclusive upper bound assigned by the last component rebuild.
    /// Component zero is reserved, so labelled components normally occupy 1..value-1.
    /// </summary>
    [LuaApiExport("GetComponentExclusiveUpperBound")]
    public int GetPathComponentExclusiveUpperBound() => _pathfindingContextView.NextComponentId;

    [LuaApiExport("GetTotalLabelledTiles")]
    public int GetTotalLabelledTiles() => _pathfindingContextView.TotalLabelledTiles;

    [LuaApiExport("GetNativeComponentTileCount")]
    public int GetNativeComponentTileCount(int componentId)
    {
        Span<Int32> counts = GetNativeComponentTileCounts();
        return (UInt32)componentId < (UInt32)counts.Length ? counts[componentId] : 0;
    }

    [LuaApiExport("RequestComponentRebuild")]
    public void RequestComponentRebuild()
    {
        _pathfindingContextView.ComponentGridDirty = 1;
    }

    [LuaApiExport("RecalculateTileAndNeighbours")]
    public bool RecalculateTileAndNeighbours(int tileX, int tileY)
    {
        if (!TryGetPackedTileId(tileX, tileY, out int tileId)
            || BulkTileDetours.c_game_update_pathfinding_for_tile_and_neighbors3x3 == null)
        {
            return false;
        }

        BulkTileDetours.c_game_update_pathfinding_for_tile_and_neighbors3x3(_pathfindingContext, tileY, tileId);
        RequestComponentRebuild();
        return true;
    }

    /// <summary>
    /// Returns the first PCL to enter when routing from currentComponentId toward destinationComponentId.
    /// </summary>
    [LuaApiExport("FindNextComponentTowardDestination")]
    public int FindNextComponentTowardDestination(int playerId, int currentComponentId, int destinationComponentId, PathConnectionQueryMode queryMode = PathConnectionQueryMode.ExcludeLadderClimb)
    {
        if ((UInt32)currentComponentId > UInt16.MaxValue
            || (UInt32)destinationComponentId > UInt16.MaxValue
            || (UInt32)queryMode > (UInt32)PathConnectionQueryMode.LadderClimbOnly
            || BulkPathingDetours.c_game_pathfinding_find_next_component_toward_destinaton == null)
        {
            return 0;
        }

        return (Int32)BulkPathingDetours.c_game_pathfinding_find_next_component_toward_destinaton(_pathfindingContext, playerId, currentComponentId, destinationComponentId, queryMode);
    }

    /// <summary>
    /// Tests macro-graph reachability using the same player and connection-class filtering as the native route query.
    /// </summary>
    [LuaApiExport("AreComponentsConnected")]
    public bool ArePathComponentsConnected(int playerId, int currentComponentId, int destinationComponentId, PathConnectionQueryMode queryMode = PathConnectionQueryMode.ExcludeLadderClimb)
    {
        if (currentComponentId <= 0 || destinationComponentId <= 0)
            return false;

        return currentComponentId == destinationComponentId
            || FindNextComponentTowardDestination(playerId, currentComponentId, destinationComponentId, queryMode) != 0;
    }

    public bool TryGetPathConnectionRecordById(int recordId, out PathConnectionRecord* pathConnectionRecord)
    {
        pathConnectionRecord = null;
        if (_pathConnectionRecordArray._array == null
            || recordId < FIRST_PATH_CONNECTION_RECORD_ID
            || recordId > LAST_PATH_CONNECTION_RECORD_ID)
        {
            return false;
        }

        pathConnectionRecord = &_pathConnectionRecordArray._array[recordId];
        return true;
    }

    public bool TryGetPathConnectionRecordByIdEx(int recordId, out NativePointer<PathConnectionRecord> pathConnectionRecord)
    {
        bool result = TryGetPathConnectionRecordById(recordId, out PathConnectionRecord* recordPointer);
        pathConnectionRecord = new NativePointer<PathConnectionRecord>(recordPointer);
        return result;
    }

    public bool TryGetPathConnectionRecordByBuildingId(int buildingId, out PathConnectionRecord* pathConnectionRecord)
    {
        pathConnectionRecord = null;
        if (buildingId <= 0
            || _pathConnectionRecordArray._array == null
            || !GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building)
            || building == null)
        {
            return false;
        }

        for (int recordId = FIRST_PATH_CONNECTION_RECORD_ID; recordId <= LAST_PATH_CONNECTION_RECORD_ID; recordId++)
        {
            PathConnectionRecord* current = &_pathConnectionRecordArray._array[recordId];
            if (current->r_IsActive == 0 || current->r_BuildingId != buildingId)
                continue;

            pathConnectionRecord = current;
            return true;
        }

        return false;
    }

    public bool TryGetPathConnectionRecordByBuildingIdEx(int buildingId, out NativePointer<PathConnectionRecord> pathConnectionRecord)
    {
        bool result = TryGetPathConnectionRecordByBuildingId(buildingId, out PathConnectionRecord* recordPointer);
        pathConnectionRecord = new NativePointer<PathConnectionRecord>(recordPointer);
        return result;
    }


    public bool TryGetPathConnectionRecordByUnitId(int unitId, out PathConnectionRecord* pathConnectionRecord)
    {
        pathConnectionRecord = null;
        if (unitId <= 0 || _pathConnectionRecordArray._array == null)
            return false;

        for (int recordId = FIRST_PATH_CONNECTION_RECORD_ID; recordId <= LAST_PATH_CONNECTION_RECORD_ID; recordId++)
        {
            PathConnectionRecord* current = &_pathConnectionRecordArray._array[recordId];
            if (current->r_IsActive == 0 || current->r_UnitId != unitId)
                continue;

            pathConnectionRecord = current;
            return true;
        }

        return false;
    }

    public bool TryGetPathConnectionRecordByUnitIdEx(int unitId, out NativePointer<PathConnectionRecord> pathConnectionRecord)
    {
        bool result = TryGetPathConnectionRecordByUnitId(unitId, out PathConnectionRecord* recordPointer);
        pathConnectionRecord = new NativePointer<PathConnectionRecord>(recordPointer);
        return result;
    }

    /// <summary>Appends the IDs of all active native macro-connection records.</summary>
    public void GetActivePathConnectionRecordIds(List<int> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        if (_pathConnectionRecordArray._array == null)
            return;

        for (int recordId = FIRST_PATH_CONNECTION_RECORD_ID; recordId <= LAST_PATH_CONNECTION_RECORD_ID; recordId++)
        {
            if (_pathConnectionRecordArray._array[recordId].r_IsActive != 0)
                results.Add(recordId);
        }
    }

    /// <summary>
    /// Appends IDs of active records that reference a component in any currently mapped component field.
    /// </summary>
    public void GetPathConnectionRecordIdsForComponent(int componentId, List<int> results)
    {
        if (results == null)
            throw new ArgumentNullException(nameof(results));

        if ((UInt32)componentId > UInt16.MaxValue || _pathConnectionRecordArray._array == null)
            return;

        for (int recordId = FIRST_PATH_CONNECTION_RECORD_ID; recordId <= LAST_PATH_CONNECTION_RECORD_ID; recordId++)
        {
            PathConnectionRecord* current = &_pathConnectionRecordArray._array[recordId];
            if (current->r_IsActive != 0
                && (current->r_PathComponentA == componentId
                    || current->r_PathComponentB == componentId
                    || current->r_PathComponentC == componentId))
            {
                results.Add(recordId);
            }
        }
    }

    /// <summary>
    /// Returns the live sparse 50-entry registered-unit ID list for a connection record.
    /// </summary>
    public Span<Int32> GetRegisteredUnitIds(int recordId)
    {
        if (!TryGetPathConnectionRecordById(recordId, out PathConnectionRecord* record))
            return [];

        return new Span<Int32>(&record->r_RegisteredUnitIds[0], 50);
    }

    /// <summary>
    /// Returns the live sparse 50-entry registered-unit global-ID list for a connection record.
    /// </summary>
    public Span<UInt32> GetRegisteredUnitGlobalIds(int recordId)
    {
        if (!TryGetPathConnectionRecordById(recordId, out PathConnectionRecord* record))
            return [];

        return new Span<UInt32>(&record->r_RegisteredUnitGlobalIds[0], 50);
    }

    [LuaApiExport("GetMoatWorkTaskIndexAtTile")]
    public int GetMoatWorkTaskIndexAtTile(int tileX, int tileY)
    {
        return TryGetPackedTileId(tileX, tileY, out int tileId) ? GetMoatWorkTaskIndexGrid()[tileId] : 0;
    }

    public int GetMoatWorkTaskSlotLimit()
    {
        Int32 slotLimit = _tileManagerView.MoatWorkTaskSlotLimit;
        return (UInt32)slotLimit <= GameTileManagerView.MoatWorkTaskSlotCapacity ? slotLimit : 0;
    }

    /// <summary>
    /// Returns the native active moat-work task count. 
    /// This is distinct from the exclusive slot high-water mark because freed slots may be reused below that mark.
    /// </summary>
    [LuaApiExport("GetMoatWorkTaskActiveCount")]
    public int GetMoatWorkTaskActiveCount()
    {
        Int32 activeCount = _tileManagerView.MoatWorkTaskActiveCount;
        return (UInt32)activeCount <= GameTileManagerView.MoatWorkTaskSlotCapacity ? activeCount : 0;
    }

    public bool TryGetMoatWorkTaskByIndex(int taskIndex, out NativePointer<MoatWorkTask> moatWorkTask)
    {
        moatWorkTask = new NativePointer<MoatWorkTask>((MoatWorkTask*)null);
        Int32 slotLimit = GetMoatWorkTaskSlotLimit();
        if (taskIndex <= 0 || taskIndex >= slotLimit)
            return false;

        MoatWorkTask* task = _tileManagerView.MoatWorkTaskSlotsPointer + taskIndex;
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

        Int32 taskIndex = GetMoatWorkTaskIndexGrid()[tileId];
        if (!TryGetMoatWorkTaskByIndex(taskIndex, out moatWorkTask))
            return false;

        return moatWorkTask.Pointer->r_TileId == tileId;
    }

    public bool TryGetPackedPathDirection(int unitId, int transitionIndex, out PackedPathDirection direction)
    {
        direction = default;
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) && pathPlan.TryGetDirection(transitionIndex, out direction);
    }

    /// <summary>
    /// Replaces one live direction nibble while preserving the other nibble in the same byte.
    /// The transition must already be within the unit's current path-plan length.
    /// </summary>
    public bool SetPackedPathDirection(int unitId, int transitionIndex, PackedPathDirection direction)
    {
        return TryGetUnitPathPlanView(unitId, out GameUnitPathPlanView pathPlan) && pathPlan.TrySetDirection(transitionIndex, direction);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsValidPackedTileId(int tileId)
    {
        return (UInt32)tileId < (UInt32)_tileManagerView.PathConnectionGrid.Length;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Boolean IsValidUnitType(eChimps unitType)
    {
        return (UInt32)unitType < PATHFINDING_UNIT_TYPE_COUNT;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Boolean IsValidPathConnectionClass(PathConnectionClass connectionClass)
    {
        return (UInt32)((Int32)connectionClass - FIRST_PATH_CONNECTION_CLASS) < PATH_CONNECTION_CLASS_COUNT;
    }

    private static Int32 GetSelectedUnitTypeCounterIndex(eChimps unitType)
    {
        return unitType switch
        {
            eChimps.CHIMP_TYPE_ARCHER => 0,
            eChimps.CHIMP_TYPE_SPEARMAN => 1,
            eChimps.CHIMP_TYPE_MACEMAN => 2,
            eChimps.CHIMP_TYPE_XBOWMAN => 3,
            eChimps.CHIMP_TYPE_PIKEMAN => 4,
            eChimps.CHIMP_TYPE_SWORDSMAN => 5,
            eChimps.CHIMP_TYPE_KNIGHT => 6,
            eChimps.CHIMP_TYPE_ENGINEER => 7,
            eChimps.CHIMP_TYPE_LADDERMAN => 8,
            eChimps.CHIMP_TYPE_TUNNELER => 9,
            eChimps.CHIMP_TYPE_MONK => 10,
            eChimps.CHIMP_TYPE_CATAPULT => 11,
            eChimps.CHIMP_TYPE_TREBUCHET => 12,
            eChimps.CHIMP_TYPE_BATTERING_RAM => 13,
            eChimps.CHIMP_TYPE_SIEGE_TOWER => 14,
            eChimps.CHIMP_TYPE_PORTABLE_SHIELD => 15,
            eChimps.CHIMP_TYPE_MANGONEL => 16,
            eChimps.CHIMP_TYPE_BALLISTA => 17,
            eChimps.CHIMP_TYPE_ARAB_BOW => 19,
            eChimps.CHIMP_TYPE_ARAB_SLAVE => 20,
            eChimps.CHIMP_TYPE_ARAB_SLINGER => 21,
            eChimps.CHIMP_TYPE_ARAB_ASSASIN => 22,
            eChimps.CHIMP_TYPE_ARAB_HORSEMAN => 23,
            eChimps.CHIMP_TYPE_ARAB_SWORDSMAN => 24,
            eChimps.CHIMP_TYPE_ARAB_GRENADIER => 25,
            eChimps.CHIMP_TYPE_ARAB_BALLISTA => 26,
            eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER => 27,
            eChimps.CHIMP_TYPE_BEDOUIN_HEALER => 28,
            eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH => 29,
            eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER => 30,
            eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER => 31,
            eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL => 32,
            eChimps.CHIMP_TYPE_BEDOUIN_SAPPER => 33,
            eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER => 34,
            _ => -1
        };
    }

    private bool TryGetPackedTileId(int tileX, int tileY, out int tileId)
    {
        tileId = 0;
        GameTileManagerAPI tileApi = GameTileManagerAPI.Instance;
        if (!tileApi.IsTileInsideMapBounds(tileX, tileY))
            return false;

        tileId = tileApi.GetTileId(tileX, tileY);
        return IsValidPackedTileId(tileId);
    }
}
