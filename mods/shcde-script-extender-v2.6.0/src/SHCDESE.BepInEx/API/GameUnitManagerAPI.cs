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
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with game units.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for creating, deleting,
/// and querying units in the game world. It offers direct manipulation of unit properties,
/// access to game data tables (like costs and damage), and a query system.
/// </remarks>
[LuaApiNamespace("Unit")]
public unsafe sealed class GameUnitManagerAPI
{
    private static readonly Lazy<GameUnitManagerAPI> _lazy = new(() => new GameUnitManagerAPI());
    public static GameUnitManagerAPI Instance => _lazy.Value;

    /// <summary>The maximum number of units the game pre-allocates memory for.</summary>
    internal const int NUM_PREALLOC_UNITS = 10000;

    internal GameUnitManager* _unitManager;
    internal SimpleNativeArray<GameUnit> _unitArray;
    internal SimpleNativeArray<UInt16> _localPlayerArmyCountsArray;

    // --- Managed Stats Arrays/Dicts ---
    internal ManagedNativeArray<UInt32> _healthDefaultsArray;
    internal ManagedNativeArray<UInt32> _speedDefaultsArray;    // lower = faster
    internal ManagedNativeArray<Int32> _unitsGoldCostsArray;
    internal ManagedNativeArray<UnitGoodCosts> _euUnitsGoodTypeCostsArray;

    // --- Damage Tables ---

    /// <summary>Gets the damage lookup table for standard melee attacks.</summary>
    public ManagedNativeMatrix<Int32> MeleeDamageLookupTable { get; private set; }

    /// <summary>Gets the damage lookup table for the Eunuch's area-of-effect melee attack.</summary>
    internal ManagedNativeArray<Int32> _meleeEunuchAOEDamageArray;

    /// <summary>Gets the damage lookup table for arrow-based ranged attacks.</summary>
    internal ManagedNativeArray<Int32> _rangedArrowDamageArray;

    /// <summary>Gets the damage lookup table for bolt-based ranged attacks.</summary>
    internal ManagedNativeArray<Int32> _rangedBoltDamageArray;

    /// <summary>Gets the damage lookup table for slinger-based ranged attacks.</summary>
    internal ManagedNativeArray<Int32> _rangedSlingerDamageArray;

    /// <summary>Gets the damage lookup table for javelin-based ranged attacks.</summary>
    internal ManagedNativeArray<Int32> _rangedJavelinDamageArray;

    // Managed Dictionaries

    /// <summary>
    /// All unit gold costs
    /// </summary>
    internal ManagedDictionary<eChimps, int> _unitGoldCostsDict;

    /// <summary>
    /// All unit fire damage values
    /// </summary>
    internal ManagedDictionary<eChimps, int> _unitFireDamageDict;

    /// <summary>
    /// All unit heal values from bedouin healer
    /// </summary>
    internal ManagedDictionary<eChimps, int> _unitBedouinHealingDict;

    /// <summary>
    /// Controls what units can attack wall tiles
    /// If a unit is not listed in here, the defaults will be applied (by internal engine)
    /// </summary>
    internal ManagedDictionary<eChimps, int> _unitCanAttackWallsDict;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int GetUnitBedouinHealingDelegate(eChimps unit);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    internal delegate int GetUnitFireDamageDelegate(eChimps unit);

    // --- Others ---

    /// <summary>All unit ids in this list will be unselectable in-game.</summary>
    internal HashSet<int> _unselectableUnitIds;

    /// <summary>All unit ids in this list will be hidden in-game.</summary>
    internal HashSet<int> _visualHiddenUnitIds;

    /// <summary>All unit ids that have a custom scale attached.</summary>
    internal Dictionary<int, Vector3> _unitSpriteScaleOverrides;

    /// <summary>
    /// All unit types that are being tracked (AI States)
    /// </summary>
    internal HashSet<eChimps> _registeredAIStateTrackers;

    /// <summary>
    /// The default fire damage to all units
    /// </summary>
    internal const int DEFAULT_UNIT_FIRE_DAMAGE = 100;

    /// <summary>
    /// The default bedouin heal to all units
    /// </summary>
    internal const int DEFAULT_BEDOUIN_HEAL = 10;

    /// <summary>
    /// Cached unit enum amount
    /// </summary>
    private static readonly int ChimpEnumCount = Enum.GetValues(typeof(eChimps)).Length;

    private int _initialized = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameUnitManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GameUnitManagerAPI()
    {
        // --- Damage Tables ---
        MeleeDamageLookupTable = new ManagedNativeMatrix<Int32>((void*)(GameGlobalsManager.Instance.MeleeDamageTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount * ChimpEnumCount, ChimpEnumCount);
        _meleeEunuchAOEDamageArray = new ManagedNativeArray<Int32>((byte*)(GameGlobalsManager.Instance.MeleeEunuchAOEDamageTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);
        _rangedArrowDamageArray = new ManagedNativeArray<Int32>((byte*)(GameGlobalsManager.Instance.RangedArrowDamageTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);
        _rangedBoltDamageArray = new ManagedNativeArray<Int32>((byte*)(GameGlobalsManager.Instance.RangedBoltDamageTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);
        _rangedSlingerDamageArray = new ManagedNativeArray<Int32>((byte*)(GameGlobalsManager.Instance.RangedSlingerDamageTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);
        _rangedJavelinDamageArray = new ManagedNativeArray<Int32>((byte*)(GameGlobalsManager.Instance.RangedJavelinDamageTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);

        // --- Core ---
        _unitManager = (GameUnitManager*)GameGlobalsManager.Instance.GameUnitManagerVA;
        _unitArray = new SimpleNativeArray<GameUnit>((byte*)&_unitManager->GameUnitArray, NUM_PREALLOC_UNITS);

        // --- Stat Arrays ---
        _healthDefaultsArray = new ManagedNativeArray<UInt32>((byte*)(GameGlobalsManager.Instance.UnitHealthTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);
        _speedDefaultsArray = new ManagedNativeArray<UInt32>((byte*)(GameGlobalsManager.Instance.SpeedTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), ChimpEnumCount);
        _localPlayerArmyCountsArray = new SimpleNativeArray<UInt16>((byte*)GameGlobalsManager.Instance.LocalPlayerArmyCountsVA, ChimpEnumCount);
        _unitsGoldCostsArray = new ManagedNativeArray<Int32>((byte*)(GameGlobalsManager.Instance.UnitEUGoldCostTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), 30);
        _euUnitsGoodTypeCostsArray = new ManagedNativeArray<UnitGoodCosts>((byte*)(GameGlobalsManager.Instance.UnitEUGoodTypeCostsTableRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), 7);

        // --- Collections ---
        _unselectableUnitIds = new HashSet<int>();
        _visualHiddenUnitIds = new HashSet<int>();
        _unitSpriteScaleOverrides = new Dictionary<int, Vector3>();
        _registeredAIStateTrackers = new HashSet<eChimps>();

        Dictionary<eChimps, int> initialGoldCosts = new Dictionary<eChimps, int>()
        {
            [eChimps.CHIMP_TYPE_ENGINEER] = 30,
            [eChimps.CHIMP_TYPE_TUNNELER] = 30,
            [eChimps.CHIMP_TYPE_LADDERMAN] = 4,
            [eChimps.CHIMP_TYPE_MONK] = 10,
            [eChimps.CHIMP_TYPE_ARAB_BOW] = 75,
            [eChimps.CHIMP_TYPE_ARAB_SLAVE] = 5,
            [eChimps.CHIMP_TYPE_ARAB_SLINGER] = 12,
            [eChimps.CHIMP_TYPE_ARAB_ASSASIN] = 60,
            [eChimps.CHIMP_TYPE_ARAB_HORSEMAN] = 80,
            [eChimps.CHIMP_TYPE_ARAB_SWORDSMAN] = 80,
            [eChimps.CHIMP_TYPE_ARAB_GRENADIER] = 100,
            [eChimps.CHIMP_TYPE_ARCHER] = 12,
            [eChimps.CHIMP_TYPE_SPEARMAN] = 8,
            [eChimps.CHIMP_TYPE_MACEMAN] = 20,
            [eChimps.CHIMP_TYPE_XBOWMAN] = 20,
            [eChimps.CHIMP_TYPE_PIKEMAN] = 20,
            [eChimps.CHIMP_TYPE_SWORDSMAN] = 40,
            [eChimps.CHIMP_TYPE_KNIGHT] = 40,
            [eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER] = 25,
            [eChimps.CHIMP_TYPE_BEDOUIN_SAPPER] = 50,
            [eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER] = 50,
            [eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL] = 100,
            [eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER] = 80,
            [eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH] = 100,
            [eChimps.CHIMP_TYPE_BEDOUIN_HEALER] = 100,
            [eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER] = 130
        };
        _unitGoldCostsDict = new ManagedDictionary<eChimps, int>(initialGoldCosts, 0);

        Dictionary<eChimps, int> initialFireDamage = new Dictionary<eChimps, int>()
        {
            [eChimps.CHIMP_TYPE_LORD] = 25,
            [eChimps.CHIMP_TYPE_FIREMAN] = 1,
            [eChimps.CHIMP_TYPE_ARAB_GRENADIER] = 10,
            [eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER] = 10,
            [eChimps.CHIMP_TYPE_BEDOUIN_SAPPER] = 5,
            [eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER] = 5,
        };
        _unitFireDamageDict = new ManagedDictionary<eChimps, int>(initialFireDamage, DEFAULT_UNIT_FIRE_DAMAGE);

        _unitBedouinHealingDict = new ManagedDictionary<eChimps, int>(DEFAULT_BEDOUIN_HEAL);

        _unitCanAttackWallsDict = new ManagedDictionary<eChimps, int>(-2);

        LogHelper.Information($"_gameManager: {new IntPtr(_unitManager).ToString("X16")}");
        LogHelper.Information($"_unitArray: {new IntPtr(_unitArray._array).ToString("X16")}");
        LogHelper.Information($"_meleeEunuchAOEDamageArray: {new IntPtr(_meleeEunuchAOEDamageArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_rangedArrowDamageArray: {new IntPtr(_rangedArrowDamageArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_rangedBoltDamageArray: {new IntPtr(_rangedBoltDamageArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_rangedSlingerDamageArray: {new IntPtr(_rangedSlingerDamageArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_rangedJavelinDamageArray: {new IntPtr(_rangedJavelinDamageArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_healthDefaultsArray: {new IntPtr(_healthDefaultsArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_speedDefaultsArray: {new IntPtr(_speedDefaultsArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_localPlayerArmyCountsArray: {new IntPtr(_localPlayerArmyCountsArray._array).ToString("X16")}");
        LogHelper.Information($"_euUnitsGoldCostsArray: {new IntPtr(_unitsGoldCostsArray.GetBaseAddress()).ToString("X16")}");
        LogHelper.Information($"_euUnitsGoodTypeCostArray: {new IntPtr(_euUnitsGoodTypeCostsArray.GetBaseAddress()).ToString("X16")}");

        LogHelper.Information($"SizeOf(GameUnitManager)={Marshal.SizeOf<GameUnitManager>()}, SizeOf(GameUnit)={Marshal.SizeOf<GameUnit>()}");
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
        UnitR3EventHooks.OnUnitUnityVisualSpawn.Observable.Subscribe(OnUnitUnityVisualUpdate);
    }

    /// <summary>
    /// Event handler called when a map is unloaded.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs e)
    {
        LogHelper.Information($"Unloading");

        Instance._unselectableUnitIds.Clear();
        Instance._unitSpriteScaleOverrides.Clear();
        Instance._visualHiddenUnitIds.Clear();

        Instance.MeleeDamageLookupTable.ClearOverrides();
        Instance._meleeEunuchAOEDamageArray.ClearOverrides();
        Instance._rangedArrowDamageArray.ClearOverrides();
        Instance._rangedBoltDamageArray.ClearOverrides();
        Instance._rangedJavelinDamageArray.ClearOverrides();
        Instance._rangedSlingerDamageArray.ClearOverrides();
        Instance._euUnitsGoodTypeCostsArray.ClearOverrides();
        Instance._unitGoldCostsDict.ClearOverrides();
        Instance._healthDefaultsArray.ClearOverrides();
        Instance._speedDefaultsArray.ClearOverrides();
        Instance._unitBedouinHealingDict.ClearOverrides();
        Instance._unitFireDamageDict.ClearOverrides();

    }

    /// <summary>
    /// Handles unity-side addchimp event
    /// </summary>
    private static void OnUnitUnityVisualUpdate(EventAPI.Units.UnitUnityVisualSpawnEventArgs e)
    {
        //LogHelper.Verbose("Handling custom unit visuals");

        // Custom sprite-scale support.
        Vector3 scale = GameUnitManagerAPI.Instance.GetSpriteScale(e.UnitId);
        UnityEngine.Vector3 uVec3 = *(UnityEngine.Vector3*)&scale;
        e.SpriteRenderer.transform.localScale = uVec3;

        // Visual hide support
        if (Instance.IsVisualHidden(e.UnitId))
        {
            e.SpriteRenderer.enabled = false;
        }
        else e.SpriteRenderer.enabled = true;
    }

    /// <summary>
    /// Gets a native pointer to the current game unit manager instance.
    /// </summary>
    /// <returns>A <see cref="NativePointer{GameUnitManager}"/> representing the game unit manager.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativePointer<GameUnitManager> GetUnitManager()
    {
        return _unitManager;
    }

    /// <summary>
    /// Returns the underlying array of game units managed by this instance.
    /// </summary>
    /// <returns>A <see cref="SimpleNativeArray{GameUnit}"/> containing all units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GameUnit> GetUnitArray()
    {
        return _unitArray;
    }

    /// <summary>
    /// Returns a span representing the current collection of units managed by the instance.
    /// </summary>
    /// <returns>A <see cref="Span{GameUnit}"/> containing the units.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<GameUnit> GetUnitsAsSpan()
    {
        return _unitArray.AsSpan();
    }

    //
    // Common Functions
    //

    /// <summary>
    /// Attempts to retrieve a direct, raw pointer to a unit by its ID.
    /// </summary>
    /// <param name="unitId">The ID of the unit to retrieve.</param>
    /// <param name="unit">When this method returns, contains a pointer to the unit if found; otherwise, null.</param>
    /// <returns><c>true</c> if the unit was found and is within the valid array bounds; otherwise, <c>false</c>.</returns>
    public bool TryGetUnitById(int unitId, out GameUnit* unit)
    {
        unit = null;
        if (!IsValidId(unitId))
        {
            LogHelper.Error($"Tried to access unit index that was out of range: [{unitId}/{_unitArray.Length}]");
            return false;
        }

        if (_unitArray._array == null)
            return false;

        unit = &_unitArray._array[unitId - 1];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a unit by its ID.
    /// </summary>
    /// <param name="unitId">The ID of the unit to retrieve.</param>
    /// <param name="unit">When this method returns, contains a <see cref="NativePointer{GameUnit}"/> wrapping the unit object if found; otherwise, an invalid pointer.</param>
    /// <returns><c>true</c> if the unit was found; otherwise, <c>false</c>.</returns>
    public bool TryGetUnitByIdEx(int unitId, out NativePointer<GameUnit> unit)
    {
        bool result = TryGetUnitById(unitId, out GameUnit* tribePtr);
        unit = new NativePointer<GameUnit>(tribePtr);
        return result;
    }

    /// <summary>
    /// Internal way to retrieve the damage of fire to units
    /// </summary>
    /// <param name="unit">The unit</param>
    /// <returns>Damage</returns>
    internal static int GetFireDamageInternal(eChimps unit)
    {
        return Instance._unitFireDamageDict.GetValue(unit);
    }

    /// <summary>
    /// Internal way to set the damage of fire to units
    /// </summary>
    /// <param name="unit">The unit</param>
    /// <param name="damage">Damage</param>
    internal static void SetFireDamageInternal(eChimps unit, int damage)
    {
        Instance._unitFireDamageDict.SetValue(unit, damage);
    }

    /// <summary>
    /// Internal way to retrieve the bedouin heal to units
    /// </summary>
    /// <param name="unit">The unit</param>
    /// <returns>Heal</returns>
    internal static int GetBedouinHealInternal(eChimps unit)
    {
        return Instance._unitBedouinHealingDict.GetValue(unit);
    }

    /// <summary>
    /// Internal way to set the bedouin heal to units
    /// </summary>
    /// <param name="unit">The unit</param>
    /// <param name="heal">Damage</param>
    internal static void SetBedouinHealInternal(eChimps unit, int heal)
    {
        Instance._unitBedouinHealingDict.SetValue(unit, heal);
    }

    /// <summary>
    /// Get whether a unit type can attack wall tiles.
    /// </summary>
    /// <param name="unit">The unit type</param>
    /// <returns>Whether it can attack walls</returns>
    [LuaApiExport("GetCanAttackWalls")]
    public int GetCanAttackWalls(eChimps unit)
    {
        return _unitCanAttackWallsDict.GetValue(unit);
    }

    /// <summary>
    /// Set whether a unit type can attack wall tiles.
    /// There are sets of behaviour values which are semi-documented.
    /// 0x5F5E100: Can attack
    /// 5625: Catapult
    /// 7225: Trebutchet
    /// 4900: Mangonel
    /// -1: Cannot attack
    /// </summary>
    /// <param name="unit">The unit type</param>
    /// <param name="behaviour">Whether it can attack walls or other interaction</param>
    [LuaApiExport("SetCanAttackWalls")]
    public void SetCanAttackWalls(eChimps unit, int behaviour)
    {
        _unitCanAttackWallsDict.SetValue(unit, behaviour);
    }

    /// <summary>
    /// Creates a unit at a specific world tile position.
    /// </summary>
    /// <param name="playerOwnerId">The ID of the player who will own and control the unit.</param>
    /// <param name="playerColorId">The player sprite color this unit will use.</param>
    /// <param name="worldTileX">The world tile X-coordinate.</param>
    /// <param name="worldTileY">The world tile Y-coordinate.</param>
    /// <param name="heightElevation">The unit's height elevation on the tile.</param>
    /// <param name="chimp">The type of unit (<see cref="eChimps"/>) to create.</param>
    /// <returns>The unique ID of the newly created unit.</returns>
    [LuaApiExport("CreateWorld")]
    public Int64 CreateUnitWorld(int playerOwnerId, int playerColorId, int worldTileX, int worldTileY, int heightElevation, eChimps chimp)
    {
        return BulkUnitDetours.c_game_unit_spawn_ex_hook_impl(_unitManager, playerOwnerId, playerColorId, worldTileX, worldTileY, heightElevation, chimp);
    }

    /// <summary>
    /// Creates a unit at a local (8x scaled) tile position.
    /// </summary>
    /// <param name="playerOwnerId">The ID of the player who will own and control the unit.</param>
    /// <param name="playerColorId">The player sprite color this unit will use.</param>
    /// <param name="localTileX">The local tile X-coordinate (world coordinate * 8).</param>
    /// <param name="localTileY">The local tile Y-coordinate (world coordinate * 8).</param>
    /// <param name="heightElevation">The unit's height elevation on the tile.</param>
    /// <param name="chimp">The type of unit (<see cref="eChimps"/>) to create.</param>
    /// <returns>The unique ID of the newly created unit.</returns>
    /// <remarks>This method internally calls <see cref="CreateUnitWorld"/> after scaling the coordinates.</remarks>
    [LuaApiExport("CreateLocal")]
    public Int64 CreateUnitLocal(int playerOwnerId, int playerColorId, int localTileX, int localTileY, int heightElevation, eChimps chimp) => CreateUnitWorld(playerOwnerId, playerColorId, localTileX * 8, localTileY * 8, heightElevation, chimp);

    /// <summary>
    /// Immediately deletes a unit from the game.
    /// </summary>
    /// <param name="unitId">The ID of the unit to delete.</param>
    /// <remarks>For a safer alternative, consider using <see cref="DeleteUnitSafe"/></remarks>
    [LuaApiExport("Delete")]
    public void DeleteUnit(int unitId)
    {
        if (!IsValid(unitId))
        {
            LogHelper.Warning($"Tried to delete invalid entity: {unitId}");
            return;
        }
        BulkUnitDetours.c_game_unit_delete_hook_impl(_unitManager, (UInt32)unitId);
    }

    /// <summary>
    /// Safely marks a unit object for deletion by the game engine.
    /// </summary>
    /// <param name="unitId">The ID of the unit to delete.</param>
    /// <returns><c>true</c> if the unit was found and marked for deletion; otherwise, <c>false</c>.</returns>
    /// <remarks>
    /// This is the recommended way to delete unit. It changes its state, allowing the
    /// game engine to clean it up gracefully on a subsequent frame.
    /// </remarks>
    [LuaApiExport("DeleteSafe")]
    public bool DeleteUnitSafe(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return false;
        }
        unit->r_AliveState = AliveState.MarkedForDeletion;

        //LogHelper.Debug($"{unitId}/{new IntPtr(&unit->r_AliveState).ToString("X16")} -- Marked for deletion");
        return true;
    }

    /// <summary>
    /// Gets all alive units in the game.
    /// </summary>
    [LuaApiExport("GetAllAlive")]
    public int[] GetAllAliveUnits()
    {
        List<int> results = new List<int>();
        QueryUnits().Where(UnitPredicates.IsAlive).ToIdList(results);
        return [.. results];
    }

    /// <summary>
    /// Checks if a unit id is valid and exists.
    /// </summary>
    /// <param name="unitId">The ID of the unit to check.</param>
    /// <returns><c>true</c> if the unit was found and is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValid")]
    public bool IsValid(int unitId)
    {
        if (!IsValidId(unitId))
            return false;

        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return false;
        }
        return (int)unit->r_AliveState != 0;
    }

    /// <summary>
    /// Checks if a unit id is valid.
    /// </summary>
    /// <param name="unitId">The ID to check.</param>
    /// <returns><c>true</c> if the unit id is valid; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsValidId")]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsValidId(int unitId)
    {
        if (unitId <= 0 || unitId > _unitArray.Length)
            return false;

        return true;
    }

    /// <summary>
    /// Get the globalId by a unit id
    /// </summary>
    /// <param name="unitId">The unit id to get the global id from.</param>
    /// <returns>The global id</returns>
    [LuaApiExport("GetGlobalId")]
    public int GetGlobalId(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return -1;
        }
        return (int)unit->r_GlobalId;
    }

    /// <summary>
    /// Get a unit by globalId
    /// </summary>
    /// <param name="globalId">The unit global id.</param>
    /// <returns>The global id; Otherwise -1</returns>
    [LuaApiExport("GetByGlobalId")]
    public int GetByGlobalId(int globalId)
    {
        List<int> results = new List<int>();
        QueryUnits().Where(UnitPredicates.HasGlobalId(globalId)).ToIdList(results);
        if (results.Count == 0)
            return -1;
        return results[0];
    }

    /// <summary>
    /// Get the tribe of a unit
    /// </summary>
    /// <param name="unitId">The unit id to get the global id from.</param>
    /// <returns>The global id</returns>
    [LuaApiExport("GetTribe")]
    public int GetTribe(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return -1;
        }
        return (int)unit->r_TribeId;
    }

    /// <summary>
    /// Get the player owner of a unit
    /// </summary>
    /// <param name="unitId">The unit id to get the player owner from.</param>
    /// <returns>The Player owner id</returns>
    [LuaApiExport("GetOwner")]
    public int GetOwner(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return -1;
        }
        return (int)unit->r_ControllableForPlayerId;
    }

    /// <summary>
    /// Get the type of a unit
    /// </summary>
    /// <param name="unitId">The unit id to get the type from.</param>
    /// <returns>The unit type (<see cref="eChimps"/>)</returns>
    [LuaApiExport("GetType")]
    public eChimps GetType(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return eChimps.CHIMP_TYPE_NULL;
        }
        return unit->r_UnitChimp;
    }

    /// <summary>
    /// Retrieves the total count of a specific army unit type for the local player.
    /// </summary>
    /// <param name="chimp">The unit type (<see cref="eChimps"/>) to count.</param>
    /// <returns>The total count of the specified unit in the local player's army.</returns>
    /// <remarks>This reads directly from the game's internal army count table.</remarks>
    [LuaApiExport("GetArmyCount")]
    public int GetUnitArmyCount(eChimps chimp)
    {
        return _localPlayerArmyCountsArray.GetValue((int)chimp);
    }

    /// <summary>
    /// Gets the default (maximum) health for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <returns>The default health value.</returns>
    [LuaApiExport("GetDefaultHealth")]
    public UInt32 GetDefaultHealth(eChimps chimp)
    {
        return _healthDefaultsArray.GetValue((int)chimp);
    }

    /// <summary>
    /// Sets the default (maximum) health for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <param name="value">The new default health value to set.</param>
    [LuaApiExport("SetDefaultHealth")]
    public void SetDefaultHealth(eChimps chimp, UInt32 value)
    {
        _healthDefaultsArray.SetValue((int)chimp, value);
    }

    /// <summary>
    /// Gets the default speed for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <returns>The default speed value. A lower value means faster movement.</returns>
    [LuaApiExport("GetDefaultSpeed")]
    public UInt16 GetDefaultSpeed(eChimps chimp)
    {
        // Speed is stored in the lower 16 bits of a 32-bit native value
        return (UInt16)(_speedDefaultsArray.GetValue((int)chimp) & 0xFFFF);
    }

    /// <summary>
    /// Sets the default speed for a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <param name="value">The new default speed value to set (clamped between 0 and 6).</param>
    [LuaApiExport("SetDefaultSpeed")]
    public void SetDefaultSpeed(eChimps chimp, UInt16 value)
    {
        if (value > 6)
            value = 6;

        // preserve upper 16 bits
        UInt32 current = _speedDefaultsArray.GetValue((int)chimp);
        UInt32 newValue = (current & 0xFFFF0000u) | value;
        _speedDefaultsArray.SetValue((int)chimp, newValue);
    }

    /// <summary>
    /// Gets the default run speed bonus for a specific cavalry unit type.
    /// </summary>
    /// <param name="chimp">The cavalry unit type.</param>
    /// <returns>The default speed bonus.</returns>
    [LuaApiExport("GetDefaultCavalryRunSpeedBonus")]
    public UInt16 GetDefaultCavalryRunSpeedBonus(eChimps chimp)
    {
        switch (chimp)
        {
            case eChimps.CHIMP_TYPE_KNIGHT:
                return GameGlobalsManager.Instance.KnightRunSpeedBonus!.GetValue();
            case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                return GameGlobalsManager.Instance.ArabHorsemanRunSpeedBonus!.GetValue();
            case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                return GameGlobalsManager.Instance.BedouinCamelLancerRunSpeedBonus!.GetValue();
            case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                return GameGlobalsManager.Instance.BedouinHeavyCamelRunSpeedBonus!.GetValue();
            default:
                return 1;
        }
    }

    /// <summary>
    /// Sets the default run speed for a specific cavalry unit type.
    /// </summary>
    /// <param name="chimp">The cavalry unit type.</param>
    /// <param name="value">The new default speed bonus value to set.</param>
    [LuaApiExport("SetDefaultCavalryRunSpeedBonus")]
    public void SetDefaultCavalryRunSpeedBonus(eChimps chimp, UInt16 value)
    {
        switch (chimp)
        {
            case eChimps.CHIMP_TYPE_KNIGHT:
                GameGlobalsManager.Instance.KnightRunSpeedBonus!.SetValue(value);
                break;
            case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
                GameGlobalsManager.Instance.ArabHorsemanRunSpeedBonus!.SetValue(value);
                break;
            case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
                GameGlobalsManager.Instance.BedouinCamelLancerRunSpeedBonus!.SetValue(value);
                break;
            case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
                GameGlobalsManager.Instance.BedouinHeavyCamelRunSpeedBonus!.SetValue(value);
                break;
            default:
                return;
        }
    }

    /// <summary>
    /// Gets the gold cost to recruit a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <returns>The gold cost; Otherwise 0.</returns>
    [LuaApiExport("GetGoldCost")]
    public int GetUnitGoldCost(eChimps chimp) => _unitGoldCostsDict.GetValue(chimp);

    /// <summary>
    /// Sets the gold cost to recruit a specific unit type.
    /// </summary>
    /// <param name="chimp">The unit type.</param>
    /// <param name="value">The new gold cost to set.</param>
    [LuaApiExport("SetGoldCost")]
    public void SetUnitGoldCost(eChimps chimp, int value)
    {
        _unitGoldCostsDict.SetValue(chimp, value);
        _unitsGoldCostsArray.SetValue((int)chimp, value);
    }

    /// <summary>
    /// Gets the resource costs (besides gold) to recruit a European unit type.
    /// </summary>
    /// <param name="chimp">The European unit type.</param>
    /// <returns>A <see cref="UnitGoodCosts"/> struct detailing the required goods.</returns>
    [LuaApiExport("GetGoodCosts")]
    public UnitGoodCosts GetUnitGoodCosts(eChimps chimp)
    {
        int index = (chimp - eChimps.CHIMP_TYPE_ARCHER);
        return _euUnitsGoodTypeCostsArray.GetValue(index);
    }

    /// <summary>
    /// Sets the resource costs (besides gold) to recruit a European unit type.
    /// INFO: When wanting to signify horse requirement, you need to set it as the last good requirement!
    /// Unit_SetUnitGoodCosts(eChimps.CHIMP_TYPE_KNIGHT, eGoods.STORED_SWORDS, eGoods.STORED_METAL_ARMOUR, eGoods.STORED_NULL, eGoods._SE_REQUIRE_HORSE)
    /// </summary>
    /// <param name="chimp">The European unit type.</param>
    /// <param name="costs">A <see cref="UnitGoodCosts"/> struct detailing the new required goods.</param>
    //[LuaApiExport("SetUnitGoodCostsEx")]
    public void SetUnitGoodCosts(eChimps chimp, UnitGoodCosts costs)
    {
        int index = (chimp - eChimps.CHIMP_TYPE_ARCHER);
        LogHelper.Debug($"Setting chimp {chimp} cost to {costs.ToString()} at 0x{new IntPtr(&_euUnitsGoodTypeCostsArray.GetBaseAddress().Pointer[index])}");
        _euUnitsGoodTypeCostsArray.SetValue(index, costs);
    }

    /// <summary>
    /// Damage a unit by a melee damage source.
    /// This function can kill units.
    /// </summary>
    /// <param name="attackerUnitId">The ID of the unit that causes damage. Can be 0.</param>
    /// <param name="victimUnitId">The ID of the unit that receives damage</param>
    /// <param name="damage">The amount of damage to inflict. Leave at 0 for default damage by <paramref name="attackerUnitId"/></param>
    /// <returns>Returns <c>true</c> on success</returns>
    [LuaApiExport("MeleeDamage")]
    public bool DamageUnitMelee(int attackerUnitId, int victimUnitId, int damage = 0)
    {
        return BulkUnitDetours.c_game_unit_takedamage_melee_hook_impl(_unitManager, attackerUnitId, victimUnitId, damage) == 1;
    }

    /// <summary>
    /// Damage a unit by a range damage source.
    /// This function can kill units.
    /// </summary>
    /// <param name="victimUnitId">The ID of the unit that receives damage</param>
    /// <param name="projectileId">The ID of the projectile that causes damage. Cannot be 0.</param>
    /// <param name="damage">The amount of damage to inflict. Leave at 0 for default damage by <paramref name="projectileId"/></param>
    /// <returns>Returns <c>true</c> on success</returns>
    [LuaApiExport("RangedDamage")]
    public bool DamageUnitRanged(int victimUnitId, int projectileId, int damage = 0)
    {
        return BulkUnitDetours.c_game_unit_takedamage_projectile_hook_impl(_unitManager, victimUnitId, projectileId, damage) == 1;
    }

    /// <summary>
    /// Influences a units health by a value.
    /// This function does not usually kill a unit by itself.
    /// </summary>
    /// <param name="unitId">The ID of the unit to damage.</param>
    /// <param name="deltaHealth">The value to inflict/heal.</param>
    [LuaApiExport("DamageEx")]
    public void DamageUnitEx(int unitId, int deltaHealth)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }

        int newHealth = (int)unit->r_CurrentHealth - deltaHealth;
        newHealth = MathUtil.Clamp(newHealth, 0, (int)unit->r_MaxHealth);

        unit->r_CurrentHealth = (UInt32)newHealth;
        UInt16 healthPercent = (UInt16)(100 * newHealth / unit->r_MaxHealth);
        unit->r_CurrentHealthPercentage = healthPercent;
        unit->r_HealthBarBlocks = (UInt32)(healthPercent / 10);
    }

    /// <summary>
    /// Damages a unit based on the game's conventional damage tables.
    /// </summary>
    /// <param name="unitId">The ID of the unit to damage.</param>
    /// <param name="attacker">The attacking unit's type.</param>
    /// <param name="defender">The defending unit's type.</param>
    /// <remarks>WARNING: This function is untested and might not account for all unit types.</remarks>
    [LuaApiExport("DamageEx2")]
    public void DamageUnitEx2(int unitId, eChimps attacker, eChimps defender)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }

        UInt32 damage = 0;
        switch (attacker)
        {
            case eChimps.CHIMP_TYPE_ARAB_SLINGER:
                damage = (UInt32)GetRangedSlingerDamageTo(defender);
                break;
            case eChimps.CHIMP_TYPE_ARCHER:
            case eChimps.CHIMP_TYPE_ARAB_BOW:
            case eChimps.CHIMP_TYPE_ARAB_HORSEMAN:
            case eChimps.CHIMP_TYPE_ARAB_GRENADIER:
                damage = (UInt32)GetRangedArrowDamageTo(defender);
                break;
            case eChimps.CHIMP_TYPE_BEDOUIN_SKIRMISHER:
                damage = (UInt32)GetRangedJavelinDamageTo(defender);
                break;
            case eChimps.CHIMP_TYPE_XBOWMAN:
            case eChimps.CHIMP_TYPE_BALLISTA:
            case eChimps.CHIMP_TYPE_ARAB_BALLISTA:
                damage = (UInt32)GetRangedBoltDamageTo(defender);
                break;
            case eChimps.CHIMP_TYPE_SPEARMAN:
            case eChimps.CHIMP_TYPE_PIKEMAN:
            case eChimps.CHIMP_TYPE_MACEMAN:
            case eChimps.CHIMP_TYPE_SWORDSMAN:
            case eChimps.CHIMP_TYPE_KNIGHT:
            case eChimps.CHIMP_TYPE_MONK:
            case eChimps.CHIMP_TYPE_LION:
            case eChimps.CHIMP_TYPE_CROCODILE:
            case eChimps.CHIMP_TYPE_HYENA:
            case eChimps.CHIMP_TYPE_LORD:
            case eChimps.CHIMP_TYPE_ARAB_ASSASIN:
            case eChimps.CHIMP_TYPE_ARAB_SWORDSMAN:
            case eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER:
            case eChimps.CHIMP_TYPE_HEALER:
            case eChimps.CHIMP_TYPE_BEDOUIN_AMBUSHER:
            case eChimps.CHIMP_TYPE_BEDOUIN_HEAVY_CAMEL:
            case eChimps.CHIMP_TYPE_BEDOUIN_SAPPER:
            case eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER:
                damage = (UInt32)MeleeDamageLookupTable.GetValue((int)attacker, (int)defender);
                break;
            case eChimps.CHIMP_TYPE_BEDOUIN_EUNUCH:
                damage = (UInt32)GetMeleeEunuchAOEDamageTo(defender);
                break;
            default:
                LogHelper.Warning($"Could not find damage (from-to) mapping for chimp: {attacker.ToString()} to chimp: {defender.ToString()}");
                break;
        }

        unit->r_CurrentHealth = Math.Max(0, unit->r_CurrentHealth - damage);
    }

    /// <summary>
    /// Gets a units health.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>The health of the unit.</returns>
    [LuaApiExport("GetCurrentHealth")]
    public int GetCurrentHealth(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return 0;
        }

        return (int)unit->r_CurrentHealth;
    }

    /// <summary>
    /// Sets a units health, respecting max hp.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="health">The health to set.</param>
    [LuaApiExport("SetCurrentHealth")]
    public void SetCurrentHealth(int unitId, int health)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }

        unit->r_CurrentHealth = (uint)Math.Min(unit->r_MaxHealth, health);
    }

    /// <summary>
    /// Gets a units max health.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>The max health of the unit.</returns>
    [LuaApiExport("GetMaxHealth")]
    public int GetMaxHealth(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return 0;
        }

        return (int)unit->r_MaxHealth;
    }

    /// <summary>
    /// Sets a units max health.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="maxHealth">The max health to set.</param>
    [LuaApiExport("SetMaxHealth")]
    public void SetMaxHealth(int unitId, int maxHealth)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }

        unit->r_MaxHealth = (uint)maxHealth;
    }

    /// <summary>
    /// Gets a units speed.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>The current speed level of the unit. Otherwise -1.</returns>
    [LuaApiExport("GetSpeed")]
    public int GetSpeed(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return 0;
        }
        return unit->r_CurrentSpeed;
    }

    /// <summary>
    /// Sets a units speed.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="speedLevel">The speed to set.</param>
    [LuaApiExport("SetSpeed")]
    public void SetSpeed(int unitId, UInt16 speedLevel)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }

        unit->r_CurrentSpeed = speedLevel;
    }

    /// <summary>
    /// Instantly kills a unit by setting its health to zero.
    /// </summary>
    /// <param name="unitId">The ID of the unit to kill.</param>
    [LuaApiExport("Kill")]
    public void KillUnit(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }

        unit->r_CurrentHealth = 1;
        DamageUnitMelee(0, unitId, Int16.MaxValue);
    }

    /// <summary>
    /// Orders a unit to move to a specific tile coordinate.
    /// </summary>
    /// <param name="unitId">The ID of the unit to command.</param>
    /// <param name="tileX">The destination tile X-coordinate.</param>
    /// <param name="tileY">The destination tile Y-coordinate.</param>
    /// <param name="unknown">An unknown parameter passed to the game's internal function (default is 0).</param>
    [LuaApiExport("MoveTo")]
    public void MoveToTile(int unitId, int tileX, int tileY, int unknown = 0)
    {
        BulkUnitDetours.c_game_unit_issueorder_movehere_hook_impl(_unitManager, unitId, tileX, tileY, unknown);
    }

    /// <summary>
    /// Checks if a unit is strictly within the camera view.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    [LuaApiExport("IsRendered")]
    public bool IsRendered(int unitId)
    {
        Vector3 pos = GetCurrentUnityPosition(unitId);
        if (pos == default)
            return false;

        // Convert World Point to Viewport (0,0 is bottom-left, 1,1 is top-right)
        UnityEngine.Vector3 viewportPos = UnityEngine.Camera.main.WorldToViewportPoint(pos.ToUnityVector3Safe());

        // Check if inside screen bounds with a small margin for sprite width
        bool isOnScreen = (viewportPos.x > -0.1f && viewportPos.x < 1.1f &&
                           viewportPos.y > -0.1f && viewportPos.y < 1.1f &&
                           viewportPos.z > 0); // z>0 means in front of camera

        return isOnScreen;
    }

    /// <summary>
    /// Gets the current unity-side position of a unit
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>An <see cref="Vector3"/> representing the current unity coordinates, or a default vector if not found.</returns>
    [LuaApiExport("GetUnityPosition")]
    public Vector3 GetCurrentUnityPosition(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return default;
        }

        if (!GameMap.instance.chimps.TryGetValue(unitId, out Chimp chimp))
        {
            LogHelper.Debug($"Could not find unity-side unit by id: {unitId}");
            return default;
        }

        UnityEngine.Vector3 ueVec3 = chimp.position;
        return *(System.Numerics.Vector3*)&ueVec3;
    }

    /// <summary>
    /// Gets the current local tile position of the unit.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> representing the current local tile coordinates, or a default vector if not found.</returns>
    /// <remarks>Local coordinates are the coarse-grained grid positions (used by the map grid system for Buildings, etc)</remarks>
    [LuaApiExport("GetLocalPosition")]
    public UnmanagedVector2<UInt16> GetCurrentLocalTilePosition(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return default;
        }
        return *(UnmanagedVector2<UInt16>*)(&unit->r_CurrentTilePositionX);
    }

    /// <summary>
    /// Gets the current world tile position of the unit.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>An <see cref="UnmanagedVector2{UInt16}"/> representing the current world tile coordinates, or a default vector if not found.</returns>
    /// <remarks>World coordinates are the fine-grained positions used by the game engine, where 1 local tile equals 8 world tiles.</remarks>
    [LuaApiExport("GetWorldPosition")]
    public UnmanagedVector2<UInt16> GetCurrentWorldTilePosition(int unitId)
    {
        return GetCurrentLocalTilePosition(unitId).ToWorldPosition();
    }

    /// <summary>
    /// Sets the current local tile position of a unit, effectively teleporting it.
    /// </summary>
    /// <param name="unitId">The ID of the unit to move.</param>
    /// <param name="localTilePosition">The destination local tile coordinates.</param>
    /// <remarks>
    /// WARNING: This function directly manipulates the unit's position data. It can cause visual glitches,
    /// such as the unit briefly turning invisible before reappearing, as the game's rendering and logic
    /// systems catch up to the sudden change.
    /// </remarks>
    [LuaApiExport("SetLocalPosition")]
    public void SetCurrentLocalTilePosition(int unitId, UnmanagedVector2<UInt16> localTilePosition)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }
        UInt32 localTilePositionId = (UInt32)GameTileManagerAPI.Instance.GetTileId(localTilePosition.X, localTilePosition.Y);

        // We need to do this as well because else it -might- look glitchy due to interpolation
        *(UnmanagedVector2<UInt16>*)(&unit->r_TargetTilePositionX) = localTilePosition;
        *(UnmanagedVector2<UInt16>*)(&unit->r_TargetTilePositionX2) = localTilePosition;
        *(UnmanagedVector2<UInt16>*)(&unit->r_NextTilePositionX2) = localTilePosition;
        *(UnmanagedVector2<UInt16>*)(&unit->r_PreviousTilePositionX) = localTilePosition;
        unit->r_TargetPositionTileId = localTilePositionId;
        unit->r_NextPositionTileId2 = localTilePositionId;
        unit->r_PreviousPositionTileId = localTilePositionId;

        // Core position
        *(UnmanagedVector2<UInt16>*)(&unit->r_CurrentTilePositionX) = localTilePosition;
        *(UnmanagedVector2<UInt16>*)(&unit->r_CurrentWorldPositionX) = localTilePosition.ToWorldPosition();
        unit->r_CurrentPositionTileId = localTilePositionId;
    }

    /// <summary>
    /// Sets the current world tile position of a unit, effectively teleporting it.
    /// </summary>
    /// <param name="unitId">The ID of the unit to move.</param>
    /// <param name="worldTilePosition">The destination world tile coordinates.</param>
    /// <remarks>
    /// This is a convenience wrapper that converts world coordinates to local coordinates before calling
    /// <see cref="SetCurrentLocalTilePosition(int, UnmanagedVector2{UInt16})"/>.
    /// It is subject to the same potential visual glitches.
    /// </remarks>
    [LuaApiExport("SetWorldPosition")]
    public void SetCurrentWorldTilePosition(int unitId, UnmanagedVector2<UInt16> worldTilePosition)
    {
        SetCurrentLocalTilePosition(unitId, worldTilePosition.ToLocalPosition());
    }

    /// <summary>
    /// Checks if a siege engien unit is manned by at least one engineer.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns><c>true</c> if the unit is manned; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsSiegeEngineManned")]
    public bool IsSiegeEngineManned(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return false;
        }
        if (unit->r_AssignedEngineer1 != 0)
            return true;
        if (unit->r_AssignedEngineer2 != 0)
            return true;
        if (unit->r_AssignedEngineer3 != 0)
            return true;
        if (unit->r_AssignedEngineer4 != 0)
            return true;
        return false;
    }

    /// <summary>
    /// Checks if a unit is currently invisible.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns><c>true</c> if the unit is invisible; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsInvisible")]
    public bool GetIsInvisible(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return false;
        }
        return (unit->r_IsInvisible) != 0;
    }

    /// <summary>
    /// Sets the invisibility state of a unit.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="isInvisible">The new invisibility state to set. <c>true</c> for invisible, <c>false</c> for visible.</param>
    [LuaApiExport("SetInvisible")]
    public void SetIsInvisible(int unitId, bool isInvisible)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }
        unit->r_IsInvisible = (byte)(isInvisible ? 1 : 0);
    }

    /// <summary>
    /// Check if a unit is supposed to be hidden visually (unity-side)
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>True if this unit is visually hidden; Otherwise false.</returns>
    [LuaApiExport("GetVisualHidden")]
    public bool IsVisualHidden(int unitId)
    {
        if (!Instance._visualHiddenUnitIds.Contains(unitId))
        {
            return false;
        }
        return true;
    }

    /// <summary>
    /// Sets if a unit is supposed to be hidden visually (unity-side)
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="hidden">Hidden state</param>
    [LuaApiExport("SetVisualHidden")]
    public void SetVisualHidden(int unitId, bool hidden)
    {
        if (hidden)
            Instance._visualHiddenUnitIds.Add(unitId);
        else
            Instance._visualHiddenUnitIds.Remove(unitId);

        // Set it for currently rendered chimps.
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (GameMap.instance.chimps.TryGetValue(unitId, out Chimp chimp) && chimp.objectID == unitId)
            {
                chimp.sprRenderer.enabled = !hidden;
            }
        });
    }

    /// <summary>
    /// Gets the local scale of a unit's sprite.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>The sprite's current scale, or <c>Vector3.One</c> if not found.</returns>
    [LuaApiExport("GetSpriteScale")]
    public Vector3 GetSpriteScale(int unitId)
    {
        if (!Instance._unitSpriteScaleOverrides.TryGetValue(unitId, out Vector3 scale))
        {
            return Vector3.One;
        }
        return scale;
    }

    /// <summary>
    /// Sets the local scale of a unit's sprite.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="scale">The new scale to apply.</param>
    [LuaApiExport("SetSpriteScale")]
    public void SetSpriteScale(int unitId, Vector3 scale)
    {
        Instance._unitSpriteScaleOverrides[unitId] = scale;

        // Set it for all currently rendered chimps.
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (GameMap.instance.chimps.TryGetValue(unitId, out Chimp chimp) && chimp.objectID == unitId)
            {
                chimp.sprRenderer.transform.localScale = scale.ToUnityVector3Safe();
            }
        });
    }

    /// <summary>
    /// Resets the local scale of a unit's sprite.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="scale">The new scale to apply. (Required for on-screen changes)</param>
    [LuaApiExport("ResetSpriteScale")]
    public void ResetSpriteScale(int unitId, Vector3 scale)
    {
        Instance._unitSpriteScaleOverrides.Remove(unitId);

        // Set it for all currently rendered chimps.
        if (GameMap.instance.chimps.TryGetValue(unitId, out Chimp chimp))
        {
            chimp.sprRenderer.transform.localScale = scale.ToUnityVector3Safe();
        }
    }

    /// <summary>
    /// Gets the game mateterial of a unit.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>The the units GameMaterial.</returns>
    [LuaApiExport("GetGameMaterial")]
    public GM GetGameMaterial(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return (GM)0;
        }
        return unit->r_GameMaterialIndex;
    }

    /// <summary>
    /// Sets the game material of a unit.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="material">The new GameMaterial.</param>
    [LuaApiExport("SetGameMaterial")]
    public void SetGameMaterial(int unitId, GM material)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }
        unit->r_GameMaterialIndex = material;
    }

    /// <summary>
    /// Gets all avilable chimp types
    /// </summary>
    /// <returns>All available chimp types as an array.</returns>
    [LuaApiExport("GetAllChimpTypes")]
    public eChimps[] GetAllChimpTypes()
    {
        List<eChimps> types = new List<eChimps>();
        foreach (eChimps v in Enum.GetValues(typeof(eChimps)))
        {
            types.Add(v);
        }
        return [.. types];
    }

    /// <summary>
    /// Retrieve the current shield health (demolisher-exclusive)
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>The shield current health on success; Otherwise -1</returns>
    [LuaApiExport("GetShieldCurrentHealth")]
    public int GetShieldCurrentHealth(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return -1;
        }
        return unit->r_DemolisherShieldHealth;
    }

    /// <summary>
    /// Set the shield current health (demolisher-exclusive)
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="health">The new shield current health.</param>
    [LuaApiExport("SetShieldCurrentHealth")]
    public void SetShieldHealth(int unitId, UInt16 health)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }
        unit->r_DemolisherShieldHealth = health;
    }

    /// <summary>
    /// Get the default shield current health (demolisher-exclusive)
    /// </summary>
    /// <returns>The current default shield health, or 0 if the underlying component is null</returns>
    [LuaApiExport("GetDefaultShieldHealth")]
    public UInt16 GetDefaultShieldHealth() => GameGlobalsManager.Instance.BedouinDemolisherShieldHealth?.GetValue() ?? 0;

    /// <summary>
    /// Set the default shield current health (demolisher-exclusive)
    /// </summary>
    /// <param name="health">The new shield default health.</param>
    [LuaApiExport("SetDefaultShieldHealth")]
    public void SetDefaultShieldHealth(UInt16 health) => GameGlobalsManager.Instance.BedouinDemolisherShieldHealth?.SetValue(health);

    /// <summary>
    /// Gets the base damage a melee unit inflicts upon a target unit type.
    /// </summary>
    /// <param name="source">The attacking melee unit type (<see cref="eChimps"/>).</param>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the melee damage table.</returns>
    [LuaApiExport("GetMeleeDamageFromTo")]
    public int GetMeleeDamageFromTo(eChimps source, eChimps target) => MeleeDamageLookupTable.GetValue((int)source, (int)target);

    /// <summary>
    /// Sets the base damage a melee unit inflicts upon a target unit type.
    /// </summary>
    /// <param name="source">The attacking melee unit type (<see cref="eChimps"/>).</param>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the melee damage table.</param>
    [LuaApiExport("SetMeleeDamageFromTo")]
    public void SetMeleeDamageFromTo(eChimps source, eChimps target, int value) => MeleeDamageLookupTable.SetValue((int)source, (int)target, value);

    /// <summary>
    /// Gets the base damage from a Eunuch's area-of-effect attack upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the Eunuch AOE damage table.</returns>
    [LuaApiExport("GetMeleeEunuchAOEDamageTo")]
    public int GetMeleeEunuchAOEDamageTo(eChimps target) => _meleeEunuchAOEDamageArray.GetValue((int)target);

    /// <summary>
    /// Sets the base damage from a Eunuch's area-of-effect attack upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the Eunuch AOE damage table.</param>
    [LuaApiExport("SetMeleeEunuchAOEDamageTo")]
    public void SetMeleeEunuchAOEDamageTo(eChimps target, int value) => _meleeEunuchAOEDamageArray.SetValue((int)target, value);

    /// <summary>
    /// Gets the base damage from an arrow projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the arrow damage table.</returns>
    [LuaApiExport("GetRangedArrowDamageTo")]
    public int GetRangedArrowDamageTo(eChimps target) => _rangedArrowDamageArray.GetValue((int)target);

    /// <summary>
    /// Sets the base damage from an arrow projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the arrow damage table.</param>
    [LuaApiExport("SetRangedArrowDamageTo")]
    public void SetRangedArrowDamageTo(eChimps target, int value) => _rangedArrowDamageArray.SetValue((int)target, value);

    /// <summary>
    /// Gets the base damage from a bolt projectile (e.g., from a Crossbowman or Ballista) upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the bolt damage table.</returns>
    [LuaApiExport("GetRangedBoltDamageTo")]
    public int GetRangedBoltDamageTo(eChimps target) => _rangedBoltDamageArray.GetValue((int)target);

    /// <summary>
    /// Sets the base damage from a bolt projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the bolt damage table.</param>
    [LuaApiExport("SetRangedBoltDamageTo")]
    public void SetRangedBoltDamageTo(eChimps target, int value) => _rangedBoltDamageArray.SetValue((int)target, value);

    /// <summary>
    /// Gets the base damage from a slinger's stone projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the slinger damage table.</returns>
    [LuaApiExport("GetRangedSlingerDamageTo")]
    public int GetRangedSlingerDamageTo(eChimps target) => _rangedSlingerDamageArray.GetValue((int)target);

    /// <summary>
    /// Sets the base damage from a slinger's stone projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the slinger damage table.</param>
    [LuaApiExport("SetRangedSlingerDamageTo")]
    public void SetRangedSlingerDamageTo(eChimps target, int value) => _rangedSlingerDamageArray.SetValue((int)target, value);

    /// <summary>
    /// Gets the base damage from a javelin projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <returns>The base damage value from the javelin damage table.</returns>
    [LuaApiExport("GetRangedJavelinDamageTo")]
    public int GetRangedJavelinDamageTo(eChimps target) => _rangedJavelinDamageArray.GetValue((int)target);

    /// <summary>
    /// Sets the base damage from a javelin projectile upon a target unit type.
    /// </summary>
    /// <param name="target">The defending unit type (<see cref="eChimps"/>).</param>
    /// <param name="value">The new base damage value to set in the javelin damage table.</param>
    [LuaApiExport("SetRangedJavelinDamageTo")]
    public void SetRangedJavelinDamageTo(eChimps target, int value) => _rangedJavelinDamageArray.SetValue((int)target, value);

    /// <summary>
    /// Sets the selectability of a unit id.
    /// Use this to make units unselectable.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <param name="selectable">Enable or disable selectability.</param>
    [LuaApiExport("SetSelectable")]
    public void SetSelectable(int unitId, bool selectable)
    {
        if (selectable)
        {
            _unselectableUnitIds.Remove(unitId);
            return;
        }
        _unselectableUnitIds.Add(unitId);
    }

    /// <summary>
    /// Gets the selectability of a unit id.
    /// </summary>
    /// <param name="unitId">The ID of the unit.</param>
    /// <returns>Selects if the unit is selectable.</returns>
    [LuaApiExport("IsSelectable")]
    public bool IsSelectable(int unitId)
    {
        return !_unselectableUnitIds.Contains(unitId);
    }

    /// <summary>
    /// Iterates through an array of unit IDs and sets the ID to 0 if it is in the unselectable list.
    /// </summary>
    /// <param name="pChimps">A pointer to the array of unit IDs.</param>
    /// <param name="count">The number of elements in the array.</param>
    internal static void FilterUnselectableUnits(UInt64 pChimps, UInt32 count)
    {
        // Early exit if there are no units to filter
        if (Instance._unselectableUnitIds.Count == 0)
            return;

        if (pChimps == 0 || count == 0)
        {
            return;
        }

        Span<uint> chimpsSpan = new Span<uint>((void*)pChimps, (int)count);
        for (int i = 0; i < count; i++)
        {
            if (Instance._unselectableUnitIds.Contains((int)chimpsSpan[i]))
            {
                chimpsSpan[i] = 0;
            }
        }
    }

    /// <summary>
    /// Gets the unit fire damage.
    /// 100 = Default for almost all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <returns>Fire damage to unit</returns>
    [LuaApiExport("GetFireDamage")]
    public int GetFireDamage(eChimps unit) => GetFireDamageInternal(unit);

    /// <summary>
    /// Sets the unit fire damage.
    /// 100 = Default for almost all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <param name="damage">The new received fire damage</param>
    [LuaApiExport("SetFireDamage")]
    public void SetFireDamage(eChimps unit, int damage) => SetFireDamageInternal(unit, damage);

    /// <summary>
    /// Gets the unit heal by a bedouin healer
    /// 10 = Default for all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <returns>Bedouin heal to unit</returns>
    [LuaApiExport("GetBedouinHeal")]
    public int GetBedouinHeal(eChimps unit) => GetBedouinHealInternal(unit);

    /// <summary>
    /// Sets the unit heal by a bedouin healer.
    /// 10 = Default for all units.
    /// </summary>
    /// <param name="unit">The unit type to query for</param>
    /// <param name="heal">The new heal amount</param>
    [LuaApiExport("SetBedouinHeal")]
    public void SetBedouinHeal(eChimps unit, int heal) => SetBedouinHealInternal(unit, heal);

    /// <summary>
    /// Returns the attacking unit id (context-dependent)
    /// Formerly GetCurrentContextAttackingUnitId
    /// </summary>
    [LuaApiExport("GetCurrentContextUnitId")]
    public UInt16 GetCurrentContextUnitId()
    {
        UInt64 addr = GameGlobalsManager.Instance.CurrentContextUnitIdVA;
        if (addr == 0)
        {
            LogHelper.Debug($"Current Context: UnitIdVA is null!");
            return 0;
        }

        return *(UInt16*)(addr);
    }

    /// <summary>
    /// Registers AI State tracking for a specific type of unit
    /// </summary>
    /// <param name="unit">The unit.</param>
    [LuaApiExport("RegisterAIStateTracker")]
    public void RegisterAIStateTracker(eChimps unit)
    {
        if (!_registeredAIStateTrackers.Contains(unit))
        {
            _registeredAIStateTrackers.Add(unit);
        }
    }

    /// <summary>
    /// Registers AI State tracking for a specific type of unit
    /// </summary>
    /// <param name="unit">The unit.</param>
    [LuaApiExport("IsAIStateTracked")]
    public bool IsAIStateTracked(eChimps unit)
    {
        return _registeredAIStateTrackers.Contains(unit);
    }

    /// <summary>
    /// Gets the current facing direction of a unit.
    /// </summary>
    /// <param name="unitId">The unit id.</param>
    /// <returns>The current unit facing direction; Otherwise Invalid.</returns>
    [LuaApiExport("GetDirection")]
    public Dircs GetDirection(int unitId)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return Dircs.Invalid;
        }
        return unit->r_Direction;
    }

    /// <summary>
    /// Sets the current facing direction of a unit.
    /// </summary>
    /// <param name="unitId">The unit id.</param>
    /// <param name="dir">The new facing direction.</param>
    [LuaApiExport("SetDirection")]
    public void SetDirection(int unitId, Dircs dir)
    {
        if (!TryGetUnitById(unitId, out GameUnit* unit))
        {
            LogHelper.Warning($"Could not find unit by id: {unitId}");
            return;
        }
        unit->r_Direction = dir;
    }

    #region Query System

    /// <summary>
    /// Begins a high-performance query over all possible unit slots.
    /// </summary>
    public GameStructQuery<GameUnit> QueryUnits()
    {
        return new GameStructQuery<GameUnit>(_unitArray._array, _unitArray.Length);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    /// <param name="results">The list to save the found results in.</param>
    /// <param name="basePredicate">The search query predicate</param>
    /// <param name="stateFilter">The unit state to look out for <see cref="AliveState"/></param>
    /// <param name="unitType">The unit type to look out for <see cref="eChimps"/></param>
    /// <param name="relationship">The to-player relationshiop to look out for <see cref="PlayerRelationship"/></param>
    /// <param name="povPlayerId">The context-relevant playerId. Only used if relationship is not null.</param>
    public void ExecuteQuery(List<int> results, RefPredicate<GameUnit> basePredicate, AliveState? stateFilter, eChimps? unitType, PlayerRelationship? relationship, int? povPlayerId)
    {
        GameStructQuery<GameUnit> query = QueryUnits().Where(basePredicate);

        if (stateFilter.HasValue)
        {
            AliveState state = stateFilter.Value;
            query = query.Where((in unit) => unit.r_AliveState == state);
        }

        if (unitType.HasValue)
        {
            eChimps chimpType = unitType.Value;
            query = query.Where(UnitPredicates.IsOfType(chimpType));
        }

        if (relationship != PlayerRelationship.Any && povPlayerId.HasValue)
        {
            switch (relationship)
            {
                case PlayerRelationship.Allied:
                    query = query.Where(UnitPredicates.IsAlliedTo(povPlayerId.Value));
                    break;
                case PlayerRelationship.Enemy:
                    query = query.Where(UnitPredicates.IsEnemyTo(povPlayerId.Value));
                    break;
                case PlayerRelationship.Self:
                    query = query.Where(UnitPredicates.IsOwnedBy(povPlayerId.Value));
                    break;
            }
        }

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for convenience.
    /// </summary>
    public static class UnitPredicates
    {
        public static readonly RefPredicate<GameUnit> IsAlive = static (in unit) => unit.r_AliveState == AliveState.IsAlive;
        public static readonly RefPredicate<GameUnit> IsDead = static (in unit) => unit.r_AliveState == AliveState.MarkedForDeletion;

        // Predicate to match any unit, used as a default for generic queries.
        public static readonly RefPredicate<GameUnit> Any = static (in unit) => true;

        public static RefPredicate<GameUnit> IsOwnedBy(int playerOwnerId) => (in unit) => unit.r_ControllableForPlayerId == playerOwnerId;
        public static RefPredicate<GameUnit> IsAlliedTo(int playerId) => (in unit) => GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(unit.r_ControllableForPlayerId, playerId);
        public static RefPredicate<GameUnit> IsEnemyTo(int playerId) => (in unit) => !GamePlayerManagerAPI.Instance.IsPlayerAlliedTo(unit.r_ControllableForPlayerId, playerId);
        public static RefPredicate<GameUnit> IsOfType(eChimps chimpType) => (in unit) => unit.r_UnitChimp == chimpType;
        public static RefPredicate<GameUnit> IsOfAnyType(params eChimps[] types)
        {
            HashSet<eChimps> typeSet = new HashSet<eChimps>(types);
            return (in GameUnit u) => typeSet.Contains(u.r_UnitChimp);
        }
        public static RefPredicate<GameUnit> IsOfAnyType(HashSet<eChimps> types) => (in GameUnit u) => types.Contains(u.r_UnitChimp);

        public static RefPredicate<GameUnit> HasGlobalId(int globalId) => (in unit) => unit.r_GlobalId == globalId;

        /// <summary>
        /// Matches units whose current tile falls within the specified rectangle.
        /// Reads <see cref="GameUnit.r_CurrentTilePositionX"/> and
        /// <see cref="GameUnit.r_CurrentTilePositionY"/> directly via the <c>in</c>
        /// parameter instead of calling <c>CurrentTilePosition()</c>, which uses a
        /// <c>fixed</c> statement that pins a stack copy rather than the native array
        /// element when the struct is on the unmanaged heap.
        /// </summary>
        public static RefPredicate<GameUnit> IsWithinRect(int x, int y, int width, int height) =>
            (in GameUnit u) =>
                u.r_CurrentTilePositionX >= x && u.r_CurrentTilePositionX < x + width &&
                u.r_CurrentTilePositionY >= y && u.r_CurrentTilePositionY < y + height;

        /// <summary>
        /// Matches units whose current tile falls within the specified sphere (circle).
        /// Uses a pre-computed squared radius to avoid <c>Math.Sqrt</c> per entry.
        /// Same rationale as <see cref="IsWithinRect"/> for avoiding <c>CurrentTilePosition()</c>.
        /// </summary>
        public static RefPredicate<GameUnit> IsWithinSphere(int cx, int cy, int radius)
        {
            int rSq = radius * radius;
            return (in GameUnit u) =>
            {
                int dx = u.r_CurrentTilePositionX - cx;
                int dy = u.r_CurrentTilePositionY - cy;
                return dx * dx + dy * dy <= rSq;
            };
        }

        /// <summary>
        /// Mathces units that are currently within a Building's Occupied Tile Array.
        /// </summary>
        /// <param name="otaPtr">The occupied tile array pointer</param>
        /// <param name="gridSize">The grid size of the building</param>
        public static RefPredicate<GameUnit> IsWithinOTA(UInt32* otaPtr, int gridSize)
        {
            // Build the set eagerly at predicate construction time (not per-unit)
            // The pointer is only dereferenced here, not captured into the closure.
            HashSet<uint> otaSet = new HashSet<uint>(gridSize);
            for (int i = 0; i < gridSize; i++)
                otaSet.Add(otaPtr[i]);

            return (in GameUnit u) => otaSet.Contains(u.r_CurrentPositionTileId);
        }
    }

    /// <summary>
    /// Fills a list with IDs of all units, with optional filters (no spatial constraint).
    /// </summary>
    public void GetAllUnits(List<int> results, AliveState? stateFilter = null, eChimps? unitType = null, PlayerRelationship? relationship = PlayerRelationship.Any, int? povPlayerId = 1)
    {
        ExecuteQuery(results, UnitPredicates.Any, stateFilter, unitType, relationship, povPlayerId);
    }

    #endregion

}