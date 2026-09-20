using RedBird.Core.Memory;
using SHCDESE.API.Components.Spatial;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Interop.Query;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with game vegetation such as trees and shrubs.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for creating, deleting,
/// and querying vegetation in the game world. It provides methods to access and modify
/// individual vegetation properties and includes a high-performance query system.
/// </remarks>
[LuaApiNamespace("Vegetation")]
public unsafe sealed class GameVegetationManagerAPI
{
    private static readonly Lazy<GameVegetationManagerAPI> _lazy = new(() => new GameVegetationManagerAPI());
    public static GameVegetationManagerAPI Instance => _lazy.Value;

    internal const int NUM_PREALLOC_VEGETATION = 4000;

    internal GameVegetationManager* _vegetationManager;
    internal SimpleNativeArray<GameVegetation> _vegArray;
    private SimpleNativeArray<UInt32> _treeGrowthStageTable;
    private SimpleNativeArray<UInt32> _treeAreaProximityLevelTable;

    /// <summary>
    /// Controls whether the ticker function for vegetation is disabled or not.
    /// </summary>
    public bool VegetationSimulationEnabled { get; set; } = true;

    /// <summary>
    /// Controls whether the growth function for vegetation is disabled or not.
    /// </summary>
    public bool VegetationGrowthEnabled { get; set; } = true;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameVegetationManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameVegetationManagerAPI()
    {
        _vegetationManager = (GameVegetationManager*)GameGlobalsManager.Instance.GameVegetationManagerVA;
        _vegArray = new SimpleNativeArray<GameVegetation>((byte*)&_vegetationManager->VegetationArray, NUM_PREALLOC_VEGETATION);

        _treeGrowthStageTable = new SimpleNativeArray<UInt32>((byte*)(GameGlobalsManager.Instance.TreeGrowthProgressionTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), Enum.GetValues(typeof(TreeGrowthStage)).Length * 4);
        _treeAreaProximityLevelTable = new SimpleNativeArray<UInt32>((byte*)(GameGlobalsManager.Instance.TreeProximityAreaLevelTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), 10);

        LogHelper.Information($"_vegetationManager: {new IntPtr(_vegetationManager).ToString("X16")}");
        LogHelper.Information($"_vegArray: {new IntPtr(_vegArray._array).ToString("X16")}");
        LogHelper.Information($"_treeGrowthStageTable: {new IntPtr(_treeGrowthStageTable._array).ToString("X16")}");
        LogHelper.Information($"_treeAreaProximityLevelTable: {new IntPtr(_treeAreaProximityLevelTable._array).ToString("X16")}");
    }

    /// <summary>
    /// Gets a native pointer to the current game vegetation manager instance.
    /// </summary>
    /// <returns>A <see cref="NativePointer{GameVegetationManager}"/> representing the game vegetation manager.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativePointer<GameVegetationManager> GetVegetationManager()
    {
        return _vegetationManager;
    }

    /// <summary>
    /// Returns the underlying array of game vegetation managed by this instance.
    /// </summary>
    /// <returns>A <see cref="SimpleNativeArray{GameVegetation}"/> containing all vegetation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GameVegetation> GetVegetationArray()
    {
        return _vegArray;
    }

    /// <summary>
    /// Returns a span representing the current collection of vegetation managed by the instance.
    /// </summary>
    /// <returns>A <see cref="Span{GameVegetation}"/> containing the vegetation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GameVegetation> GetVegetationAsSpan()
    {
        return _vegArray.AsSpan();
    }

    /// <summary>
    /// Attempts to retrieve a direct, raw pointer to a vegetation object by its ID.
    /// </summary>
    /// <param name="vegetationId">The unique identifier of the vegetation.</param>
    /// <param name="vegetation">When this method returns, contains a pointer to the vegetation object if found; otherwise, null.</param>
    /// <returns><c>true</c> if the vegetation was found and is within the valid array bounds; otherwise, <c>false</c>.</returns>
    public bool TryGetVegetationById(int vegetationId, out GameVegetation* vegetation)
    {
        vegetation = null;
        if (vegetationId <= 0 || vegetationId > _vegArray.Length)
        {
            LogHelper.Error($"Tried to access vegetation index that was out of range: [{vegetationId}/{_vegArray.Length}]");
            return false;
        }

        if (_vegArray._array == null)
            return false;

        vegetation = &_vegArray._array[vegetationId - 1];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a vegetation object by its ID.
    /// </summary>
    /// <param name="vegetationId">The unique identifier of the vegetation.</param>
    /// <param name="vegetation">When this method returns, contains a <see cref="NativePointer{GameVegetation}"/> wrapping the vegetation object if found; otherwise, an invalid pointer.</param>
    /// <returns><c>true</c> if the vegetation was found; otherwise, <c>false</c>.</returns>
    public bool TryGetVegetationByIdEx(int vegetationId, out NativePointer<GameVegetation> vegetation)
    {
        bool result = TryGetVegetationById(vegetationId, out GameVegetation* vegPtr);
        vegetation = new NativePointer<GameVegetation>(vegPtr);
        return result;
    }

    /// <summary>
    /// Creates a vegetation object at a specific tile location, typically used by the map editor.
    /// This function is the same on the Map Editor uses.
    /// </summary>
    /// <param name="tileX">The X-coordinate of the tile to place the vegetation on.</param>
    /// <param name="tileY">The Y-coordinate of the tile to place the vegetation on.</param>
    /// <param name="eMappers">The type of vegetation to create.</param>
    /// <returns>Unknown.</returns>
    [LuaApiExport("Create")]
    public Int64 Create(UInt16 tileX, UInt16 tileY, eMappers eMappers)
    {
        return BulkMapEditorDetours.c_game_editor_place_vegetation_hook_impl(GameTileManagerAPI.Instance.GetTileManager(), tileX, tileY, eMappers);
    }

    /// <summary>
    /// Creates a vegetation object at a specific tile location.
    /// This is a custom implementation that supports more advanced parameters and return id.
    /// </summary>
    /// <param name="tileX">The X-coordinate of the tile to place the vegetation on.</param>
    /// <param name="tileY">The Y-coordinate of the tile to place the vegetation on.</param>
    /// <param name="vegetationType">The type of vegetation to create.</param>
    /// <param name="treeProximityAreaLevel">Unknown, leave at -1 for auto-assign.</param>
    /// <param name="a6">Unknown.</param>
    /// <param name="a7">Unknown.</param>
    /// <param name="growthStage">The starting growth stage.</param>
    /// <returns><c>VegetationID</c> if the vegetation was created; otherwise, <c>0</c>. (0-based)</returns>
    [LuaApiExport("CreateEx")]
    public Int64 CreateEx(UInt16 tileX, UInt16 tileY, VegetationType vegetationType, Int16 treeProximityAreaLevel = -1, int a6 = 0, Int16 a7 = 0, int growthStage = 3)
    {
        if (treeProximityAreaLevel == -1)
        {
            treeProximityAreaLevel = (Int16)BulkVegetationDetours.c_game_get_tree_proximity_area_level_from_type!(_vegetationManager, vegetationType, growthStage);
        }

        Int64 vegetationId = BulkVegetationDetours.c_game_spawn_vegetation_hook_impl(_vegetationManager, tileX, tileY, vegetationType, treeProximityAreaLevel, a6, a7, growthStage);
        if (!TryGetVegetationById((int)vegetationId, out GameVegetation* veg))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return -1;
        }
        int tileId = GameTileManagerAPI.Instance.GetTileId(tileX, tileY);

        if (GameTileManagerAPI.Instance.HasTilePropertyFlag(tileId, TilePropertyFlag.Moat | TilePropertyFlag.MoatPlanned))
        {
            GameTileManagerAPI.Instance.RemoveTilePropertyFlag(tileId, TilePropertyFlag.Moat);
            GameTileManagerAPI.Instance.RemoveTilePropertyFlag(tileId, TilePropertyFlag.MoatPlanned);
        }

        GameTileManagerAPI.Instance.RemoveTilePropertyFlag(tileId, TilePropertyFlag.ImpassableEdge);
        GameTileManagerAPI.Instance.AddTilePropertyFlag(tileId, TilePropertyFlag.IsTree);

        GameTileManagerAPI.Instance.SetTileVegetationId(tileId, (int)vegetationId);
        BulkVegetationDetours.c_game_create_tree_proximity_area!(GameTileManagerAPI.Instance.GetTileManager(), (int)vegetationId, 0);
        BulkTileDetours.c_game_update_pathfinding_for_tile_and_neighbors3x3_wrapper!(GameTileManagerAPI.Instance.GetPathfindingContext(), tileY, tileId);
        return vegetationId;
    }

    /// <summary>
    /// Safely marks a vegetation object for deletion by the game engine.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation to delete.</param>
    /// <returns><c>true</c> if the vegetation was found and marked for deletion; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is the recommended way to delete vegetation. It changes its state, allowing the
    /// game engine to clean it up gracefully on a subsequent frame.
    /// </remarks>
    [LuaApiExport("DeleteSafe")]
    public bool DeleteSafe(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return false;
        }
        vegetation->r_AliveState = AliveState.MarkedForDeletion;

        return true;
    }

    /// <summary>
    /// Immediately deletes a vegetation object from the game.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation to delete.</param>
    /// <remarks>
    /// Use with caution, as immediate deletion could cause issues if other game systems are
    /// still referencing the object. Prefer DeleteVegetationSafe where possible.
    /// </remarks>
    [LuaApiExport("Delete")]
    public void Delete(int vegetationId)
    {
        BulkVegetationDetours.c_game_vegetation_delete_hook_impl(_vegetationManager, vegetationId);
    }

    //
    // Common Functions
    //

    /// <summary>
    /// Gets the current health of a vegetation object.
    /// Only used by trees (I think?) until it falls down, at which point it is ready to harvest by a lumberjack.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <returns>The health value, or 0 if the vegetation is not found.</returns>
    [LuaApiExport("GetCurrentHealth")]
    public UInt16 GetCurrentHealth(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Warning($"Could not find vegetation by id: {vegetationId}");
            return 0;
        }
        return vegetation->r_Health;
    }

    /// <summary>
    /// Sets the current health of a vegetation object.
    /// Only used by trees (I think?) until it falls down, at which point it is ready to harvest by a lumberjack.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <param name="health">The new health value to set.</param>
    [LuaApiExport("SetCurrentHealth")]
    public void SetCurrentHealth(int vegetationId, UInt16 health)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return;
        }
        vegetation->r_Health = health;
    }

    /// <summary>
    /// Gets the resource state of a vegetation object (e.g., amount of wood remaining).
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <returns>The resource state value, or 0 if the vegetation is not found.</returns>
    [LuaApiExport("GetResourceState")]
    public Int16 GetResourceState(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return 0;
        }
        return vegetation->r_ResourceState;
    }

    /// <summary>
    /// Sets the resource state of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <param name="resourceState">The new resource state value to set.</param>
    [LuaApiExport("SetResourceState")]
    public void SetResourceState(int vegetationId, Int16 resourceState)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return;
        }
        vegetation->r_ResourceState = resourceState;
    }

    /// <summary>
    /// Gets the type of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <returns>The <see cref="VegetationType"/>, or <see cref="VegetationType.None"/> if not found.</returns>
    [LuaApiExport("GetType")]
    public VegetationType GetType(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return VegetationType.None;
        }
        return vegetation->r_VegetationType;
    }

    /// <summary>
    /// Sets the type of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <param name="vegetationType">The new <see cref="VegetationType"/> to set.</param>
    [LuaApiExport("SetType")]
    public void SetType(int vegetationId, VegetationType vegetationType)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return;
        }
        vegetation->r_VegetationType = vegetationType;
    }

    /// <summary>
    /// Gets the growth stage of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <returns>The growth value, or 0 if not found.</returns>
    [LuaApiExport("GetGrowthStage")]
    public UInt32 GetGrowthStage(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return 0;
        }
        return vegetation->r_GrowthStage;
    }

    /// <summary>
    /// Sets the growth stage of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <param name="growthStage">The new growth value to set.</param>
    [LuaApiExport("SetGrowthStage")]
    public void SetGrowthStage(int vegetationId, UInt32 growthStage)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return;
        }
        vegetation->r_GrowthStage = growthStage;
    }

    /// <summary>
    /// Gets the growth progress of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <returns>The growth progress, or 0 if not found.</returns>
    [LuaApiExport("GetGrowthProgress")]
    public UInt32 GetGrowthProgress(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return 0;
        }
        return vegetation->r_GrowthProgress;
    }

    /// <summary>
    /// Sets the growth progress of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <param name="progress">The new growth progress to set.</param>
    [LuaApiExport("SetGrowthProgress")]
    public void SetGrowthProgress(int vegetationId, UInt32 progress)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return;
        }
        vegetation->r_GrowthProgress = progress;
    }

    /// <summary>
    /// Gets the tile position of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> representing the tile coordinates, or a default vector if not found.</returns>
    [LuaApiExport("GetPosition")]
    public UnmanagedVector2<UInt16> GetLocalTilePosition(int vegetationId)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return default;
        }
        return *(UnmanagedVector2<UInt16>*)(&vegetation->r_TilePositionX);
    }

    /// <summary>
    /// Sets the tile position of a vegetation object.
    /// </summary>
    /// <param name="vegetationId">The ID of the vegetation.</param>
    /// <param name="position">The new tile coordinates to set.</param>
    [LuaApiExport("SetPosition")]
    public void SetLocalTilePosition(int vegetationId, UnmanagedVector2<UInt16> position)
    {
        if (!TryGetVegetationById(vegetationId, out GameVegetation* vegetation))
        {
            LogHelper.Error($"Could not find vegetation by id: {vegetationId}");
            return;
        }
        *(UnmanagedVector2<UInt16>*)(&vegetation->r_TilePositionX) = position;
    }

    //
    // Tree Growth Stage Duration
    //

    /// <summary>
    /// Gets the duration (conditional in game ticks) a tree spends in a specific growth stage.
    /// </summary>
    /// <param name="vegetation">The type of tree.</param>
    /// <param name="stage">The growth stage to query.</param>
    /// <returns>The duration in game ticks.</returns>
    [LuaApiExport("GetTreeGrowthStageDuration")]
    public UInt32 GetTreeGrowthStageDuration(VegetationType vegetation, TreeGrowthStage stage)
    {
        int index = (int)vegetation * 7 + (int)stage;
        return _treeGrowthStageTable[index];
    }

    /// <summary>
    /// Sets the duration (conditional in game ticks) a tree spends in a specific growth stage.
    /// </summary>
    /// <param name="vegetation">The type of tree.</param>
    /// <param name="stage">The growth stage to modify.</param>
    /// <param name="duration">The new duration in game ticks.</param>
    [LuaApiExport("SetTreeGrowthStageDuration")]
    public void SetTreeGrowthStageDuration(VegetationType vegetation, TreeGrowthStage stage, UInt32 duration)
    {
        int index = (int)vegetation * 7 + (int)stage;
        _treeGrowthStageTable[index] = duration;
    }

    /// <summary>
    /// Whether simulation for vegetation is enabled
    /// </summary>
    /// <returns>Returns true if simulation for vegetation is enabled; Otherwise false.</returns>
    [LuaApiExport("IsSimulationEnabled")]
    public bool IsSimulationEnabled()
    {
        return VegetationSimulationEnabled;
    }

    /// <summary>
    /// Controls the simulation for vegetation
    /// </summary>
    /// <param name="enabled">Simulation state</param>
    [LuaApiExport("SetSimulationEnabled")]
    public void SetSimulationEnabled(bool enabled)
    {
        VegetationSimulationEnabled = enabled;
    }

    /// <summary>
    /// Whether growth for vegetation is enabled
    /// </summary>
    /// <returns>Returns true if growth for vegetation is enabled; Otherwise false.</returns>
    [LuaApiExport("IsGrowthEnabled")]
    public bool IsGrowthEnabled()
    {
        return VegetationGrowthEnabled;
    }

    /// <summary>
    /// Controls the growth for vegetation
    /// </summary>
    /// <param name="enabled">Simulation state</param>
    [LuaApiExport("SetGrowthEnabled")]
    public void SetGrowthEnabled(bool enabled)
    {
        VegetationGrowthEnabled = enabled;
    }

    #region Query System

    /// <summary>
    /// Begins a high-performance query over all possible vegetation slots.
    /// </summary>
    /// <returns>A <see cref="GameStructQuery{GameVegetation}"/> instance to build upon.</returns>
    public GameStructQuery<GameVegetation> QueryVegetation()
    {
        return new GameStructQuery<GameVegetation>(_vegArray._array, _vegArray.Length);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    public void ExecuteQuery(List<int> results, RefPredicate<GameVegetation> basePredicate, AliveState? stateFilter, VegetationType? vegType)
    {
        GameStructQuery<GameVegetation> query = QueryVegetation().Where(basePredicate);

        if (stateFilter.HasValue)
        {
            AliveState state = stateFilter.Value;
            query = query.Where((in GameVegetation u) => u.r_AliveState == state);
        }

        if (vegType.HasValue)
        {
            VegetationType type = vegType.Value;
            query = query.Where((in GameVegetation u) => u.r_VegetationType == type);
        }

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for convenience.
    /// </summary>
    public static class VegetationPredicates
    {
        /// <summary>A predicate that matches vegetation that is currently active.</summary>
        public static readonly RefPredicate<GameVegetation> IsAlive = static (in veg) => veg.r_AliveState == AliveState.IsAlive;

        /// <summary>A predicate that matches vegetation that has been marked for deletion.</summary>
        public static readonly RefPredicate<GameVegetation> IsDead = static (in veg) => veg.r_AliveState == AliveState.MarkedForDeletion;

        /// <summary>A predicate that matches any vegetation, used as a default for generic queries.</summary>
        public static readonly RefPredicate<GameVegetation> Any = static (in unit) => true;

        /// <summary>Creates a predicate that matches vegetation of a specific type.</summary>
        public static RefPredicate<GameVegetation> IsOfType(VegetationType vegType) => (in veg) => veg.r_VegetationType == vegType;

        /// <summary>
        /// Matches vegetation whose tile falls within the specified rectangle.
        /// Reads <see cref="GameVegetation.r_TilePositionX"/> and
        /// <see cref="GameVegetation.r_TilePositionY"/> directly via the <c>in</c>
        /// parameter instead of calling <c>CurrentTilePosition()</c>, which uses a
        /// <c>fixed</c> statement that pins a stack copy rather than the native array
        /// element when the struct is on the unmanaged heap.
        /// </summary>
        public static RefPredicate<GameVegetation> IsWithinRect(int x, int y, int width, int height) =>
            (in GameVegetation v) =>
                v.r_TilePositionX >= x && v.r_TilePositionX < x + width &&
                v.r_TilePositionY >= y && v.r_TilePositionY < y + height;

        /// <summary>
        /// Matches vegetation whose tile falls within the specified sphere (circle).
        /// Uses a pre-computed squared radius to avoid <c>Math.Sqrt</c> per entry.
        /// Same rationale as <see cref="IsWithinRect"/> for avoiding <c>CurrentTilePosition()</c>.
        /// </summary>
        public static RefPredicate<GameVegetation> IsWithinSphere(int cx, int cy, int radius)
        {
            int rSq = radius * radius;
            return (in GameVegetation v) =>
            {
                int dx = v.r_TilePositionX - cx;
                int dy = v.r_TilePositionY - cy;
                return dx * dx + dy * dy <= rSq;
            };
        }
    }

    /// <summary>
    /// Fills a list with IDs of all vegetation, with optional filters (no spatial constraint).
    /// </summary>
    /// <param name="results">The list to be cleared and filled with vegetation IDs.</param>
    /// <param name="stateFilter">Optional: Filters for vegetation in a specific <see cref="AliveState"/>.</param>
    /// <param name="vegType">Optional: Filters for vegetation of a specific <see cref="VegetationType"/>.</param>
    public void GetAllVegetation(List<int> results, AliveState? stateFilter = null, VegetationType? vegType = null)
    {
        ExecuteQuery(results, VegetationPredicates.Any, stateFilter, vegType);
    }

    #endregion
}
