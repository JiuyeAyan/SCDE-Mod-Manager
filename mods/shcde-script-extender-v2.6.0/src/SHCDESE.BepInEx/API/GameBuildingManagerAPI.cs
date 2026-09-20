using R3;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Managed;
using SHCDESE.API.LowLevel;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Extensions;
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
using System.Runtime.InteropServices;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with game buildings.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for querying and modifying
/// buildings in the game world. It provides direct access to building properties, cost tables,
/// and includes a high-performance query system for spatial and property-based searches.
/// </remarks>
[LuaApiNamespace("Building")]
public unsafe sealed class GameBuildingManagerAPI
{
    private static readonly Lazy<GameBuildingManagerAPI> _lazy = new(() => new GameBuildingManagerAPI());
    public static GameBuildingManagerAPI Instance => _lazy.Value;

    /// <summary>The maximum number of buildings the game pre-allocates memory for.</summary>
    internal const int NUM_PREALLOC_BUILDINGS = 4000;

    private GameBuildingManager* _buildingManager;
    private SimpleNativeArray<GameBuilding> _buildingArray;

    // --- Managed Stats Arrays ---

    internal ManagedNativeArray<uint> _buildingHealthDefaultsArray;
    internal ManagedNativeArray<UInt16> _housingPopulationSpaceDefaultsArray;

    // Default costs
    private IntPtr _buildingDefaultCostsArray;
    private IntPtr _buildingDefaultCostsArrayEnd;
    private int _buildingDefaultCostsCount;
    // begin:00000001802CD314
    // end..:00000001802CDBAB
    // size: 0x897 - 2199

    // --- Managed Active Costs ---
    private SimpleNativeArray<Int32> _buildingStoneCostsArray;
    private SimpleNativeArray<Int32> _buildingRawPitchCostsArray;
    private SimpleNativeArray<Int32> _buildingGoldCostsArray;
    private SimpleNativeArray<Int32> _buildingWoodCostsArray;
    private SimpleNativeArray<Int32> _buildingIronIngotsCostsArray;

    // --- Managed Dictionary ---

    /// <summary>
    /// Table that contains all non-standard fire damage handling for buildings.
    /// All buildings by default receive 1 fire damage if they are vulnerable (defined seperately).
    /// </summary>
    private ManagedDictionary<eStructs, Int16> _buildingFireDamageTable;

    // low / high wall cost control
    private float _highWallCostMultiplier = 0.5f;
    private float _lowWallCostMultiplier = 0.25f;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int GetWallCostMultiplierDelegate();

    internal static int GetLowWallCostMultiplierInternal()
    {
        return (int)(Instance._lowWallCostMultiplier * 4);
    }

    internal static int GetHighWallCostMultiplierInternal()
    {
        return (int)(Instance._highWallCostMultiplier * 4);
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate Int16 GetBuildingFireDamageDelegate(eStructs building);

    /// <summary>
    /// The default damage value of killing pits
    /// </summary>
    internal const int DEFAULT_KILLINGPIT_DAMAGE = 18000;

    /// <summary>
    /// The default refund multiplier
    /// </summary>
    internal const float DEFAULT_BUILDING_REFUND_MULTIPLIER = 0.5f;

    /// <summary>
    /// Configured killing pit damage (all units)
    /// </summary>
    [LuaApiExport("KillingPitDamage")]
    public ManagedValue<int> KillingPitDamage { get; }

    /// <summary>
    /// Building refund multiplier for: Wood
    /// </summary>
    [LuaApiExport("WoodRefundMultiplier")]
    public ManagedValue<float> WoodRefundMultiplier { get; }

    /// <summary>
    /// Building refund multiplier for: Stone
    /// </summary>
    [LuaApiExport("StoneRefundMultiplier")]
    public ManagedValue<float> StoneRefundMultiplier { get; }

    /// <summary>
    /// Building refund multiplier for: Iron
    /// </summary>
    [LuaApiExport("IronRefundMultiplier")]
    public ManagedValue<float> IronRefundMultiplier { get; }

    /// <summary>
    /// Building refund multiplier for: Pitch
    /// </summary>
    [LuaApiExport("PitchRefundMultiplier")]
    public ManagedValue<float> PitchRefundMultiplier { get; }

    /// <summary>
    /// Building refund multiplier for: Gold
    /// </summary>
    [LuaApiExport("GoldRefundMultiplier")]
    public ManagedValue<float> GoldRefundMultiplier { get; }

    /// <summary>
    /// Table mapping map sizes to their allowed building radius from the Keep.
    /// </summary>
    private ManagedDictionary<int, int> _keepProximityTable;

    /// <summary>
    /// If set to a value greater than 0, this will be used as the buildable radius 
    /// regardless of map size.
    /// </summary>
    [LuaApiExport("KeepProximityOverride")]
    public ManagedValue<int> KeepProximityOverride { get; }

    private int _initialized = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameBuildingManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameBuildingManagerAPI()
    {
        int amountStructs = Enum.GetValues(typeof(eStructs)).Length;

        _buildingManager = (GameBuildingManager*)GameGlobalsManager.Instance.GameBuildingManagerVA;
        _buildingArray = new SimpleNativeArray<GameBuilding>((byte*)&_buildingManager->BuildingsArray, NUM_PREALLOC_BUILDINGS);

        // Health, Population
        _buildingHealthDefaultsArray = new ManagedNativeArray<uint>((byte*)(GameGlobalsManager.Instance.BuildingHealthTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), amountStructs);
        _housingPopulationSpaceDefaultsArray = new ManagedNativeArray<UInt16>((byte*)(GameGlobalsManager.Instance.BuildingHousingPopulatonSpaceTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), amountStructs);

        // Default Costs
        _buildingDefaultCostsArray = (IntPtr)(GameGlobalsManager.Instance.BuildingDefaultCostsTableVA);
        _buildingDefaultCostsArrayEnd = (IntPtr)(GameGlobalsManager.Instance.BuildingDefaultCostsTableEndVA);
        _buildingDefaultCostsCount = (int)((_buildingDefaultCostsArrayEnd.ToInt64() - _buildingDefaultCostsArray.ToInt64()) / sizeof(BuildingCost));

        // Active Costs
        int costArrayLen = amountStructs * sizeof(BuildingCost);
        _buildingStoneCostsArray = new SimpleNativeArray<int>((byte*)(GameGlobalsManager.Instance.BuildingStoneCostsTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), costArrayLen);
        _buildingRawPitchCostsArray = new SimpleNativeArray<int>((byte*)(GameGlobalsManager.Instance.BuildingRawPitchCostsTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), costArrayLen);
        _buildingGoldCostsArray = new SimpleNativeArray<int>((byte*)(GameGlobalsManager.Instance.BuildingGoldCostsTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), costArrayLen);
        _buildingWoodCostsArray = new SimpleNativeArray<int>((byte*)(GameGlobalsManager.Instance.BuildingWoodCostsTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), costArrayLen);
        _buildingIronIngotsCostsArray = new SimpleNativeArray<int>((byte*)(GameGlobalsManager.Instance.BuildingIronIngotsCostsTableRVA + (ulong)CrusaderLibrary.Instance.LibraryModuleHandle), costArrayLen);

        Dictionary<eStructs, Int16> initialFireDamage = new Dictionary<eStructs, Int16>()
        {
            [eStructs.STRUCT_HOVEL] = 4,
            [eStructs.STRUCT_WOODCUTTERS_HUT] = 4,
            [eStructs.STRUCT_HUNTERS_HUT] = 4,
            [eStructs.STRUCT_WHEATFARM] = 4,
            [eStructs.STRUCT_HOPSFARM] = 4,
            [eStructs.STRUCT_APPLEFARM] = 4,
            [eStructs.STRUCT_CATTLEFARM] = 4
        };
        _buildingFireDamageTable = new ManagedDictionary<eStructs, Int16>(initialFireDamage, 1);

        KillingPitDamage = new ManagedValue<int>(DEFAULT_KILLINGPIT_DAMAGE);

        WoodRefundMultiplier = new ManagedValue<float>(DEFAULT_BUILDING_REFUND_MULTIPLIER);
        StoneRefundMultiplier = new ManagedValue<float>(DEFAULT_BUILDING_REFUND_MULTIPLIER);
        IronRefundMultiplier = new ManagedValue<float>(DEFAULT_BUILDING_REFUND_MULTIPLIER);
        PitchRefundMultiplier = new ManagedValue<float>(DEFAULT_BUILDING_REFUND_MULTIPLIER);
        GoldRefundMultiplier = new ManagedValue<float>(DEFAULT_BUILDING_REFUND_MULTIPLIER);

        Dictionary<int, int> initialProximity = new()
        {
            { 160, 45 },
            { 200, 50 },
            { 300, 60 },
            { 400, 70 },
            { 500, 80 },
            { 600, 90 },
            { 700, 100 },
            { 800, 100 }
        };

        // Default fallback is 70 if a map size isnt found
        _keepProximityTable = new ManagedDictionary<int, int>(initialProximity, 70);

        // Initialize the override to -1 (disabled)
        KeepProximityOverride = new ManagedValue<int>(-1);

        LogHelper.Information($"_buildingManager: {new IntPtr(_buildingManager).ToString("X16")}");
        LogHelper.Information($"_buildingArray: {new IntPtr(_buildingArray._array).ToString("X16")}");
        LogHelper.Information($"_buildingDefaultCostsArray: {_buildingDefaultCostsArray.ToString("X16")}");
        LogHelper.Information($"_buildingDefaultCostsArrayEnd: {_buildingDefaultCostsArrayEnd.ToString("X16")}");
        LogHelper.Information($"_buildingStoneCostsArray: {new IntPtr(_buildingStoneCostsArray._array).ToString("X16")}");
        LogHelper.Information($"_buildingRawPitchCostsArray: {new IntPtr(_buildingRawPitchCostsArray._array).ToString("X16")}");
        LogHelper.Information($"_buildingGoldCostsArray: {new IntPtr(_buildingGoldCostsArray._array).ToString("X16")}");
        LogHelper.Information($"_buildingWoodCostsArray: {new IntPtr(_buildingWoodCostsArray._array).ToString("X16")}");
        LogHelper.Information($"_buildingIronIngotsCostsArray: {new IntPtr(_buildingIronIngotsCostsArray._array).ToString("X16")}");

    }
    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    /// <summary>
    /// Event handler called when a map is unloaded.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs e)
    {
        LogHelper.Information($"Unloading");

        Instance._buildingHealthDefaultsArray.ClearOverrides();
        Instance._housingPopulationSpaceDefaultsArray.ClearOverrides();

        Instance._buildingFireDamageTable.ClearOverrides();

        Instance._keepProximityTable.ClearOverrides();
        Instance.KeepProximityOverride.ClearOverrides();
    }

    /// <summary>
    /// Gets a native pointer to the current game building manager instance.
    /// </summary>
    /// <returns>A <see cref="NativePointer{GameBuildingManager}"/> representing the game building manager.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativePointer<GameBuildingManager> GetBuildingManager()
    {
        return _buildingManager;
    }

    /// <summary>
    /// Returns the underlying array of game buildings managed by this instance.
    /// </summary>
    /// <returns>A <see cref="SimpleNativeArray{GameBuilding}"/> containing all buildings. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GameBuilding> GetBuildingsArray()
    {
        return _buildingArray;
    }

    /// <summary>
    /// Returns a span representing the current collection of buildings managed by the instance.
    /// </summary>
    /// <remarks>
    /// The returned span provides direct access to the underlying building data without allocating
    /// additional memory. Modifications to the span affect the original collection.
    /// </remarks>
    /// <returns>A <see cref="Span{GameBuilding}"/> containing the buildings.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GameBuilding> GetBuildingsAsSpan()
    {
        return _buildingArray.AsSpan();
    }

    /// <summary>
    /// Attempts to retrieve a direct, raw pointer to a building by its ID.
    /// </summary>
    /// <param name="buildingId">The unique identifier of the building.</param>
    /// <param name="building">When this method returns, contains a pointer to the building if found; otherwise, null.</param>
    /// <returns><c>true</c> if the building was found and is within the valid array bounds; otherwise, <c>false</c>.</returns>
    public bool TryGetBuildingById(int buildingId, out GameBuilding* building)
    {
        building = null;
        if (!IsValidId(buildingId))
        {
            LogHelper.Error($"Tried to access building index that was out of range: [{buildingId}/{_buildingArray.Length}]");
            return false;
        }

        if (_buildingArray._array == null)
            return false;

        building = &_buildingArray._array[buildingId - 1];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a building object by its ID.
    /// </summary>
    /// <param name="buildingId">The unique identifier of the building.</param>
    /// <param name="building">When this method returns, contains a <see cref="NativePointer{GameBuilding}"/> wrapping the building object if found; otherwise, an invalid pointer.</param>
    /// <returns><c>true</c> if the building was found; otherwise, <c>false</c>.</returns>
    public bool TryGetBuildingByIdEx(int buildingId, out NativePointer<GameBuilding> building)
    {
        bool result = TryGetBuildingById(buildingId, out GameBuilding* buildingPtr);
        building = new NativePointer<GameBuilding>(buildingPtr);
        return result;
    }

    /// <summary>
    /// Returns the building id (context-dependent)
    /// </summary>
    [LuaApiExport("GetCurrentContextBuildingId")]
    public UInt16 GetCurrentContextBuildingId()
    {
        UInt64 addr = GameGlobalsManager.Instance.CurrentContextBuildingValueVA;
        if (addr == 0)
        {
            LogHelper.Debug($"Current Context: BuildingValueVA is null!");
            return 0;
        }

        return *(UInt16*)addr;
    }

    /// <summary>
    /// Gets the count of all active, empty resource buildings of a specific type owned by a player.
    /// </summary>
    /// <param name="playerId">The ID of the player to check.</param>
    /// <param name="building">The type of building to count.</param>
    /// <returns>The number of matching buildings.</returns>
    /// <remarks>This method is a 1:1 replacement for the game's internal `c_game_player_get_alive_empty_resource_buildings_of_type` function.</remarks>
    [LuaApiExport("GetEmptyResourceCount")]
    public int GetAliveEmptyResourceBuildingsOfTypeByPlayer(int playerId, eStructs building)
    {
        List<int> results = new List<int>();
        ExecuteQuery(results, static (in building) => building.r_CurrentGoodStackAmount <= 0, AliveState.IsAlive, null);
        return results.Count;
    }

    /// <summary>
    /// Internal way to retrieve the damage of fire to buildings
    /// </summary>
    /// <param name="building">The building</param>
    /// <returns>Damage</returns>
    internal static Int16 GetBuildingFireDamageInternal(eStructs building)
    {
        LogHelper.Verbose($"Building: {building}");
        return Instance._buildingFireDamageTable.GetValue(building);
    }

    /// <summary>
    /// Internal way to set the damage of fire to buildings
    /// </summary>
    /// <param name="building">The building</param>
    /// <param name="damage">Damage</param>
    internal static void SetBuildingFireDamageInternal(eStructs building, Int16 damage)
    {
        Instance._buildingFireDamageTable.SetValue(building, damage);
    }

    /// <summary>
    /// Internal way to set the damage of fire to buildings
    /// Push-based.
    /// </summary>
    /// <param name="building">The building</param>
    /// <param name="damage">Damage</param>
    internal static void PushBuildingFireDamageInternal(eStructs building, Int16 damage)
    {
        Instance._buildingFireDamageTable.Push(building, damage);
    }

    /// <summary>
    /// Internal way to set the damage of fire to buildings
    /// Push-based.
    /// </summary>
    /// <param name="building">The building</param>
    /// <returns>Damage</returns>
    internal static Int16 PopBuildingFireDamageInternal(eStructs building)
    {
        return Instance._buildingFireDamageTable.Pop(building);
    }

    /// <summary>
    /// Spawns a building structure in a raw manner.
    /// NOTE: Do always prefer <see cref="CreatePrefab"/>, as this function is experimental and may not work in all circumstances.
    /// </summary>
    /// <param name="playerId">The ID of the player who will own the building.</param>
    /// <param name="tileX">The top-left tile X-coordinate for the building's placement.</param>
    /// <param name="tileY">The top-left tile Y-coordinate for the building's placement.</param>
    /// <param name="heightElevation">The height elevation for the building.</param>
    /// <param name="building">The specific type of building (<see cref="eStructs"/>) to create.</param>
    /// <param name="buildingScale">The size scale parameter passed to the game's internal function. A woodcutter would be of size 3 (3x3)</param>
    /// <param name="visualPlayerid">The player ID that determines the building's color scheme.</param>
    /// <param name="spriteVariationIndex">The index for the building's sprite variation.</param>
    /// <returns>The unique ID of the newly created building object.</returns>
    /// <remarks>
    /// This is a low-level function that spawns only the core building object. It performs no placement checks,
    /// does not deduct resources, and does not create associated sub-components (like farm plots or stockpile tiles).
    /// For a complete, player-like building placement, use <see cref="CreatePrefab"/>.
    /// </remarks>
    /// <seealso cref="CreatePrefab"/>
    [LuaApiExport("Create")]
    public Int64 Create(int playerId, int tileX, int tileY, Int16 heightElevation, eStructs building, int buildingScale, int visualPlayerid, int spriteVariationIndex)
    {
        return BulkBuildingDetours.c_game_building_spawn_hook_impl(_buildingManager, playerId, tileX, tileY, heightElevation, building, buildingScale, visualPlayerid, spriteVariationIndex);
    }

    /// <summary>
    /// Creates a complete building as if placed by a player, including all associated components and resource costs.
    /// </summary>
    /// <param name="playerId">The ID of the player placing the building.</param>
    /// <param name="tileX">The top-left tile X-coordinate for the building's placement.</param>
    /// <param name="tileY">The top-left tile Y-coordinate for the building's placement.</param>
    /// <param name="mv">The building type from the editor's object palette enumeration (<see cref="eMappers"/>).</param>
    /// <param name="buildingScale">The size scale parameter passed to the game's internal function. A woodcutter would be of size 3 (3x3)</param>
    /// <param name="a7">An unknown integer parameter passed to the game's internal function.</param>
    /// <param name="bIsFree">If set to <c>true</c>, the player will not be charged the resource cost for the building.</param>
    /// <param name="bypassPlacementRules">If set to <c>true</c>, no placement validation will be applied for the attempt.</param>
    /// <returns>A value indicating the result of the placement attempt. A non-zero value typically indicates success.</returns>
    /// <remarks>
    /// This is the high-level, recommended function for creating buildings. It correctly handles resource deduction,
    /// placement validation, and the creation of all required sub-structures (e.g., farm plots).
    /// If you require the id of a object or a group of objects created by this function, you will need to set-up a listener hook for the building spawn event.
    /// <see cref="EventAPI.Buildings.BuildingSpawnEventArgs"/>
    /// </remarks>
    /// <seealso cref="Create"/>
    [LuaApiExport("CreatePrefab")]
    public Int64 CreatePrefab(int playerId, int tileX, int tileY, eMappers mv, int buildingScale, int a7, bool bIsFree, bool bypassPlacementRules = false)
    {
        LogHelper.Debug($"Creating Prefab [{mv}] for player={playerId} at tile {tileX}, {tileY}, with scale {buildingScale}, is free: {bIsFree}, a7={a7}, bypassPlacementRules={bypassPlacementRules}");
        if (bypassPlacementRules)
        {
            GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride = true;
            GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue = false;
        }
        Int64 result = BulkBuildingDetours.c_game_player_build_structure_hook_impl((IntPtr)GameGlobalsManager.Instance.GameTileManagerVA, playerId, tileX, tileY, mv, buildingScale, a7, bIsFree == true ? (byte)1 : (byte)0);

        if (bypassPlacementRules)
            GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride = false;

        return result;
    }

    /// <summary>
    /// Safely marks a building object for deletion by the game engine.
    /// </summary>
    /// <param name="buildingId">The ID of the building to delete.</param>
    /// <returns><c>true</c> if the building was found and marked for deletion; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is the recommended way to delete buildings. It changes its state, allowing the
    /// game engine to clean it up gracefully on a subsequent frame.
    /// </remarks>
    [LuaApiExport("DeleteSafe")]
    public bool DeleteBuildingSafe(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Warning($"Could not find building by id: {buildingId}");
            return false;
        }
        building->r_AliveState = AliveState.MarkedForDeletion;

        //LogHelper.Debug($"GameBuildingManagerAPI: DeleteBuildingSafe({buildingId}/{new IntPtr(&building->r_AliveState).ToString("X16")}) -- Marked for deletion");
        return true;
    }

    /// <summary>
    /// Immediately deletes a building object from the game.
    /// </summary>
    /// <param name="buildingId">The ID of the building to delete.</param>
    /// <remarks>
    /// Use with caution, as immediate deletion could cause issues if other game systems are
    /// still referencing the object. Prefer <see cref="DeleteBuildingSafe"/> where possible.
    /// </remarks>
    [LuaApiExport("Delete")]
    public void Delete(int buildingId)
    {
        if (!IsValid(buildingId))
        {
            LogHelper.Warning($"Tried to delete invalid entity: {buildingId}");
            return;
        }
        BulkBuildingDetours.c_game_building_delete_hook_impl(_buildingManager, buildingId);
    }

    /// <summary>
    /// Gets all alive buildings in the game.
    /// </summary>
    [LuaApiExport("GetAllAlive")]
    public int[] GetAllAliveBuildings()
    {
        List<int> results = new List<int>();
        QueryBuildings().Where(BuildingPredicates.IsAlive).ToIdList(results);
        return [.. results];
    }

    /// <summary>
    /// Checks if a building id is valid and exists.
    /// </summary>
    /// <param name="buildingId">The ID of the building to check.</param>
    /// <returns><c>true</c> if the building was found and is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValid")]
    public bool IsValid(int buildingId)
    {
        if (!IsValidId(buildingId))
            return false;

        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Warning($"Could not find building by id: {buildingId}");
            return false;
        }
        return (int)building->r_AliveState != 0;
    }

    /// <summary>
    /// Checks if a building id is valid.
    /// </summary>
    /// <param name="buildingId">The ID to check.</param>
    /// <returns><c>true</c> if the building id is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValidId")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidId(int buildingId)
    {
        if (buildingId <= 0 || buildingId > _buildingArray.Length)
            return false;

        return true;
    }

    /// <summary>
    /// Gets the default (maximum) health for a specific building type.
    /// </summary>
    /// <param name="building">The type of building (<see cref="eStructs"/>) to query.</param>
    /// <returns>The default health value.</returns>
    [LuaApiExport("GetDefaultHealth")]
    public int GetDefaultHealth(eStructs building)
    {
        return (int)_buildingHealthDefaultsArray.GetValue((int)building);
    }

    /// <summary>
    /// Sets the default (maximum) health for a specific building type.
    /// </summary>
    /// <param name="building">The type of building (<see cref="eStructs"/>) to modify.</param>
    /// <param name="value">The new default health value to set.</param>
    [LuaApiExport("SetDefaultHealth")]
    public void SetDefaultHealth(eStructs building, UInt32 value)
    {
        _buildingHealthDefaultsArray.SetValue((int)building, value);
    }

    /// <summary>
    /// Gets the default amount of population space provided by a housing building.
    /// </summary>
    /// <param name="building">The type of housing building (<see cref="eStructs"/>).</param>
    /// <returns>The number of peasants the building can house.</returns>
    [LuaApiExport("GetDefaultHousingPopulationSpace")]
    public int GetDefaultHousingPopulationSpace(eStructs building)
    {
        return (int)_housingPopulationSpaceDefaultsArray.GetValue((int)building * 2);
    }

    /// <summary>
    /// Sets the default amount of population space provided by a housing building.
    /// </summary>
    /// <param name="building">The type of housing building (<see cref="eStructs"/>).</param>
    /// <param name="value">The new number of peasants the building should house.</param>
    [LuaApiExport("SetDefaultHousingPopulationSpace")]
    public void SetDefaultHousingPopulationSpace(eStructs building, UInt16 value)
    {
        _housingPopulationSpaceDefaultsArray.SetValue((int)building * 2, value);
    }

    /// <summary>
    /// Updates the visual resources for the specified game building.
    /// For full effect, you also have to call <see cref="GameTileManagerAPI.TryUpdateTileResourceVisualsForBuilding(int)"/> in most cases.
    /// </summary>
    /// <param name="buildingId">The building id to update</param>
    [LuaApiExport("UpdateVisuals")]
    public void UpdateVisualResourceGoods(int buildingId)
    {
        BulkBuildingDetours.c_game_update_visual_goodsyard_goods!(_buildingManager, buildingId);
    }

    /// <summary>
    /// Adds a specified amount of a good to a goodsyard
    /// </summary>
    /// <param name="buildingId">The ID of the goodsyard to modify.</param>
    /// <param name="buildingGlobalId">The GlobalID of the goodsyard to modify.</param>
    /// <param name="good">The type of good to add.</param>
    /// <param name="amount">The amount to add. This value should be positive.</param>
    /// <param name="capacity">Optional. A capacity value to override the building's default maximum storage. If null, the building's own <code>r_MaxGoodStackAmount</code> is used.</param>
    /// <param name="add">If the function should actually add to the goodsyard or not.</param>
    /// <remarks>
    /// This calls <see cref="BulkBuildingDetours.c_game_add_good_to_goodsyard_hook_impl"/> internally, so it should behave exactly like the game's internal function.
    /// </remarks>
    [LuaApiExport("AddGoodToGoodsyard")]
    public bool AddGoodToGoodsyard(int buildingId, int buildingGlobalId, eGoods good, int amount, int? capacity, bool add)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return false;
        }
        capacity ??= (int)building->r_MaxGoodStackAmount;

        return BulkBuildingDetours.c_game_add_good_to_goodsyard_hook_impl(_buildingManager, buildingId, buildingGlobalId, good, amount, (int)capacity, add == true ? 1 : 0) == 1;
    }

    /// <summary>
    /// Adds a specified amount of a good to a building's local storage.
    /// </summary>
    /// <param name="buildingId">The ID of the building to modify.</param>
    /// <param name="good">The type of good to add.</param>
    /// <param name="amount">The amount to add. This value should be positive.</param>
    /// <param name="capacity">Optional. A capacity value to override the building's default maximum storage. If null, the building's own <code>r_MaxGoodStackAmount</code> is used.</param>
    /// <remarks>
    /// This is a wrapper that finds the building by its ID and calls the underlying extension method.
    /// The final amount is clamped by the specified capacity. This method also updates the building's
    /// <code>r_CurrentGoodStackAmount</code> and <code>r_LocalStorageGoodType</code> fields to maintain consistency.
    /// Warning: This is a custom-implementation and may not perfectly match the game's internal logic.
    /// </remarks>
    [LuaApiExport("AddLocalGood")]
    public void AddLocalGoodsAmountEx(int buildingId, eGoods good, int amount, int? capacity)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }

        ref GameBuilding self = ref Unsafe.AsRef<GameBuilding>(building);
        self.AddLocalGoodsAmount(good, amount, capacity);
    }

    /// <summary>
    /// Removes a specified amount of a good from a building's local storage.
    /// </summary>
    /// <param name="buildingId">The ID of the building to modify.</param>
    /// <param name="good">The type of good to remove.</param>
    /// <param name="amount">The amount to remove. This value should be negative (e.g., -10 to remove 10).</param>
    /// <remarks>
    /// This is a wrapper that finds the building by its ID and calls the underlying extension method.
    /// The final amount is clamped at zero. This method also updates the <code>r_CurrentGoodStackAmount</code>
    /// and potentially resets the <code>r_LocalStorageGoodType</code> if the storage becomes empty.
    /// Warning: This is a custom-implementation and may not perfectly match the game's internal logic.
    /// </remarks>
    [LuaApiExport("RemoveLocalGood")]
    public void RemoveLocalGoodsAmountEx(int buildingId, eGoods good, int amount)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }

        ref GameBuilding self = ref Unsafe.AsRef<GameBuilding>(building);
        self.RemoveLocalGoodsAmount(good, amount);
    }

    /// <summary>
    /// Retrieves all local storage amounts for each good type in the specified building.
    /// </summary>
    /// <param name="buildingId">The ID of the building to query.</param>
    /// <returns>An array of integers representing the storage amount for each good, indexed by the <see cref="eGoods"/> enum. Returns an empty array if the building is not found.</returns>
    [LuaApiExport("GetAllLocalStorage")]
    public int[] GetAllLocalStorage(int buildingId)
    {
        int goodCount = Enum.GetValues(typeof(Enums.Goods)).Length;
        List<int> localStorage = new List<int>(goodCount);
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return Array.Empty<int>();
        }

        Int32* localStoragePtr = (Int32*)&building->r_NullAmount;
        for (int i = 0; i < goodCount; i++)
        {
            localStorage.Add(localStoragePtr[i]);
        }
        return localStorage.ToArray();
    }

    /// <summary>
    /// Gets the amount of a specific good stored in a building's local storage.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="goodType">The type of good to query.</param>
    /// <returns>The amount of the specified good, or 0 if the building is not found.</returns>
    [LuaApiExport("GetLocalStorage")]
    public int GetLocalStorage(int buildingId, eGoods goodType)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return 0;
        }
        Int32* localStoragePtr = (Int32*)&building->r_NullAmount;
        return localStoragePtr[(int)goodType];
    }

    /// <summary>
    /// Gets the current number of goods in the primary production/storage stack of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The current stack amount, or -1 if the building is not found.</returns>
    [LuaApiExport("GetCurrentStackAmount")]
    public int GetCurrentGoodStackAmount(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return (int)building->r_CurrentGoodStackAmount;
    }

    /// <summary>
    /// Sets the current number of goods in the primary production/storage stack of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="amount">The new stack amount to set.</param>
    [LuaApiExport("Building_SetCurrentStackAmount")]
    public void SetCurrentGoodStackAmount(int buildingId, int amount)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_CurrentGoodStackAmount = (uint)amount;
    }

    /// <summary>
    /// Gets the maximum capacity of the primary production/storage stack of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The maximum stack capacity, or -1 if the building is not found.</returns>
    [LuaApiExport("GetMaxStackAmount")]
    public int GetMaxGoodStackAmount(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return (int)building->r_MaxGoodStackAmount;
    }

    /// <summary>
    /// Sets the maximum capacity of the primary production/storage stack of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="amount">The new maximum stack capacity to set.</param>
    [LuaApiExport("Building_SetMaxStackAmount")]
    public void SetMaxGoodStackAmount(int buildingId, int amount)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_MaxGoodStackAmount = (uint)amount;
    }

    /// <summary>
    /// Gets the type of good managed by the building's primary storage.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The <see cref="eGoods"/> type, or 0 if the building is not found.</returns>
    [LuaApiExport("GetLocalStorageGoodType")]
    public eGoods GetLocalStorageGoodType(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return 0;
        }
        return building->r_LocalStorageGoodType;
    }

    /// <summary>
    /// Sets the type of good managed by the building's primary storage.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="goodType">The new <see cref="eGoods"/> type to set.</param>
    [LuaApiExport("SetLocalStorageGoodType")]
    public void SetLocalStorageGoodType(int buildingId, eGoods goodType)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_LocalStorageGoodType = goodType;
    }

    /// <summary>
    /// Gets the current health of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The current health value, or -1 if the building is not found.</returns>
    [LuaApiExport("GetCurrentHealth")]
    public int GetCurrentHealth(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return building->r_CurrentHealth;
    }

    /// <summary>
    /// Sets the current health of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="health">The new health value to set.</param>
    [LuaApiExport("SetCurrentHealth")]
    public void SetCurrentHealth(int buildingId, Int16 health)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_CurrentHealth = health;
    }

    /// <summary>
    /// Gets the max health of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The max health value, or -1 if the building is not found.</returns>
    [LuaApiExport("GetMaxHealth")]
    public int GetMaxHealth(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return building->r_MaxHealth;
    }

    /// <summary>
    /// Sets the max health of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="health">The new max health value to set.</param>
    [LuaApiExport("SetMaxHealth")]
    public void SetMaxHealth(int buildingId, UInt16 health)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_MaxHealth = health;
    }

    /// <summary>
    /// Gets the globalid of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The Globalid of the building.</returns>
    [LuaApiExport("GetGlobalId")]
    public int GetGlobalId(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return (int)building->r_GlobalId;
    }

    /// <summary>
    /// Gets the current owner of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The current owner of the building.</returns>
    [LuaApiExport("GetOwner")]
    public int GetOwner(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return building->r_PlayerIdOwner;
    }

    /// <summary>
    /// Gets the current owner of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="playerId">The new PlayerId owner.</param>
    /// <returns>The current owner of the building.</returns>
    [LuaApiExport("SetOwner")]
    public void SetOwner(int buildingId, int playerId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_PlayerIdOwner = (ushort)playerId;
    }

    /// <summary>
    /// Gets the current type of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The current type of the building. Null if not found.</returns>
    [LuaApiExport("GetType")]
    public eStructs GetType(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return eStructs.STRUCT_NULL;
        }
        return building->r_BuildingType;
    }

    /// <summary>
    /// Gets the number of game ticks a building has been on fire.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The number of ticks on fire, or 0 if not on fire or not found.</returns>
    [LuaApiExport("GetOnFireTicks")]
    public UInt16 GetOnFireTicks(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return 0;
        }
        return building->r_OnFireTicks;
    }

    /// <summary>
    /// Sets the on-fire state of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="onFireTicks">The number of ticks to set. Any value greater than 0 will cause the building to catch on fire.</param>
    [LuaApiExport("SetOnFireTicks")]
    public void SetOnFireTicks(int buildingId, UInt16 onFireTicks)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_OnFireTicks = onFireTicks;
    }

    /// <summary>
    /// Gets the beginning tile id of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The beginning TileId of the building.</returns>
    [LuaApiExport("GetBeginTileId")]
    public int GetBeginTileId(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return default;
        }
        return (int)building->r_TileIdBegin;
    }

    /// <summary>
    /// Gets the occupying tile grid size of a building.
    /// </summary>
    /// <remarks>
    /// A building of grid size 3 would be the equivalent of a woodcutters hut (3x3)
    /// </remarks>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The occupying tile grid size.</returns>
    [LuaApiExport("GetBuildingOccupyTileGridSize")]
    public int GetOccupyTileGridSize(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return default;
        }
        return (int)building->r_OccupyTileGridSize;
    }

    /// <summary>
    /// Gets all occupied tile ids of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The occupied tile id array</returns>
    [LuaApiExport("GetOccupiedTileIds")]
    public int[] GetOccupiedTileIds(int buildingId)
    {
        List<int> occupiedTiles = new List<int>();
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return [];
        }

        UInt32* grid = (&building->r_OccupiedTileIdsArrayBegin);
        int gridSize = (int)(building->r_OccupyTileGridSize * building->r_OccupyTileGridSize);
        for (int i = 0; i < gridSize; i++)
        {
            occupiedTiles.Add((int)grid[i]);
        }

        return occupiedTiles.ToArray();
    }

    /// <summary>
    /// Gets good that this building is supposed to produce currently.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The good to be produced.</returns>
    [LuaApiExport("GetProductionGood")]
    public eGoods GetCurrentlyProducedGood(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return 0;
        }
        return building->r_ProducedGoodId;
    }

    /// <summary>
    /// Sets the good that this building is supposed to produce.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="good">The good to be produced.</param>
    /// <returns>The good to be produced.</returns>
    [LuaApiExport("SetProductionGood")]
    public void SetCurrentlyProducedGood(int buildingId, eGoods good)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_ProducedGoodId = good;
    }

    /// <summary>
    /// Gets the tile begin position of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> representing the begin tile coordinates, or a default vector if not found.</returns>
    [LuaApiExport("GetBeginPosition")]
    public UnmanagedVector2<UInt16> GetBeginPosition(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return default;
        }
        return *(UnmanagedVector2<UInt16>*)(&building->r_TilePositionXBegin);
    }

    /// <summary>
    /// Gets the tile end position of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> representing the end tile coordinates, or a default vector if not found.</returns>
    [LuaApiExport("GetEndPosition")]
    public UnmanagedVector2<UInt16> GetEndPosition(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return default;
        }
        return *(UnmanagedVector2<UInt16>*)(&building->r_TilePositionXEnd);
    }

    /// <summary>
    /// Checks if a building is currently in a "sleeping" state (inactive).
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns><c>true</c> if the building is sleeping; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsSleeping")]
    public bool IsSleeping(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return false;
        }
        return building->r_IsSleeping == 1;
    }

    /// <summary>
    /// Sets the "sleeping" state of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="isSleeping">The new sleeping state to set.</param>
    [LuaApiExport("SetSleeping")]
    public void SetSleeping(int buildingId, bool isSleeping)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_IsSleeping = (byte)(isSleeping ? 1 : 0);
    }

    /// <summary>
    /// Gets the required workers of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <returns>The required total workers of the building.</returns>
    [LuaApiExport("GetRequiredWorkers")]
    public int GetRequiredWorkers(int buildingId)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return -1;
        }
        return building->r_TotalWorkersRequired;
    }

    /// <summary>
    /// Sets the required workers of a building.
    /// </summary>
    /// <param name="buildingId">The ID of the building.</param>
    /// <param name="totalWorkers">The new total workers required.</param>
    /// <returns>The required total workers of the building.</returns>
    [LuaApiExport("SetRequiredWorkers")]
    public void SetRequiredWorkers(int buildingId, ushort totalWorkers)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"TryGetBuildingById failed for buildingId: {buildingId}");
            return;
        }
        building->r_TotalWorkersRequired = totalWorkers;
    }

    //
    // Get/Setters for: Default Building Costs
    //

    /// <summary>
    /// Gets the default construction costs for a type of building from the game's core data.
    /// </summary>
    /// <param name="building">The type of building.</param>
    /// <returns>A <see cref="BuildingCost"/> struct with the default costs.</returns>
    [LuaApiExport("GetDefaultCost")]
    public BuildingCost GetDefaultCost(eStructs building)
    {
        byte* ptr = (byte*)_buildingDefaultCostsArray - 4;
        BuildingCost cost = *(BuildingCost*)(ptr + ((int)building * sizeof(BuildingCost)));

        LogHelper.Debug($"addr={new IntPtr(ptr + ((int)building * sizeof(BuildingCost))).ToString("X16")}, building={(int)building}");
        return cost;
    }

    /// <summary>
    /// Sets the default construction costs for a type of building in the game's core data.
    /// </summary>
    /// <param name="building">The type of building.</param>
    /// <param name="newCost">A <see cref="BuildingCost"/> struct with the new costs.</param>
    /// <param name="updateUnityEngineSite">Whether to update the visualo unity engine site as well.</param>
    /// <remarks>
    /// This modifies the game's base data and will also update secondary hardcoded values.
    /// <para>WARNING: This is not the function to use if you want to change costs once you are inside a map! 
    /// This function changes the defaults on a global level, the function to use to change costs per-map are <see cref="GetStoneCost"/> or similiar.</para>
    /// </remarks>
    //[LuaApiExport("Building_SetDefaultCostEx")]
    public void SetDefaultCost(eStructs building, BuildingCost newCost, bool updateUnityEngineSite = true)
    {
        int buildingIndex = (int)building;
        if (buildingIndex < 0 || buildingIndex >= _buildingDefaultCostsCount)
        {
            LogHelper.Error($"SetDefaultCost: Building type {buildingIndex} is out of bounds (Max: {_buildingDefaultCostsCount})");
            return;
        }

        byte* ptr = (byte*)_buildingDefaultCostsArray - 4;
        *(BuildingCost*)(ptr + ((int)building * sizeof(BuildingCost))) = newCost;
        LogHelper.Debug($"addr={new IntPtr(ptr + ((int)building * sizeof(BuildingCost))).ToString("X16")}, building={(int)building}");

        // we also need to update the assembly-csharp hardcoded elements
        if (updateUnityEngineSite)
        {
            SetCostVisual(building, newCost);
        }
    }

    //
    // Get/Setters for: Building Costs
    //

    /// <summary>
    /// Gets the unity-side visual cost of a building
    /// </summary>
    /// <param name="building">The building type</param>
    /// <returns>The cost</returns>
    private NativePointer<BuildingCost> GetCostVisualPtr(eStructs building)
    {
        fixed (int* unityRawPtr = GameData.game_data_txt)
        {
            return &((BuildingCost*)unityRawPtr)[(int)building];
        }
    }

    /// <summary>
    /// Sets the unity-side visual cost of a building
    /// </summary>
    /// <param name="building">The building type</param>
    /// <param name="newCost">The new cost</param>
    private void SetCostVisual(eStructs building, BuildingCost newCost)
    {
        fixed (int* unityRawPtr = GameData.game_data_txt)
        {
            BuildingCost* unityPtr = (BuildingCost*)unityRawPtr;
            unityPtr[(int)building] = newCost;
        }
    }

    /// <summary>
    /// Gets the current stone cost for a building type.
    /// </summary>
    /// <param name="building">The building type</param>
    /// <returns>The stone cost</returns>
    [LuaApiExport("GetStoneCost")]
    public Int32 GetStoneCost(eStructs building)
    {
        int index = (int)building * 5;
        return _buildingStoneCostsArray.GetValue(index);
    }

    /// <summary>
    /// Sets the current stone cost for a building type.
    /// </summary>
    /// <param name="building">The building type</param>
    /// <param name="value">The new stone cost</param>
    [LuaApiExport("SetStoneCost")]
    public void SetStoneCost(eStructs building, Int32 value)
    {
        int index = (int)building * 5;
        if (index < 0 || index >= _buildingStoneCostsArray.Length)
        {
            LogHelper.Error($"Index {index} out of bounds for building {building}");
            return;
        }

        NativePointer<BuildingCost> visualPtr = GetCostVisualPtr(building);
        if (visualPtr.IsNull)
        {
            LogHelper.Error($"Unity-pointer is null");
            return;
        }
        visualPtr.Pointer->Stone = value;
        _buildingStoneCostsArray.SetValue(index, value);
    }

    /// <summary>
    /// Gets the current raw pitch cost for a building type.
    /// </summary>
    /// <param name="building">The building type</param>
    /// <returns>The raw pitch cost</returns>
    [LuaApiExport("GetPitchCost")]
    public Int32 GetRawPitchCost(eStructs building)
    {
        return _buildingRawPitchCostsArray.GetValue((int)building * 5);
    }

    /// <summary>Sets the current raw pitch cost for a building type.</summary>
    [LuaApiExport("SetPitchCost")]
    public void SetRawPitchCost(eStructs building, Int32 value)
    {
        int index = (int)building * 5;
        if (index < 0 || index >= _buildingStoneCostsArray.Length)
        {
            LogHelper.Error($"Index {index} out of bounds for building {building}");
            return;
        }

        NativePointer<BuildingCost> visualPtr = GetCostVisualPtr(building);
        if (visualPtr.IsNull)
        {
            LogHelper.Error($"Unity-pointer is null");
            return;
        }
        visualPtr.Pointer->Pitch = value;
        _buildingRawPitchCostsArray.SetValue(index, value);
    }

    /// <summary>
    /// Gets the current gold cost for a building type.
    /// </summary>
    /// <param name="building">The building type</param>
    /// <returns>The gold cost</returns>
    [LuaApiExport("GetGoldCost")]
    public Int32 GetGoldCost(eStructs building)
    {
        int index = (int)building * 5;
        return _buildingGoldCostsArray.GetValue(index);
    }

    /// <summary>Sets the current gold cost for a building type.</summary>
    [LuaApiExport("SetGoldCost")]
    public void SetGoldCost(eStructs building, Int32 value)
    {
        int index = (int)building * 5;
        if (index < 0 || index >= _buildingStoneCostsArray.Length)
        {
            LogHelper.Error($"Index {index} out of bounds for building {building}");
            return;
        }

        NativePointer<BuildingCost> visualPtr = GetCostVisualPtr(building);
        if (visualPtr.IsNull)
        {
            LogHelper.Error($"Unity-pointer is null");
            return;
        }
        visualPtr.Pointer->Gold = value;
        _buildingGoldCostsArray.SetValue(index, value);
    }

    /// <summary>
    /// Gets the current wood cost for a building type.
    /// </summary>
    /// <param name="building">The building type</param>
    /// <returns>The wood cost</returns>
    [LuaApiExport("GetWoodCost")]
    public Int32 GetWoodCost(eStructs building)
    {
        int index = (int)building * 5;
        return _buildingWoodCostsArray.GetValue(index);
    }

    /// <summary>Sets the current wood cost for a building type.</summary>
    [LuaApiExport("SetWoodCost")]
    public void SetWoodCost(eStructs building, Int32 value)
    {
        int index = (int)building * 5;
        if (index < 0 || index >= _buildingStoneCostsArray.Length)
        {
            LogHelper.Error($"Index {index} out of bounds for building {building}");
            return;
        }

        NativePointer<BuildingCost> visualPtr = GetCostVisualPtr(building);
        if (visualPtr.IsNull)
        {
            LogHelper.Error($"Unity-pointer is null");
            return;
        }
        visualPtr.Pointer->Wood = value;
        _buildingWoodCostsArray.SetValue(index, value);
    }

    /// <summary>
    /// Gets the current iron ingot cost for a building type.
    /// </summary>
    /// <param name="building">The building type</param>
    /// <returns>The iron ingot cost</returns>
    [LuaApiExport("GetIronCost")]
    public Int32 GetIronIngotCost(eStructs building)
    {
        int index = (int)building * 5;
        return _buildingIronIngotsCostsArray.GetValue(index);
    }

    /// <summary>Sets the current iron ingot cost for a building type.</summary>
    [LuaApiExport("SetIronCost")]
    public void SetIronIngotCost(eStructs building, Int32 value)
    {
        int index = (int)building * 5;
        if (index < 0 || index >= _buildingStoneCostsArray.Length)
        {
            LogHelper.Error($"Index {index} out of bounds for building {building}");
            return;
        }

        NativePointer<BuildingCost> visualPtr = GetCostVisualPtr(building);
        if (visualPtr.IsNull)
        {
            LogHelper.Error($"Unity-pointer is null");
            return;
        }
        visualPtr.Pointer->Iron = value;
        _buildingIronIngotsCostsArray.SetValue(index, value);
    }

    /// <summary>
    /// Reduces a building's health by a raw value.
    /// </summary>
    /// <param name="buildingId">The ID of the building to damage.</param>
    /// <param name="damage">The amount of damage to inflict.</param>
    /// <param name="playerIdSource">The optional player id that is responsible.</param>
    [LuaApiExport("Damage")]
    public void Damage(int buildingId, Int16 damage, int playerIdSource = 0)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"could not find building by id: {buildingId}");
            return;
        }

        BulkBuildingDetours.c_game_buildingtile_take_damage_hook_impl(GameTileManagerAPI.Instance.GetTileManager(), (int)building->r_TileIdBegin, building->r_TilePositionXBegin, building->r_TilePositionYBegin, damage, 0, playerIdSource, 0, 0);
    }

    /// <summary>
    /// Instantly kills a unit by setting its health to zero.
    /// </summary>
    /// <param name="buildingId">The ID of the building to kill.</param>
    /// <param name="playerIdSource">The optional player id that is responsible.</param>
    [LuaApiExport("Kill")]
    public void Kill(int buildingId, int playerIdSource = 0)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"could not find building by id: {buildingId}");
            return;
        }
        building->r_CurrentHealth = 1;
        BulkBuildingDetours.c_game_buildingtile_take_damage_hook_impl(GameTileManagerAPI.Instance.GetTileManager(), (int)building->r_TileIdBegin, building->r_TilePositionXBegin, building->r_TilePositionYBegin, Int16.MaxValue, 0, playerIdSource, 0, 0);
    }

    /// <summary>
    /// Builds a wall for a player for the specified X, Y begin coords to the X, Y end coords.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="tileXBegin">The Tile X Beginning of the Wall</param>
    /// <param name="tileYBegin">The Tile Y Beginning of the Wall</param>
    /// <param name="tileXEnd">The Tile X End of the Wall.</param>
    /// <param name="tileYEnd">The Tile Y End of the Wall.</param>
    /// <param name="mv">The wall type to build.</param>
    /// <param name="a7">Unknown argument (Default: 100)</param>
    [LuaApiExport("CreateWall")]
    public void CreateWall(int playerId, int tileXBegin, int tileYBegin, int tileXEnd, int tileYEnd, eMappers mv, int a7 = 100)
    {
        BulkBuildingDetours.c_game_build_wall_hook_impl(GameTileManagerAPI.Instance.GetTileManager(), playerId, tileXBegin, tileYBegin, tileXEnd, tileYEnd, mv, a7);
    }

    /// <summary>
    /// Retrieves the low wall cost mult.
    /// Default is 0.25
    /// </summary>
    /// <returns>Low Wall Cost</returns>
    [LuaApiExport("GetLowWallCostMultiplier")]
    public float GetLowWallCostMultiplier()
    {
        return _lowWallCostMultiplier;
    }

    /// <summary>
    /// Retrieves the low wall cost mult.
    /// </summary>
    /// <param name="mult">The cost multiplier</param>
    /// <returns>Low Wall Cost</returns>
    [LuaApiExport("SetLowWallCostMultiplier")]
    public void SetLowWallCostMultiplier(float mult)
    {
        _lowWallCostMultiplier = mult;
    }

    /// <summary>
    /// Retrieves the high wall cost mult.
    /// Default is 0.5
    /// </summary>
    /// <returns>Low Wall Cost</returns>
    [LuaApiExport("GetHighWallCostMultiplier")]
    public float GetHighWallCostMultiplier()
    {
        return _highWallCostMultiplier;
    }

    /// <summary>
    /// Retrieves the high wall cost mult.
    /// </summary>
    /// <param name="mult">The cost multiplier</param>
    /// <returns>Low Wall Cost</returns>
    [LuaApiExport("SetHighWallCostMultiplier")]
    public void SetHighWallCostMultiplier(float mult)
    {
        _highWallCostMultiplier = mult;
    }

    /// <summary>
    /// Gets the building fire damage.
    /// 1 = Default for almost all buildings.
    /// </summary>
    /// <param name="building">The building</param>
    /// <returns>Fire damage to building</returns>
    [LuaApiExport("GetFireDamage")]
    public Int16 GetBuildingFireDamage(eStructs building) => GetBuildingFireDamageInternal(building);

    /// <summary>
    /// Sets the building fire damage.
    /// 1 = Default for almost all buildings.
    /// </summary>
    /// <param name="building">The building</param>
    /// <param name="damage">The damage</param>
    public void SetBuildingFireDamage(eStructs building, Int16 damage) => SetBuildingFireDamageInternal(building, damage);

    /// <summary>
    /// Repairs a given building.
    /// </summary>
    /// <param name="playerId">Do the repair as this player id</param>
    /// <param name="buildingId">The building to repair</param>
    /// <param name="woodCost">The to-be-paid wood cost</param>
    /// <param name="stoneCost">The to-be-paid stone cost</param>
    /// <param name="buildingGlobalId">The building global id to repair</param>
    [LuaApiExport("Repair")]
    public void Repair(int playerId, int buildingId, int woodCost, int stoneCost, int buildingGlobalId)
    {
        BulkBuildingDetours.c_game_building_repair_hook_impl(playerId, buildingId, woodCost, stoneCost, buildingGlobalId);
    }

    /// <summary>
    /// Returns a horse stable's unit id link
    /// </summary>
    /// <param name="buildingId">The building id</param>
    /// <param name="slot">The zero-based slot number (0-3)</param>
    /// <returns>Linked Unit Id</returns>
    [LuaApiExport("GetStablesUnitIdLink")]
    public int GetStablesUnitIdLink(int buildingId, int slot)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"could not find building by id: {buildingId}");
            return -1;
        }
        int* links = (int*)(&building->r_UsedHorse1UnitId);
        return links[slot];
    }

    /// <summary>
    /// Returns a horse stable's unit id link
    /// </summary>
    /// <param name="buildingId">The building id</param>
    /// <param name="slot">The zero-based slot number (0-3)</param>
    /// <param name="unitId">The unit id to link with</param>
    /// <param name="unitGlobalId">The unit global id to link with</param>
    /// <param name="bidirectional">Whether to set the link in both directions</param>
    /// <returns>Linked Unit Id</returns>
    [LuaApiExport("SetStablesUnitIdLink")]
    public void SetStablesUnitIdLink(int buildingId, int slot, int unitId, int unitGlobalId, bool bidirectional = true)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"could not find building by id: {buildingId}");
            return;
        }

        if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Error($"could not find unit by id: {unitId}");
            return;
        }

        UInt16* linksId = (UInt16*)(&building->r_UsedHorse1UnitId);
        UInt32* linksGlobalId = (UInt32*)(&building->r_UsedHorse1GlobalId);
        linksId[slot] = (UInt16)unitId;
        linksGlobalId[slot] = (UInt32)unitGlobalId;

        if (bidirectional)
        {
            unit->r_LinkedStableBuildingId = (UInt16)buildingId;
            unit->r_LinkedStableGlobalId = (UInt32)building->r_GlobalId;
        }
    }

    /// <summary>
    /// Unlinks a horse stable's unit id link
    /// </summary>
    /// <param name="buildingId">The building id</param>
    /// <param name="slot">The zero-based slot number (0-3)</param>
    /// <param name="bidirectional">Whether to remove the link in both directions</param>
    public void UnlinkStablesUnitIdLink(int buildingId, int slot, bool bidirectional = true)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"could not find building by id: {buildingId}");
            return;
        }

        UInt16* linksId = (UInt16*)(&building->r_UsedHorse1UnitId);
        UInt32* linksGlobalId = (UInt32*)(&building->r_UsedHorse1GlobalId);
        int unitId = linksId[slot];
        int unitGlobalId = (int)linksGlobalId[slot];
        linksId[slot] = 0;
        linksGlobalId[slot] = 0;
        if (bidirectional && unitId > 0)
        {
            if (GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
            {
                unit->r_LinkedStableBuildingId = 0;
                unit->r_LinkedStableGlobalId = 0;
            }
            else
            {
                LogHelper.Error($"could not find unit by id: {unitId} to unlink from stable");
            }
        }
    }

    /// <summary>
    /// Returns a horse stable's unit global id link
    /// </summary>
    /// <param name="buildingId">The building id</param>
    /// <param name="slot">The zero-based slot number (0-3)</param>
    /// <returns>Linked Unit Global Id</returns>
    [LuaApiExport("GetStablesUnitGlobalIdLink")]
    public int GetStablesUnitGlobalIdLink(int buildingId, int slot)
    {
        if (!TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            LogHelper.Error($"could not find building by id: {buildingId}");
            return -1;
        }
        int* links = (int*)(&building->r_UsedHorse1GlobalId);
        return links[slot];
    }

    /// <summary>
    /// Gets the current allowed build range from the keep for a specific map size.
    /// </summary>
    [LuaApiExport("GetKeepProximityRange")]
    public int GetKeepProximityRange(int mapSize) => _keepProximityTable.GetValue(mapSize);

    /// <summary>
    /// Sets the allowed build range from the keep for a specific map size.
    /// </summary>
    [LuaApiExport("SetKeepProximityRange")]
    public void SetKeepProximityRange(int mapSize, int range) => _keepProximityTable.SetValue(mapSize, range);

    /// <summary>
    /// Gets the effective keep proximity range, taking into account any global override that may be set.
    /// Script Extender only.
    /// </summary>
    internal Int64 GetKeepProximityRangeInternal(int mapSize)
    {
        int globalOverride = KeepProximityOverride.GetValue();
        if (globalOverride > 0)
        {
            return (Int64)globalOverride;
        }

        return (Int64)_keepProximityTable.GetValue(mapSize);
    }

    #region Query System

    /// <summary>
    /// Begins a high-performance query over all possible building slots.
    /// </summary>
    /// <returns>A <see cref="GameStructQuery{GameBuilding}"/> instance to build upon.</returns>
    public GameStructQuery<GameBuilding> QueryBuildings()
    {
        return new GameStructQuery<GameBuilding>(_buildingArray._array, _buildingArray.Length);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    /// <remarks>
    /// The values written to <c>results</c> are <b>one-based game IDs</b> (<c>slot index + 1</c>)
    /// and can be passed directly to <see cref="TryGetBuildingById"/> without adjustment.
    /// </remarks>
    public void ExecuteQuery(List<int> results, RefPredicate<GameBuilding> basePredicate, AliveState? stateFilter, eStructs? buildingType, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
    {
        GameStructQuery<GameBuilding> query = QueryBuildings().Where(basePredicate);

        if (stateFilter.HasValue)
        {
            AliveState state = stateFilter.Value;
            query = query.Where((in u) => u.r_AliveState == state);
        }

        if (buildingType.HasValue)
        {
            eStructs type = buildingType.Value;
            query = query.Where((in building) => building.r_BuildingType == type);
        }

        if (relationship != PlayerRelationship.Any && povPlayerId.HasValue)
        {
            switch (relationship)
            {
                case PlayerRelationship.Allied:
                    query = query.Where((in building) => GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(povPlayerId.Value, building.r_PlayerIdOwner));
                    break;
                case PlayerRelationship.Enemy:
                    query = query.Where((in building) => !GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(povPlayerId.Value, building.r_PlayerIdOwner));
                    break;
                case PlayerRelationship.Self:
                    query = query.Where(BuildingPredicates.IsOwnedByPlayer(povPlayerId.Value));
                    break;
            }
        }

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for convenience.
    /// </summary>
    public static class BuildingPredicates
    {
        /// <summary>A predicate that matches buildings that are currently active.</summary>
        public static readonly RefPredicate<GameBuilding> IsAlive = static (in building) => building.r_AliveState == AliveState.IsAlive;

        /// <summary>A predicate that matches buildings that have been marked for deletion.</summary>
        public static readonly RefPredicate<GameBuilding> IsDead = static (in building) => building.r_AliveState == AliveState.MarkedForDeletion;

        /// <summary>A predicate that matches any buildings, used as a default for generic queries.</summary>
        public static readonly RefPredicate<GameBuilding> Any = static (in building) => true;

        /// <summary>A predicate that matches any storage building</summary>
        public static readonly RefPredicate<GameBuilding> IsStorage = static (in building) => building.r_BuildingType == eStructs.STRUCT_GOODS_YARD || building.r_BuildingType == eStructs.STRUCT_GRANARY || building.r_BuildingType == eStructs.STRUCT_ARMOURY;

        /// <summary>Creates a predicate that matches buildings of a specific type.</summary>
        public static RefPredicate<GameBuilding> IsOfType(eStructs buildingType) => (in building) => building.r_BuildingType == buildingType;

        /// <summary>Creates a predicate that matches buildings owned by a player</summary>
        public static RefPredicate<GameBuilding> IsOwnedByPlayer(int playerId) => (in building) => building.r_PlayerIdOwner == playerId;

        /// <summary>Creates a predicate that matches buildings which contain a specific good locally</summary>
        public static RefPredicate<GameBuilding> ContainsLocalGood(eGoods good) => (in building) => ((int*)Unsafe.AsPointer(ref Unsafe.AsRef(in building.r_NullAmount)))[(int)good] > 0;

        /// <summary>Creates a predicate that matches buildings which contain a specific current good stack</summary>
        public static RefPredicate<GameBuilding> ContainsCurrentGoodStack(eGoods good) => (in building) => building.r_LocalStorageGoodType == good && building.r_CurrentGoodStackAmount > 0;

        /// <summary>
        /// Creates a predicate that matches buildings containing a specific good,
        /// checking both the primary production stack and the general local storage.
        /// </summary>
        public static RefPredicate<GameBuilding> ContainsGoodInAnyStorage(eGoods good) =>
            (in building) =>
                (building.r_LocalStorageGoodType == good && building.r_CurrentGoodStackAmount > 0)
                ||
                (((uint*)Unsafe.AsPointer(ref Unsafe.AsRef(in building.r_NullAmount)))[(int)good] > 0);

        /// <summary>
        /// Matches buildings whose origin tile falls within the specified rectangle.
        /// Reads <see cref="GameBuilding.r_TilePositionXBegin"/> and
        /// <see cref="GameBuilding.r_TilePositionYBegin"/> directly via the <c>in</c>
        /// parameter instead of calling <c>CurrentTilePosition()</c>, which uses a
        /// <c>fixed</c> statement that pins a stack copy rather than the native array
        /// element when the struct is on the unmanaged heap.
        /// </summary>
        public static RefPredicate<GameBuilding> IsWithinRect(int x, int y, int width, int height) =>
            (in GameBuilding b) =>
                b.r_TilePositionXBegin >= x && b.r_TilePositionXBegin < x + width &&
                b.r_TilePositionYBegin >= y && b.r_TilePositionYBegin < y + height;

        /// <summary>
        /// Matches buildings whose origin tile falls within the specified sphere (circle).
        /// Uses a pre-computed squared radius to avoid <c>Math.Sqrt</c> per entry.
        /// Same rationale as <see cref="IsWithinRect"/> for avoiding <c>CurrentTilePosition()</c>.
        /// </summary>
        public static RefPredicate<GameBuilding> IsWithinSphere(int cx, int cy, int radius)
        {
            int rSq = radius * radius;
            return (in GameBuilding b) =>
            {
                int dx = b.r_TilePositionXBegin - cx;
                int dy = b.r_TilePositionYBegin - cy;
                return dx * dx + dy * dy <= rSq;
            };
        }
    }

    /// <summary>
    /// Fills a list with IDs of all buildings, with optional filters (no spatial constraint).
    /// </summary>
    /// <param name="results">The list to be cleared and filled with building IDs.</param>
    /// <param name="stateFilter">Optional: Filters for buildings in a specific <see cref="AliveState"/>.</param>
    /// <param name="buildingType">Optional: Filters for buildings of a specific <see cref="eStructs"/>.</param>
    /// <param name="relationship">Optional: Filters for searching for specific played alignment <see cref="PlayerRelationship"/>.</param>
    /// <param name="povPlayerId">Optional: Player alignment POV playerId. Only used if relationship is not null.</param>
    public void GetAllBuildings(List<int> results, AliveState? stateFilter = null, eStructs? buildingType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
    {
        ExecuteQuery(results, BuildingPredicates.Any, stateFilter, buildingType, relationship, povPlayerId);
    }

    #endregion
}
