using CrusaderDE;
using R3;
using RedBird.Core.Memory;
using RedBird.Core.Memory.Managed;
using Serilog;
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
using System.Linq;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with game players and their associated resources.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as the primary entry point for managing player-specific data,
/// such as gold, popularity, teams, and resource management. It also provides access to map-wide rules
/// like available buildings, units, and trade goods.
/// </remarks>
[LuaApiNamespace("Player")]
public unsafe sealed class GamePlayerManagerAPI
{
    private static readonly Lazy<GamePlayerManagerAPI> _lazy = new(() => new GamePlayerManagerAPI());
    public static GamePlayerManagerAPI Instance => _lazy.Value;

    private IntPtr _playerManager;
    private SimpleNativeArray<GamePlayerResources> _playerResources;
    private SimpleNativeArray<UInt32> _defaultSkirmishGold;
    private SimpleNativeArray<UInt32> _defaultSkirmishResources;
    private UInt32* _AIVCastleLayoutTable;

    private SimpleNativeArray<UInt32> _peasantSpawnRateIncrementsHighPop;
    private SimpleNativeArray<UInt32> _peasantSpawnRateIncrementsLowPop;
    private SimpleNativeArray<UInt32> _peasantSpawnRateIncrementsDefault;

    private UInt32* _localPlayerId = null;
    private UInt32* _selectedBuildingId = null;
    private Int32* _aiBoolPlayerList = null;
    private Int32* _aiLineUp = null;
    private Int32* _teamList = null;
    private Int32* _localPause = null;
    private IntPtr _currentMapName;
    private Int32* _currentMapNameLength;
    private Int32* _isInMapEditor = null;
    private Int32* _isLocalPlayerKeepEnclosed = null;
    private Int32* _isLocalPlayerExtremePowersEnabled = null;
    private Int32* _isMonkAvailable = null;
    private Int32* _isEngineerAvailable = null;
    private Int32* _isLaddermanAvailable = null;
    private bool _isTunnelerAvailable = true;
    private IntPtr _choreManager;
    public Int32* _noKnockdownWalls = null;
    public Int32* _globalImprovedSiegeBehaviour = null;
    public Int32* _globalMoreAggressiveSiegeBehaviour = null;
    public ChoreManagerOptionsInternal _choreManagerOptionsInternal;
    public eSkirmishGameMode* _currentSkirmishGameMode = null;
    public SkirmishMode* _currentSkirmishMode = null;
    public eGameTypeModes* _currentGameTypeMode = null;
    public Int32* _currentTrailType = null;
    public Int32* _coopTrailId = null;
    public Int32* _coopMissionId = null;
    public AIAdvantage* _aiAdvantage = null;

    private GameMapRulesInfo* _mapRulesManager = null;
    private SimpleNativeArray<UInt32> _allowedTradingGoods;

    internal GameCursorManager* CursorManager = null;

    private const int PLAYER_RESOURCES_OFFSET = 0x1343FC;
    // playerResources array begins at: _playerManager + PLAYER_RESOURCES_OFFSET

    private const int PLAYER_RESOURCES_SIZE = 0x583C;

    // the very start of the eChimps:count mapped array
    private const int DEFAULT_SKIRMISH_UNITS_OFFSET = 0x220A;

    private const int PLAYER_ALLOWED_FOOD_OFFSET = 0x2280;

    private const int BASE_PRICE_TABLE_OFFSET = 0x1817B8;

    /// <summary>
    /// Cached goods enum amount
    /// </summary>
    private static readonly int GoodsEnumCount = Enum.GetValues(typeof(eGoods)).Length;

    /// <summary>The maximum number of players supported by the game engine.</summary>
    public const int MAX_PLAYERS = 8;

    private int _initialized = 0;

    /// <summary>
    /// The base food consumption rate per peasant
    /// Default: 3
    /// </summary>
    [LuaApiExport("FoodConsumptionRate")]
    public ManagedValue<int> FoodConsumptionRate { get; }

    internal const int DEFAULT_FOOD_CONSUMPTION_RATE = 3;

    /// <summary>
    /// Initializes a new instance of the <see cref="GamePlayerManagerAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private GamePlayerManagerAPI()
    {
        _playerManager = (IntPtr)(GameGlobalsManager.Instance.GamePlayerManagerVA);
        _playerResources = new SimpleNativeArray<GamePlayerResources>((byte*)(_playerManager + PLAYER_RESOURCES_OFFSET), MAX_PLAYERS);
        _localPlayerId = (UInt32*)(GameGlobalsManager.Instance.LocalPlayerIdVA);
        _defaultSkirmishGold = new SimpleNativeArray<UInt32>((byte*)(GameGlobalsManager.Instance.PlayerDefaultSkirmishSpawnGoldTable + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle), 3);
        _defaultSkirmishResources = new SimpleNativeArray<UInt32>((byte*)(GameGlobalsManager.Instance.PlayerDefaultSkirmishResourcesVA), GoodsEnumCount);
        _AIVCastleLayoutTable = (UInt32*)(GameGlobalsManager.Instance.AIVCastleLayoutTableRVA);

        _selectedBuildingId = (UInt32*)GameGlobalsManager.Instance.CurrentlySelectedBuildingIdVA;

        _peasantSpawnRateIncrementsHighPop = new SimpleNativeArray<UInt32>((byte*)GameGlobalsManager.Instance.PeasantSpawnRateIncrementsHighPopRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle, 24);
        _peasantSpawnRateIncrementsLowPop = new SimpleNativeArray<UInt32>((byte*)GameGlobalsManager.Instance.PeasantSpawnRateIncrementsLowPopRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle, 24);
        _peasantSpawnRateIncrementsDefault = new SimpleNativeArray<UInt32>((byte*)GameGlobalsManager.Instance.PeasantSpawnRateIncrementsDefaultsRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle, 24);

        _aiBoolPlayerList = (Int32*)(GameGlobalsManager.Instance.AIBoolPlayerListVA);
        _aiLineUp = (Int32*)(GameGlobalsManager.Instance.AILineUpVA);
        _teamList = (Int32*)((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + (UInt64)GameGlobalsManager.Instance.TeamsListRVA);
        _localPause = (Int32*)(GameGlobalsManager.Instance.GamePausedVA);
        _currentMapName = (IntPtr)(GameGlobalsManager.Instance.CurrentMapNameVA);
        _currentMapNameLength = (Int32*)(GameGlobalsManager.Instance.CurrentMapNameLengthVA);
        _isInMapEditor = (Int32*)(GameGlobalsManager.Instance.IsInMapEditorVA);
        _isLocalPlayerKeepEnclosed = (Int32*)(GameGlobalsManager.Instance.PlayerKeepIsEnclosedVA);
        _isLocalPlayerExtremePowersEnabled = (Int32*)(GameGlobalsManager.Instance.PlayerExtremePowersEnabledVA);
        _isMonkAvailable = (Int32*)(GameGlobalsManager.Instance.MonkAvailableVA);
        _isEngineerAvailable = (Int32*)(GameGlobalsManager.Instance.EngineerAvailableVA);
        _isLaddermanAvailable = (Int32*)(GameGlobalsManager.Instance.LaddermanAvailableVA);
        _currentSkirmishGameMode = (eSkirmishGameMode*)(GameGlobalsManager.Instance.CurrentSkirmishGameModeVA);
        _currentTrailType = (Int32*)(GameGlobalsManager.Instance.CurrentTrailTypeVA);
        _currentSkirmishMode = (SkirmishMode*)(GameGlobalsManager.Instance.CurrentGameSkirmishModeVA);
        _currentGameTypeMode = (eGameTypeModes*)(GameGlobalsManager.Instance.CurrentGameTypeModeVA);
        _aiAdvantage = (AIAdvantage*)(GameGlobalsManager.Instance.AIAdvantageVA);

        _mapRulesManager = (GameMapRulesInfo*)(GameGlobalsManager.Instance.BuildingAvailabilityManager);
        _allowedTradingGoods = new SimpleNativeArray<UInt32>((byte*)GameGlobalsManager.Instance.MapRulesInfo_AllowedTradingGoodsRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle, GoodsEnumCount);

        CursorManager = (GameCursorManager*)(GameGlobalsManager.Instance.GameCursorManagerVA);
        _choreManager = (IntPtr)(GameGlobalsManager.Instance.ChoreManagerVA);
        _noKnockdownWalls = (Int32*)(GameGlobalsManager.Instance.NoKnockdownWallsVA);
        _globalImprovedSiegeBehaviour = (Int32*)(GameGlobalsManager.Instance.GlobalImprovedSiegingBehaviourVA);
        _globalMoreAggressiveSiegeBehaviour = (Int32*)(GameGlobalsManager.Instance.GlobalMoreAggressiveAISiegingBehaviourVA);
        _choreManagerOptionsInternal = new ChoreManagerOptionsInternal((void*)GameGlobalsManager.Instance.ChoreManagerVA);

        FoodConsumptionRate = new ManagedValue<int>(DEFAULT_FOOD_CONSUMPTION_RATE);

        LogHelper.Information($"_playerManager: {_playerManager.ToString("X16")}");
        LogHelper.Information($"_playerResources: {new IntPtr(_playerResources._array).ToString("X16")}");
        LogHelper.Information($"_defaultSkirmishGold: {new IntPtr(_defaultSkirmishGold._array).ToString("X16")}");
        LogHelper.Information($"_defaultSkirmishResources: {new IntPtr(_defaultSkirmishResources._array).ToString("X16")}");
        LogHelper.Information($"_defaultSkirmishSettingsTable: {new IntPtr(_AIVCastleLayoutTable).ToString("X16")}");
        LogHelper.Information($"_peasantSpawnRateIncrementsHighPop: {new IntPtr(_peasantSpawnRateIncrementsHighPop._array).ToString("X16")}");
        LogHelper.Information($"_peasantSpawnRateIncrementsLowPop: {new IntPtr(_peasantSpawnRateIncrementsLowPop._array).ToString("X16")}");
        LogHelper.Information($"_peasantSpawnRateIncrementsDefault: {new IntPtr(_peasantSpawnRateIncrementsDefault._array).ToString("X16")}");
        LogHelper.Information($"CursorManager: {new IntPtr(CursorManager).ToString("X16")}");
        LogHelper.Information($"_aiBoolPlayerList: {new IntPtr(_aiBoolPlayerList).ToString("X16")}");
        LogHelper.Information($"_aiLineUp: {new IntPtr(_aiLineUp).ToString("X16")}");
        LogHelper.Information($"_teamList: {new IntPtr(_teamList).ToString("X16")}");
        LogHelper.Information($"_localPause: {new IntPtr(_localPause).ToString("X16")}");
        LogHelper.Information($"_currentMapName: {_currentMapName.ToString("X16")}");
        LogHelper.Information($"_currentMapNameLength: {new IntPtr(_currentMapNameLength).ToString("X16")}");
        LogHelper.Information($"_isInMapEditor: {new IntPtr(_isInMapEditor).ToString("X16")}");
        LogHelper.Information($"_mapRulesManager: {GameGlobalsManager.Instance.BuildingAvailabilityManager.ToString("X16")}");
        LogHelper.Information($"_allowedTradingGoods: {new IntPtr(_allowedTradingGoods._array).ToString("X16")}");
        LogHelper.Information($"_cursorManager: {GameGlobalsManager.Instance.GameCursorManagerVA.ToString("X16")}");
        LogHelper.Information($"_noKnockdownWalls: {new IntPtr(_noKnockdownWalls).ToString("X16")}");
        LogHelper.Information($"_globalImprovedSiegeBehaviour: {new IntPtr(_globalImprovedSiegeBehaviour).ToString("X16")}");
        LogHelper.Information($"_globalMoreAggressiveSiegeBehaviour: {new IntPtr(_globalMoreAggressiveSiegeBehaviour).ToString("X16")}");
        LogHelper.Information($"_choreManagerOptionsInternal: {new IntPtr(_choreManagerOptionsInternal._base).ToString("X16")}");

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
    private static void OnUnloadMap(MapUnloadEventArgs args)
    {
        LogHelper.Information($"Unloading");
        Instance._isTunnelerAvailable = true;
    }

    /// <summary>
    /// Gets a native pointer to the current game cursor manager instance.
    /// </summary>
    /// <returns>A <see cref="NativePointer{GameCursorManager}"/> representing the game player cursor manager.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public NativePointer<GameCursorManager> GetCursorManager()
    {
        return CursorManager;
    }

    /// <summary>
    /// Gets a native pointer to the current game player resource manager instance.
    /// </summary>
    /// <returns>A <see cref="NativePointer{GamePlayerResources}"/> representing the game player resource manager.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<GamePlayerResources> GetPlayerResources()
    {
        return _playerResources;
    }

    /// <summary>
    /// Gets the player manager native ptr.
    /// </summary>
    /// <returns></returns>
    public IntPtr GetPlayerManager()
    {
        return _playerManager;
    }

    /// <summary>
    /// Attempts to retrieve a direct, raw pointer to a player's resource data structure by their ID.
    /// </summary>
    /// <param name="playerId">The ID of the player (1-8).</param>
    /// <param name="resources">When this method returns, contains a pointer to the player's resources if found; otherwise, null.</param>
    /// <returns><c>true</c> if the player ID was valid and the pointer was retrieved; otherwise, <c>false</c>.</returns>
    public bool TryGetPlayerResourcesById(int playerId, out GamePlayerResources* resources)
    {
        resources = null;

        if (!IsPlayerIdValid(playerId))
        {
            LogHelper.Error($"Tried to access player resources index that was out of range: [{playerId}/{MAX_PLAYERS}]");
            return false;
        }

        if (_playerResources._array == null)
            return false;

        resources = &_playerResources._array[playerId - 1];
        return true;
    }

    /// <summary>
    /// Attempts to retrieve a safe, wrapped pointer to a player's resource data structure by their ID.
    /// </summary>
    /// <param name="playerId">The ID of the player (1-8).</param>
    /// <param name="resources">When this method returns, contains a <see cref="NativePointer{GamePlayerResources}"/> wrapping the object if found; otherwise, an invalid pointer.</param>
    /// <returns><c>true</c> if the player ID was valid; otherwise, <c>false</c>.</returns>
    public bool TryGetPlayerResourcesByIdEx(int playerId, out NativePointer<GamePlayerResources> resources)
    {
        bool result = TryGetPlayerResourcesById(playerId, out GamePlayerResources* resourcesPtr);
        resources = new NativePointer<GamePlayerResources>(resourcesPtr);
        return result;
    }

    /// <summary>
    /// Returns the current coop trail id
    /// </summary>
    /// <returns>The current coop trail id</returns>
    [LuaApiExport("GetCurrentCoopTrailId")]
    public int GetCurrentCoopTrailId()
    {
        if (_coopTrailId == null)
        {
            LogHelper.Error($"_coopTrailId ptr is null. This should never happen!");
            return 0;
        }
        return *_coopTrailId;
    }

    /// <summary>
    /// Returns the current coop mission id
    /// </summary>
    /// <returns>The current coop mission id</returns>
    [LuaApiExport("GetCurrentCoopMissionId")]
    public int GetCurrentCoopMissionId()
    {
        if (_coopMissionId == null)
        {
            LogHelper.Error($"_coopMissionId ptr is null. This should never happen!");
            return 0;
        }
        return *_coopMissionId;
    }

    /// <summary>
    /// Returns the current AI advantage setting
    /// </summary>
    /// <returns>The current ai advantage</returns>
    [LuaApiExport("GetCurrentAIAdvantage")]
    public AIAdvantage GetCurrentAIAdvantage()
    {
        if (_aiAdvantage == null)
        {
            LogHelper.Error($"_aiAdvantage ptr is null. This should never happen!");
            return AIAdvantage.Unknown;
        }
        return *_aiAdvantage;
    }

    /// <summary>
    /// Sets the current AI advantage setting
    /// </summary>
    [LuaApiExport("SetCurrentAIAdvantage")]
    public void SetCurrentAIAdvantage(AIAdvantage advantage)
    {
        if (_aiAdvantage == null)
        {
            LogHelper.Error($"_aiAdvantage ptr is null. This should never happen!");
            return;
        }
        *_aiAdvantage = advantage;
    }

    /// <summary>
    /// Returns the game mode of the current skirmish
    /// </summary>
    /// <returns>The current skirmish gamemode</returns>
    [LuaApiExport("GetCurrentSkirmishGameMode")]
    public eSkirmishGameMode GetCurrentSkirmishGameMode()
    {
        if (_currentSkirmishGameMode == null)
        {
            LogHelper.Error($"_currentSkirmishGameMode ptr is null. This should never happen!");
            return eSkirmishGameMode.SKIRMISH_GAME_NOT_SKIRMISH;
        }
        return *_currentSkirmishGameMode;
    }

    /// <summary>
    /// Returns the current trail type
    /// 0 = First Edition Trail
    /// 1 = Warchest Trail
    /// 2 = Extreme Trail
    /// Further trails exist but are not documented.
    /// </summary>
    /// <returns>The current trail type; Otherwise -1 on error.</returns>
    [LuaApiExport("GetCurrentTrailType")]
    public int GetCurrentTrailType()
    {
        if (_currentTrailType == null)
        {
            LogHelper.Error($"_currentTrailType ptr is null. This should never happen!");
            return -1;
        }
        return *_currentTrailType;
    }

    /// <summary>
    /// Returns the current game skirmish mode.
    /// </summary>
    /// <returns>The current game skirmish mode; Otherwise Unknown on error.</returns>
    [LuaApiExport("GetCurrentSkirmishMode")]
    public SkirmishMode GetCurrentSkirmishMode()
    {
        if (_currentSkirmishMode == null)
        {
            LogHelper.Error($"_currentSkirmishMode ptr is null. This should never happen!");
            return SkirmishMode.Unknown;
        }
        return *_currentSkirmishMode;
    }

    /// <summary>
    /// Returns the current game type mode.
    /// </summary>
    /// <returns>The current game type mode; Otherwise GAMETYPE_MULTIPLAYER on error.</returns>
    [LuaApiExport("GetCurrentGameTypeMode")]
    public eGameTypeModes GetCurrentGameTypeMode()
    {
        if (_currentGameTypeMode == null)
        {
            LogHelper.Error($"_currentGameTypeMode ptr is null. This should never happen!");
            return eGameTypeModes.GAMETYPE_MULTIPLAYER;
        }
        return *_currentGameTypeMode;
    }

    /// <summary>
    /// Sets the camera raw position directly in Unity World Space.
    /// </summary>
    /// <param name="position">The target Unity World Space coordinate. This is NOT a game tile coordinate.</param>
    /// <remarks>
    /// This function operates on the engine internal camera coordinate system. It does not account for isometric projection, map rotation, or terrain height. 
    /// Using game tile coordinates (e.g., 400, 400) with this function will result in unpredictable camera movement as the coordinates will be misinterpreted.
    /// It is preserved for specific low-level use cases or for restoring a previously saved raw camera position. 
    /// For general use, call <see cref="SetScreenCenterToTilePosition"/> instead.
    /// </remarks>
    [LuaApiExport("SetScreenCenterPosition")]
    public void SetScreenCenterPosition(Vector2 position)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            CameraControls2D.instance.setCameraPos(position.X, position.Y);
            GameMap.instance.PreCalcScreenCentre();
        });
    }

    /// <summary>
    /// Moves the camera to center the view on a specific local game tile coordinate. This is the recommended function for camera positioning.
    /// </summary>
    /// <param name="position">A Vector2 containing the target local tile coordinates (e.g., X=400, Y=400 on a 400x400 map).</param>
    /// <remarks>
    /// This function acts as a translator, converting human-readable grid coordinates into the complex world coordinates required by the camera.
    /// It should be used instead of SetScreenCenterPosition, which expects raw camera coordinates.
    /// The conversion process is as follows:
    /// <para> 1. Takes a Local Tile Coordinate (the game logical grid). </para>
    /// <para> 2. Converts it to an internal Isometric Tilemap Coordinate, accounting for map rotation. </para>
    /// <para> 3. Calculates the base Unity World Position for that tile. </para>
    /// <para> 4. Adjusts the World Position Y-axis based on the tile terrain height. </para>
    /// <para> 5. Passes the final, calculated World Position to the camera controller. </para>
    /// </remarks>
    [LuaApiExport("SetScreenCenterToTilePosition")]
    public void SetScreenCenterToTilePosition(Vector2 position)
    {
        Vector2 cameraWorldPosition = CoordinateConverter.ConvertLocalTileToCameraWorld(position);
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            // Set the camera position using the calculated world coordinates.
            if (CameraControls2D.instance != null)
            {
                CameraControls2D.instance.setCameraPos(cameraWorldPosition.X, cameraWorldPosition.Y);
            }

            // Recalculate the screen center for rendering updates.
            if (GameMap.instance != null)
            {
                GameMap.instance.PreCalcScreenCentre();
                GameMap.instance.ignoreNextCachedBounds = false;
            }
        });
    }

    /// <summary>
    /// Moves the camera to center the view on a specific unit.
    /// </summary>
    /// <param name="unitId">The unique identifier of the unit to center the view on.</param>
    /// <remarks>
    /// This is a convenience wrapper function. It retrieves the unit's current tile position and then calls
    /// <see cref="SetScreenCenterToTilePosition"/> to perform the camera movement. If the unit ID is not valid, an error will be logged.
    /// </remarks>
    [LuaApiExport("SetScreenCenterToUnit")]
    public void SetScreenCenterToUnit(int unitId)
    {
        if (!GameUnitManagerAPI.Instance.TryGetUnitById(unitId, out GameUnit* unit))
        {
            Log.Error($"SetScreenCenterToUnit: Unit not found by id: {unitId}");
            return;
        }
        SetScreenCenterToTilePosition(unit->CurrentTilePosition()->AsVec2f());
    }

    /// <summary>
    /// Moves the camera to center the view on a specific building.
    /// </summary>
    /// <param name="buildingId">The unique identifier of the building to center the view on.</param>
    /// <remarks>
    /// This is a convenience wrapper function. It retrieves the building's current tile position and then calls
    /// <see cref="SetScreenCenterToTilePosition"/> to perform the camera movement. If the building ID is not valid, an error will be logged.
    /// </remarks>
    [LuaApiExport("SetScreenCenterToBuilding")]
    public void SetScreenCenterToBuilding(int buildingId)
    {
        if (!GameBuildingManagerAPI.Instance.TryGetBuildingById(buildingId, out GameBuilding* building))
        {
            Log.Error($"SetScreenCenterToBuilding: Building not found by id: {buildingId}");
            return;
        }
        SetScreenCenterToTilePosition(building->CurrentTilePosition()->AsVec2f());
    }

    /// <summary>
    /// Moves the camera to center the view on a specific projectile.
    /// </summary>
    /// <param name="projectileId">The unique identifier of the projectile to center the view on.</param>
    /// <remarks>
    /// This is a convenience wrapper function. It retrieves the projectile's current tile position and then calls
    /// <see cref="SetScreenCenterToTilePosition"/> to perform the camera movement. If the projectile ID is not valid, an error will be logged.
    /// </remarks>
    [LuaApiExport("SetScreenCenterToProjectile")]
    public void SetScreenCenterToProjectile(int projectileId)
    {
        if (!GameProjectileManagerAPI.Instance.TryGetProjectileById(projectileId, out GameProjectile* projectile))
        {
            Log.Error($"SetScreenCenterToProjectile: Projectile not found by id: {projectileId}");
            return;
        }
        SetScreenCenterToTilePosition(projectile->CurrentTilePosition()->AsVec2f());
    }

    /// <summary>
    /// Gets the coordinate at the center of the player's screen.
    /// </summary>
    /// <returns>An <see cref="Vector2"/> representing the screen's center tile position.</returns>
    [LuaApiExport("GetScreenCenterPosition")]
    public Vector2 GetScreenCenterPosition()
    {
        return *(Vector2*)&CursorManager->r_ScreenCenterPosX;
    }

    /// <summary>
    /// Gets the local game tile coordinate that is currently at the center of the screen.
    /// </summary>
    /// <returns>
    /// A <see cref="Vector2"/> representing the local tile coordinate (e.g., X and Y) at the screen center.
    /// Returns <see cref="Vector2.Zero"/> if the required game instances are not available or if a valid tile cannot be resolved.
    /// </returns>
    /// <remarks>
    /// This function performs the inverse transformation from screen space to the game local tile coordinate system.
    /// It works by querying the game internal <see cref="GameMap.CalcMapTileFromMousePos"/> method with the screen's center point.
    /// This process accounts for the current camera position, zoom level, map rotation, and isometric terrain height,
    /// ensuring the returned coordinate matches the same system used by <see cref="SetScreenCenterToTilePosition"/>.
    /// </remarks>
    public Vector2 GetScreenCenterTilePosition()
    {
        if (GameMap.instance == null)
        {
            LogHelper.Warning("GameMap instance not found.");
            return Vector2.Zero;
        }

        // Define variables to be populated by the game internal function.
        UnityEngine.Vector3 mouseMapVector = UnityEngine.Vector3.zero;              // World position under the cursor
        UnityEngine.Vector3Int mouseTileMapVector = UnityEngine.Vector3Int.zero;    // The internal, rotated tilemap coordinates
        int clickDepth = 0;                                                         // The calculated sorting depth of the tile

        // Define the screen center position.
        UnityEngine.Vector3 screenCenter = new UnityEngine.Vector3(UnityEngine.Screen.width / 2f, UnityEngine.Screen.height / 2f, 0f);

        // Use the game built-in utility to find the tile at a specific screen position.
        // It accounts for camera position, rotation, zoom, and the isometric projection with terrain height.
        GameMap.instance.CalcMapTileFromMousePos(screenCenter, ref mouseMapVector, ref mouseTileMapVector, ref clickDepth, true, true);

        // Get the GameMapTile object using the returned internal tilemap coordinates.
        GameMapTile mapTile = GameMap.instance.getMapTile(mouseTileMapVector.x, mouseTileMapVector.y);

        // The mapTile object itself contains the original, un-rotated "local" tile coordinates (gameMapX, gameMapY).
        if (mapTile != null)
        {
            return new Vector2(mapTile.gameMapX, mapTile.gameMapY);
        }

        LogHelper.Warning("Could not resolve a valid map tile at the screen center.");
        return Vector2.Zero;
    }

    /// <summary>
    /// Checks if the camera is currently locked to the playable map boundaries.
    /// </summary>
    /// <returns><c>true</c> if the camera is locked within the map bounds; <c>false</c> if it can move freely outside the map.</returns>
    /// <seealso cref="ToggleMapLocked"/>
    public bool IsMapLocked()
    {
        return CameraControls2D.instance.isMapLocked();
    }

    /// <summary>
    /// Toggles the camera's lock state, allowing or preventing it from moving outside the playable map boundaries.
    /// </summary>
    /// <remarks>
    /// When unlocked, the player or scripts can move the camera beyond the intended map edges. This is useful for
    /// developer tools, cinematic sequences, or allowing free-camera exploration.
    /// </remarks>
    /// <seealso cref="IsMapLocked"/>
    [LuaApiExport("ToggleMapLocked")]
    public void ToggleMapLocked()
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            CameraControls2D.instance.toggleMapLocked();
        });
    }

    /// <summary>
    /// Checks if the camera can move.
    /// </summary>
    /// <returns><c>true</c> if the camera is restricted from moving; <c>false</c> if it can move freely.</returns>
    public bool IsCameraControlsDisabled()
    {
        return CameraControls2D.instance.AllowMove;
    }

    /// <summary>
    /// Sets the camera moving restriction.
    /// </summary>
    [LuaApiExport("SetCameraControlsEnabled")]
    public void SetCameraControlsEnabled(bool enabled)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            CameraControls2D.instance.AllowMove = enabled;
        });
    }

    /// <summary>
    /// Gets the camera moving speed
    /// </summary>
    /// <returns>The current camera move speed</returns>
    public float GetCameraMoveSpeed()
    {
        return CameraControls2D.instance.MoveSpeed;
    }

    /// <summary>
    /// Sets the camera moving speed.
    /// </summary>
    [LuaApiExport("SetCameraMoveSpeed")]
    public void SetCameraMoveSpeed(float speed)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            CameraControls2D.instance.MoveSpeed = speed;
        });
    }

    /// <summary>
    /// Gets the camera zoom speed
    /// </summary>
    /// <returns>The current camera move speed</returns>
    public float GetCameraZoomSpeed()
    {
        return CameraControls2D.instance.ZoomSpeed;
    }

    /// <summary>
    /// Sets the camera zoom speed.
    /// </summary>
    [LuaApiExport("SetCameraZoomSpeed")]
    public void SetCameraZoomSpeed(float speed)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            CameraControls2D.instance.ZoomSpeed = speed;
        });
    }

    /// <summary>
    /// Gets the current zoom
    /// </summary>
    public float GetZoom()
    {
        return PerfectPixelWithZoom.instance.pixelsPerUnitScale;
    }

    /// <summary>
    /// Sets the current zoom
    /// </summary>
    [LuaApiExport("SetZoom")]
    public void SetZoom(float scale)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            PerfectPixelWithZoom.instance.SetZoomImmediate(scale);
        });
    }

    /// <summary>
    /// Gets the local tile coordinate currently under the mouse cursor.
    /// </summary>
    /// <returns>An <see cref="UnmanagedVector2{UInt32}"/> representing the mouse's tile position.</returns>
    [LuaApiExport("GetMousePosition")]
    public UnmanagedVector2<UInt32> GetMousePosition()
    {
        return *(UnmanagedVector2<UInt32>*)&CursorManager->r_MouseTileX;
    }

    /// <summary>
    /// Checks if the mouse cursor is currently within the playable game area (not over the UI).
    /// </summary>
    /// <returns><c>true</c> if the cursor is in the game world; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsCursorInGame")]
    public bool IsCursorInGame()
    {
        return CursorManager->r_IsCursorInGame == 1;
    }

    /// <summary>
    /// Gets the ID of the building currently being hovered over by the mouse.
    /// </summary>
    /// <returns>The building ID, or 0 if no building is being hovered.</returns>
    [LuaApiExport("GetHoveredBuildingId")]
    public int GetHoveredBuildingId()
    {
        return (int)CursorManager->r_HoverOverBuildingId;
    }

    /// <summary>
    /// Gets the ID of the unit currently being hovered over by the mouse.
    /// </summary>
    /// <returns>The unit ID, or 0 if no unit is being hovered.</returns>
    [LuaApiExport("GetHoveredUnitId")]
    public int GetHoveredUnitId()
    {
        return (int)CursorManager->r_HoverOverUnitId;
    }

    /// <summary>
    /// Gets the tile ID of the specific part of a building being hovered over by the mouse.
    /// </summary>
    /// <returns>The tile ID of the building segment.</returns>
    [LuaApiExport("GetHoveredBuildingTileId")]
    public int GetHoveredBuildingTileId()
    {
        return (int)CursorManager->r_HoverOverBuildingTileId;
    }

    /// <summary>
    /// Gets the ID of the tile currently under the mouse cursor.
    /// </summary>
    /// <returns>The tile ID.</returns>
    [LuaApiExport("GetHoveredTileId")]
    public int GetHoveredTileId()
    {
        return (int)CursorManager->r_MouseTileId;
    }

    /// <summary>
    /// Gets the number of units currently being hovered over by the mouse (e.g., in a selection box).
    /// </summary>
    /// <returns>The count of hovered units.</returns>
    [LuaApiExport("GetHoveredUnitCount")]
    public int GetHoveredChimpsCount()
    {
        return (int)GameUnitManagerAPI.Instance.GetUnitManager().Pointer->r_HoveredChimpsCount;
    }

    /// <summary>
    /// Gets the ID of the building currently being selected
    /// </summary>
    /// <returns>The building ID, or 0 if no building is being selected (typically still remains the last known selected building).</returns>
    [LuaApiExport("GetSelectedBuildingId")]
    public int GetSelectedBuildingId()
    {
        return (int)*_selectedBuildingId;
    }

    /// <summary>
    /// Gets the number of units currently selected by the player.
    /// </summary>
    /// <returns>The count of selected units.</returns>
    [LuaApiExport("GetSelectedUnitCount")]
    public int GetSelectedChimpsCount()
    {
        return (int)GameUnitManagerAPI.Instance.GetUnitManager().Pointer->r_SelectedChimpsCount;
    }

    /// <summary>
    /// Gets an array containing the IDs of all units currently selected by the player.
    /// Uses EngineInterface.selectedChimps, its format is: [unitId1, unitType1, unitId2, unitType2, ...]
    /// </summary>
    /// <returns>An array of selected unit information.</returns>
    [LuaApiExport("GetSelectedUnits")]
    public SelectedUnitInfo[] GetSelectedChimps()
    {
        int count = GetSelectedChimpsCount();

        List<SelectedUnitInfo> selectedUnits = new(count);
        fixed (int* pSelectedUnits = EngineInterface.selectedChimps)
        {
            Span<SelectedUnitInfo> all = new(pSelectedUnits, count);
            for (int i = 0; i < count; i++)
            {
                //LogHelper.Verbose($"[{i}]=[id={all[i].UnitId}, type={all[i].UnitType}]");
                selectedUnits.Add(all[i]);
            }
        }

        return [.. selectedUnits];
    }

    /// <summary>
    /// Configures the auto-trade settings for a specific good at the market.
    /// </summary>
    /// <param name="goods">The good to configure.</param>
    /// <param name="enabled">Whether auto-trading for this good is enabled.</param>
    /// <param name="buyLevel">The storage amount below which the good will be automatically bought. Leave at -1 for no-op.</param>
    /// <param name="sellLevel">The storage amount above which the good will be automatically sold. Leave at -1 for no-op.</param>
    [LuaApiExport("SetAutoTrade")]
    public void SetAutoTrade(eGoods goods, bool enabled, UInt16 buyLevel, UInt16 sellLevel = 0)
    {
        // set buy value:
        // mov word ptr ds:[rcx+rdx*2+22DA96],ax
        // rcx: 00007FFA600D9150 (playerManager)
        // rdx=(rdx+rax)
        // rdx=SIZEOF(GOODS)
        // rax: eGoods type
        // [playerManager+(SizeOf(Goods)+GoodType)*2+0x22DA96]
        const int PLAYER_RESOURCE_BUY_AT_OFFSET = 0x22DA96;
        const int PLAYER_RESOURCE_SELL_AT_OFFSET = 0x22DC58;
        const int PLAYER_RESOURCE_AUTOTRADE_ENABLED_OFFSET = 0x22D9B5;
        int goodIndex = (int)goods + (int)eGoods.Count;


        UInt16* pEnabledAt = (UInt16*)(_playerManager + goodIndex + PLAYER_RESOURCE_AUTOTRADE_ENABLED_OFFSET);
        *pEnabledAt = (byte)(enabled ? 1 : 0);

        UInt16* pBuyWhenAt = (UInt16*)(_playerManager + goodIndex * 2 + PLAYER_RESOURCE_BUY_AT_OFFSET);
        UInt16* pSellWhenAt = (UInt16*)(_playerManager + goodIndex * 2 + PLAYER_RESOURCE_SELL_AT_OFFSET);
        *pBuyWhenAt = buyLevel;
        *pSellWhenAt = sellLevel;


        LogHelper.Debug($"Enabled: {*pEnabledAt}");
        LogHelper.Debug($"Buying when at: {*pBuyWhenAt}");
        LogHelper.Debug($"Selling when at: {*pSellWhenAt}");
    }

    /// <summary>
    /// Toggles the visibility of health bars above units and buildings.
    /// </summary>
    [LuaApiExport("ToggleHealthBars")]
    public void ToggleHealthBars()
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameMap.instance.ToggleHealthBars();
        });
    }

    /// <summary>
    /// Rotates the map view 90 degrees to the left (counter-clockwise).
    /// </summary>
    [LuaApiExport("RotateMapLeft")]
    public void RotateMapLeft()
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameMap.instance.RotateMapLeft();
        });
    }

    /// <summary>
    /// Rotates the map view 90 degrees to the right (clockwise).
    /// </summary>
    [LuaApiExport("RotateMapRight")]
    public void RotateMapRight()
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameMap.instance.RotateMapRight();
        });
    }

    /// <summary>
    /// Sets the map view to a specific rotation.
    /// </summary>
    /// <param name="rotation">The target rotation direction (<see cref="Dircs"/>).</param>
    /// <param name="centreX">Optional: The X-coordinate to center the rotation on.</param>
    /// <param name="centreY">Optional: The Y-coordinate to center the rotation on.</param>
    /// <param name="force">If true, forces the rotation.</param>
    [LuaApiExport("SetMapRotation")]
    public void SetMapRotation(Dircs rotation, int centreX = -1, int centreY = -1, bool force = false)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameMap.instance.setMapRotation((Enums.Dircs)rotation, centreX, centreY, force);
        });
    }

    /// <summary>
    /// Gets whether the cursor is currently over any noesis GUI
    /// </summary>
    /// <returns>Whether the cursor is currently over any noesis GUI</returns>
    public bool IsOverNoesisGUI()
    {
        return FatControler.instance.overNoesisGUI();
    }

    /// <summary>
    /// Gets whether noesis is to receive keyboard input or not.
    /// </summary>
    /// <returns>Whether noesis is currently receiving keyboard input</returns>
    public bool GetNoesisHasKeyboard()
    {
        return FatControler.instance.noesisHasKeyboard;
    }

    /// <summary>
    /// Sets whether noesis is to receive keyboard input or not.
    /// </summary>
    /// <param name="hasKeyboard">Whether noesis is currently receiving keyboard input</param>
    [LuaApiExport("SetNoesisHasKeyboard")]
    public void SetNoesisHasKeyboard(bool hasKeyboard)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            FatControler.instance.SetNoesisKeyboardState(hasKeyboard);
        });
    }

    /// <summary>
    /// Gets the center point of the current map rotation.
    /// </summary>
    /// <returns>An <see cref="UnmanagedVector2{Int32}"/> representing the rotation's center coordinate.</returns>
    public UnmanagedVector2<Int32> GetRotationCentre()
    {
        int centreX = 0;
        int centreY = 0;
        GameMap.instance.getRotationCentre(ref centreX, ref centreY);
        return new UnmanagedVector2<Int32>(centreX, centreY);
    }

    /// <summary>
    /// Gets the current rotation of the map view.
    /// </summary>
    /// <returns>The current rotation as a <see cref="Dircs"/> enum value.</returns>
    public Dircs GetCurrentRotation()
    {
        return (Dircs)GameMap.instance.CurrentRotation();
    }

    /// <summary>
    /// Retrieves the name of the currently active map.
    /// <Remarks>
    /// This does not contain the full file path, only the file name.
    /// May or may not contain the .map file extension.
    /// </Remarks>
    /// </summary>
    /// <returns>A string containing the name of the current map. Returns an empty string if no map is active.</returns>
    [LuaApiExport("GetCurrentMapName")]
    public string GetCurrentMapName()
    {
        return Marshal.PtrToStringAnsi(_currentMapName, *_currentMapNameLength);
    }

    /// <summary>
    /// Checks if the player is in map editor mode.
    /// </summary>
    /// <returns>Returns true if in Map Editor.</returns>
    [LuaApiExport("IsInMapEditor")]
    public bool IsInMapEditor()
    {
        return *_isInMapEditor == 1;
    }

    /// <summary>
    /// Retrieves the local pause state of the current player.
    /// </summary>
    /// <returns>Pause state</returns>
    [LuaApiExport("IsLocalPaused")]
    public bool IsLocalPaused()
    {
        return *_localPause == 1;
    }

    /// <summary>
    /// Sets the local pause state of the current player.
    /// This does NOT pause the game in a proper multiplayer session. Use with caution.
    /// </summary>
    /// <param name="paused">Paused or not</param>
    [LuaApiExport("SetLocalPaused")]
    public void SetLocalPaused(bool paused)
    {
        *_localPause = paused ? 1 : 0;
    }

    /// <summary>
    /// Gets the AI Lord type for a specific player slot.
    /// </summary>
    /// <param name="playerId">The ID of the player (0-8).</param>
    /// <returns>The <see cref="AILords"/> enum value for the player, or <see cref="AILords.SK_NULL"/> if the player is not an AI or the ID is invalid.</returns>
    [LuaApiExport("GetAILord")]
    public Enums.AILords GetAILord(int playerId)
    {
        if (!IsPlayerIdValid(playerId))
            return Enums.AILords.SK_NULL;

        return (Enums.AILords)_aiLineUp[playerId];
    }

    /// <summary>
    /// Gets the AI Lord type for a specific player slot.
    /// </summary>
    /// <param name="playerId">The ID of the player (0-8).</param>
    /// <param name="aiLord">The AILord value. See <see cref="AILords"/> for reference.</param>
    [LuaApiExport("SetAILord")]
    public void SetAILord(int playerId, Enums.AILords aiLord)
    {
        if (!IsPlayerIdValid(playerId))
            return;

        _aiLineUp[playerId] = (Int32)aiLord;
    }

    /// <summary>
    /// Checks if a specific player slot is controlled by an AI.
    /// </summary>
    /// <param name="playerId">The ID of the player to check (1-8).</param>
    /// <returns><c>true</c> if the player is an AI; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsAI")]
    public bool IsAIPlayer(int playerId)
    {
        if (!IsPlayerIdValid(playerId))
            return false;

        return GetAILord(playerId) != Enums.AILords.SK_NULL;
    }

    /// <summary>
    /// Checks if a specific player slot is controlled by an AI.
    /// Warning: Unreliable
    /// </summary>
    /// <param name="playerId">The ID of the player to check (1-8).</param>
    /// <returns><c>true</c> if the player is an AI; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsAIInternal")]
    public bool IsAIPlayerInternal(int playerId)
    {
        if (!IsPlayerIdValid(playerId))
            return false;

        return _aiBoolPlayerList[playerId - 1] == 1;
    }

    /// <summary>
    /// Sets a specific player slot to be controlled by an AI.
    /// Warning: Unreliable
    /// </summary>
    /// <param name="playerId">The ID of the player (1-8).</param>
    /// <param name="isAI">AI control state.</param>
    [LuaApiExport("SetIsAIInternal")]
    public void SetIsAIPlayerInternal(int playerId, bool isAI)
    {
        if (!IsPlayerIdValid(playerId))
            return;

        _aiBoolPlayerList[playerId - 1] = isAI ? 1 : 0;
    }

    /// <summary>
    /// Validates if a player ID is within the valid range (1 to MAX_PLAYERS).
    /// </summary>
    /// <param name="playerId">The player ID to validate.</param>
    /// <returns><c>true</c> if the player ID is valid; otherwise, <c>false</c>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [LuaApiExport("IsPlayerIdValid")]
    public bool IsPlayerIdValid(int playerId)
    {
        return playerId > 0 && playerId <= MAX_PLAYERS;
    }

    /// <summary>
    /// Sets whether a specific weapon type is allowed to be produced in the current map.
    /// </summary>
    /// <param name="goods">The weapon type (<see cref="eGoods"/>).</param>
    /// <param name="enabled">The enabled state.</param>
    [LuaApiExport("SetProductionGoodAllowed")]
    public void SetIsProductionGoodAllowed(eGoods goods, bool enabled)
    {
        UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedProductionGoodsVA;
        switch (goods)
        {
            case eGoods.STORED_CROSSBOWS:
                ((UInt32*)addr)[0] = enabled ? (UInt32)1 : (UInt32)0;
                break;
            case eGoods.STORED_PIKES:
                ((UInt32*)addr)[1] = enabled ? (UInt32)1 : (UInt32)0;
                break;
            case eGoods.STORED_SWORDS:
                ((UInt32*)addr)[2] = enabled ? (UInt32)1 : (UInt32)0;
                break;
            case eGoods.STORED_BOWS:
                ((UInt32*)addr)[3] = enabled ? (UInt32)1 : (UInt32)0;
                break;
            case eGoods.STORED_SPEARS:
                ((UInt32*)addr)[4] = enabled ? (UInt32)1 : (UInt32)0;
                break;
            case eGoods.STORED_MACES:
                ((UInt32*)addr)[5] = enabled ? (UInt32)1 : (UInt32)0;
                break;
            default:
                break;
        }
    }

    /// <summary>
    /// Gets whether a specific weapon type is allowed to be produced in the current map.
    /// </summary>
    /// <param name="goods">The weapon type (<see cref="eGoods"/>).</param>
    /// <returns><c>true</c> if production is allowed; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsProductionGoodAllowed")]
    public bool IsProductionGoodAllowed(eGoods goods)
    {
        UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedProductionGoodsVA;
        switch (goods)
        {
            case eGoods.STORED_CROSSBOWS:
                return ((UInt32*)addr)[0] == 1 ? true : false;
            case eGoods.STORED_PIKES:
                return ((UInt32*)addr)[1] == 1 ? true : false;
            case eGoods.STORED_SWORDS:
                return ((UInt32*)addr)[2] == 1 ? true : false;
            case eGoods.STORED_BOWS:
                return ((UInt32*)addr)[3] == 1 ? true : false;
            case eGoods.STORED_SPEARS:
                return ((UInt32*)addr)[4] == 1 ? true : false;
            case eGoods.STORED_MACES:
                return ((UInt32*)addr)[5] == 1 ? true : false;
            default:
                break;
        }
        return false;
    }

    /// <summary>
    /// Sets all allowed to produce production goods
    /// </summary>
    /// <param name="enabled"></param>
    [LuaApiExport("SetAllProductionGoodAllowed")]
    public void SetAllProductionGoodAllowed(bool enabled)
    {
        foreach (Enums.Goods good in Enum.GetValues(typeof(Enums.Goods)))
        {
            ushort v = (ushort)good;
            SetIsProductionGoodAllowed((eGoods)v, enabled);
        }
    }

    /// <summary>
    /// Checks if a specific unit type is allowed to be recruited in the current map.
    /// </summary>
    /// <param name="unit">The unit type (<see cref="eChimps"/>).</param>
    /// <returns><c>true</c> if the unit is allowed; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsUnitRecruitable")]
    public bool IsUnitAllowed(eChimps unit)
    {
        // Special cases
        switch (unit)
        {
            case eChimps.CHIMP_TYPE_MONK:
                return *_isMonkAvailable == 1;
            case eChimps.CHIMP_TYPE_ENGINEER:
                return *_isEngineerAvailable == 1;
            case eChimps.CHIMP_TYPE_LADDERMAN:
                return *_isLaddermanAvailable == 1;
            case eChimps.CHIMP_TYPE_TUNNELER:
                return _isTunnelerAvailable;
        }

        // Arab Unit
        if (unit is >= eChimps.CHIMP_TYPE_ARAB_BOW and < eChimps.CHIMP_TYPE_ARAB_BALLISTA)
        {
            UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedArabUnitsVA;
            return ((UInt32*)addr)[(int)unit - (int)eChimps.CHIMP_TYPE_ARAB_BOW] != 0;
        }

        // Euro Unit
        if (unit is >= eChimps.CHIMP_TYPE_ARCHER and < eChimps.CHIMP_TYPE_KNIGHT)
        {
            UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedEuroUnitsVA;
            return ((UInt32*)addr)[(int)unit - (int)eChimps.CHIMP_TYPE_ARCHER] != 0;
        }

        // Bedouin Unit
        if (unit is >= eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER and < eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER)
        {
            UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedBedouinUnitsVA;
            return ((UInt32*)addr)[(int)unit - (int)eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER] != 0;
        }

        // If unit doesn't fall into any recognized category, default to false
        return false;
    }

    /// <summary>
    /// Sets whether a specific unit type is allowed to be recruited in the current map.
    /// </summary>
    /// <param name="unit">The unit type (<see cref="eChimps"/>).</param>
    /// <param name="enabled">The enabled state.</param>
    [LuaApiExport("SetUnitRecruitable")]
    public void SetIsUnitAllowed(eChimps unit, bool enabled)
    {
        LogHelper.Information($"unit: {unit}, enabled: {enabled}");
        // Special cases
        switch (unit)
        {
            case eChimps.CHIMP_TYPE_MONK:
                *_isMonkAvailable = enabled ? (Int32)1 : (Int32)0;
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton.Visibility = enabled ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
                    MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage.Visibility = enabled ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
                });
                return;
            case eChimps.CHIMP_TYPE_ENGINEER:
                *_isEngineerAvailable = enabled ? (Int32)1 : (Int32)0;
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton.Visibility = enabled ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
                    MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButtonX.Visibility = enabled ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
                });
                return;
            case eChimps.CHIMP_TYPE_LADDERMAN:
                *_isLaddermanAvailable = enabled ? (Int32)1 : (Int32)0;
                UnityMainThreadDispatcher.Instance.Enqueue(() =>
                {
                    MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton.Visibility = enabled ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
                    MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButtonX.Visibility = enabled ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
                });
                return;
            case eChimps.CHIMP_TYPE_TUNNELER:
                _isTunnelerAvailable = enabled;
                return;
        }

        // Arab Unit
        if (unit is >= eChimps.CHIMP_TYPE_ARAB_BOW and < eChimps.CHIMP_TYPE_ARAB_BALLISTA)
        {
            UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedArabUnitsVA;
            LogHelper.Information($"MapRulesInfo_AllowedArabUnitsVA: {addr.ToString("X16")}, unit={(int)unit} - {(int)eChimps.CHIMP_TYPE_ARAB_BOW}");

            ((UInt32*)addr)[(int)unit - (int)eChimps.CHIMP_TYPE_ARAB_BOW] = enabled ? (UInt32)1 : (UInt32)0;
            return;
        }

        // Euro Unit
        if (unit is >= eChimps.CHIMP_TYPE_ARCHER and < eChimps.CHIMP_TYPE_KNIGHT)
        {
            UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedEuroUnitsVA;
            LogHelper.Information($"MapRulesInfo_AllowedEuroUnitsVA: {addr.ToString("X16")}, unit={(int)unit} - {(int)eChimps.CHIMP_TYPE_ARCHER}");

            ((UInt32*)addr)[(int)unit - (int)eChimps.CHIMP_TYPE_ARCHER] = enabled ? (UInt32)1 : (UInt32)0;
            return;
        }
   
        // Bedouin Unit
        if (unit is >= eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER and < eChimps.CHIMP_TYPE_BEDOUIN_DEMOLISHER)
        {
            UInt64 addr = GameGlobalsManager.Instance.MapRulesInfo_AllowedBedouinUnitsVA;
            LogHelper.Information($"MapRulesInfo_AllowedBedouinUnitsVA: {addr.ToString("X16")}, unit={(int)unit} - {(int)eTroops.TROOP_BEDOUIN_CAMEL_LANCER}");

            ((UInt32*)addr)[(int)unit - (int)eChimps.CHIMP_TYPE_BEDOUIN_CAMEL_LANCER] = enabled ? (UInt32)1 : (UInt32)0;
            return;
        }
    }

    /// <summary>
    /// Sets all allowed to be recruited troop types.
    /// </summary>
    /// <param name="enabled"></param>
    [LuaApiExport("SetAllUnitsAllowed")]
    public void SetAllUnitsAllowed(bool enabled)
    {
        foreach (eChimps troop in Enum.GetValues(typeof(eChimps)))
        {
            UInt32 v = (UInt32)troop;
            SetIsUnitAllowed((eChimps)v, enabled);
        }
    }

    /// <summary>
    /// Checks if a specific good is allowed to be traded at the market in the current map.
    /// </summary>
    /// <param name="good">The good type (<see cref="eGoods"/>).</param>
    /// <returns><c>true</c> if trading is allowed; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsTradeGoodAllowed")]
    public bool IsTradeGoodAllowed(eGoods good)
    {
        return _allowedTradingGoods[(int)good] == 1;
    }

    /// <summary>
    /// Sets whether a specific good is allowed to be traded at the market in the current map.
    /// </summary>
    /// <param name="good">The good type (<see cref="eGoods"/>).</param>
    /// <param name="enabled">The enabled state.</param>
    [LuaApiExport("SetTradeGoodAllowed")]
    public void SetTradeGoodAllowed(eGoods good, bool enabled)
    {
        _allowedTradingGoods[(int)good] = enabled ? (UInt32)1 : (UInt32)0;
    }

    /// <summary>
    /// Sets all trade goods to be allowed/disallowed from trading
    /// </summary>
    /// <param name="enabled">The enabled state.</param>
    [LuaApiExport("SetAllTradeGoodsAllowed")]
    public void SetAllTradeGoodsAllowed(bool enabled)
    {
        foreach (Enums.Goods good in Enum.GetValues(typeof(Enums.Goods)))
        {
            ushort v = (ushort)good;
            SetTradeGoodAllowed((eGoods)v, enabled);
        }
    }

    /// <summary>
    /// Gets the availability of a specific building type in the build menu for the current map.
    /// </summary>
    /// <param name="building">The building type from the editor's palette (<see cref="eMappers"/>).</param>
    /// <returns><c>true</c> if the building is available; otherwise, <c>false</c>.</returns>
    [LuaApiExport("IsBuildingAvailable")]
    public bool GetBuildingAvailability(eMappers building)
    {
        return ((UInt16*)&_mapRulesManager->r_EMNullAvailable)[(int)building] == (UInt16)1;
    }

    /// <summary>
    /// Sets the availability of a specific building type in the build menu for the current map.
    /// </summary>
    /// <param name="building">The building type from the editor's palette (<see cref="eMappers"/>).</param>
    /// <param name="enabled">The enabled state.</param>
    [LuaApiExport("SetBuildingAvailable")]
    public void SetBuildingAvailability(eMappers building, bool enabled)
    {
        ((UInt16*)&_mapRulesManager->r_EMNullAvailable)[(int)building] = enabled ? (UInt16)1 : (UInt16)0;
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            MainViewModel.instance.HUDmain.RefreshBuildScreen();
        });
    }

    /// <summary>
    /// Sets the availability of all buildings for the current map.
    /// </summary>
    /// <param name="enabled">The enabled state.</param>
    [LuaApiExport("SetAllBuildingAvailability")]
    public void SetAllBuildingAvailability(bool enabled)
    {
        foreach (Enums.eMappers building in Enum.GetValues(typeof(Enums.eMappers)))
        {
            short v = (short)building;
            SetBuildingAvailability((eMappers)v, enabled);
        }
    }

    /// <summary>
    /// Gets the default starting resources for a skirmish game.
    /// </summary>
    /// <param name="good">The type of good.</param>
    /// <returns>The starting amount.</returns>
    [LuaApiExport("GetSkirmishDefaultResources")]
    public UInt32 GetPlayerSkirmishDefaultResources(eGoods good)
    {
        return _defaultSkirmishResources[(int)good];
    }

    /// <summary>
    /// Sets the default starting resources for a skirmish game.
    /// </summary>
    /// <param name="good">The type of good.</param>
    /// <param name="amount">The starting amount.</param>
    /// <returns>Always returns <c>true</c>.</returns>
    [LuaApiExport("SetSkirmishDefaultResources")]
    public bool SetPlayerSkirmishDefaultResources(eGoods good, UInt32 amount)
    {
        _defaultSkirmishResources[(int)good] = amount;
        return true;
    }

    /// <summary>
    /// Gets the default starting gold for a skirmish game.
    /// </summary>
    /// <param name="level">Difficulty level</param>
    /// <returns>The starting amount.</returns>
    [LuaApiExport("GetSkirmishDefaultGold")]
    public UInt32 GetPlayerSkirmishDefaultGold(int level)
    {
        if (level < 0 || level > 2)
        {
            return 0;
        }
        return _defaultSkirmishGold[level];
    }

    /// <summary>
    /// Sets the default starting gold for a skirmish game.
    /// </summary>
    /// <param name="level">Difficulty level</param>
    /// <param name="gold">The starting amount.</param>
    /// <returns>Always returns <c>true</c>.</returns>
    [LuaApiExport("SetSkirmishDefaultGold")]
    public bool SetPlayerSkirmishDefaultGold(int level, UInt32 gold)
    {
        if (level < 0 || level > 2)
        {
            return false;
        }
        _defaultSkirmishGold[level] = gold;
        return true;
    }

    /// <summary>
    /// Gets the default starting unit amount for a skirmish game.
    /// </summary>
    /// <param name="unit">Unit type in question</param>
    /// <returns>The starting amount</returns>
    [LuaApiExport("GetSkirmishDefaultUnitsAmount")]
    public int GetPlayerSkirmishDefaultUnitsAmount(eChimps unit)
    {
        //LogHelper.Verbose($"setting unit: {unit}, _defaultSkirmishSettingsTable:{new IntPtr(_defaultSkirmishSettingsTable).ToString("X16")}");
        return (int)_AIVCastleLayoutTable[(DEFAULT_SKIRMISH_UNITS_OFFSET + (int)unit) / 4];
    }

    /// <summary>
    /// Sets the default starting unit amount for a skirmish game.
    /// NOTE: Not all unit types are supported and may lead to weird results!
    /// </summary>
    /// <param name="unit">Unit type in question</param>
    /// <param name="amount">The starting amount.</param>
    [LuaApiExport("SetSkirmishDefaultUnitsAmount")]
    public void SetPlayerSkirmishDefaultUnitsAmount(eChimps unit, UInt32 amount)
    {
        _AIVCastleLayoutTable[(DEFAULT_SKIRMISH_UNITS_OFFSET + (int)unit) / 4] = amount;
    }

    /// <summary>
    /// Sets the total gold for a specific player.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="gold">Gold to set</param>
    /// <returns>Returns true on success; Otherwise false.</returns>
    [LuaApiExport("SetGold")]
    public bool SetPlayerGold(int playerId, int gold)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            return false;
        }
        resources->r_TotalGoodsGold = gold;
        return true;
    }

    /// <summary>
    /// Gets the total gold for a specific player.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Returns the gold on success; Otherwise 0.</returns>
    [LuaApiExport("GetGold")]
    public int GetPlayerGold(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            return 0;
        }
        return resources->r_TotalGoodsGold;
    }

    /// <summary>
    /// Adds or subtracts gold from a specific player's total. The final amount is clamped at zero.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="gold">Gold to add</param>
    /// <returns>Returns true on success; Otherwise false.</returns>
    [LuaApiExport("AddGold")]
    public bool AddPlayerGold(int playerId, int gold)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            return false;
        }
        int newValue = System.Math.Max((int)resources->r_TotalGoodsGold + gold, 0);
        resources->r_TotalGoodsGold = newValue;
        return true;
    }

    /// <summary>
    /// Gets the ID of the player controlling the local client.
    /// </summary>
    [LuaApiExport("GetLocalId")]
    public int GetLocalPlayerId()
    {
        LogHelper.Verbose("Retrieving local player id");
        return (int)*_localPlayerId;
    }

    /// <summary>
    /// Gets the building ID of a player's keep.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Returns keep id on success; Otherwise -1</returns>
    [LuaApiExport("GetKeepId")]
    public int GetPlayerKeepId(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return -1;
        }

        return (int)resources->r_KeepId;
    }

    /// <summary>
    /// Gets the top-left tile coordinate of a player's keep.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Returns keep position on success; Otherwise default.</returns>
    [LuaApiExport("GetKeepPosition")]
    public UnmanagedVector2<Int32> GetPlayerKeepPosition(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return default;
        }
        return *(UnmanagedVector2<Int32>*)(&resources->r_KeepTilePositionX);
    }

    /// <summary>
    /// Sets the top-left tile coordinate of a player's keep.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="position">Position to set</param>
    /// <returns>Returns true on success; Otherwise false.</returns>
    [LuaApiExport("SetKeepPosition")]
    public bool SetPlayerKeepPosition(int playerId, UnmanagedVector2<UInt32> position)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return false;
        }
        resources->r_KeepTilePositionX = position.X;
        resources->r_KeepTilePositionY = position.Y;
        return true;
    }

    /// <summary>
    /// Gets the tile coordinate of a player's keep door.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Returns keep door position on success; Otherwise default.</returns>
    [LuaApiExport("GetKeepDoorPosition")]
    public UnmanagedVector2<Int32> GetPlayerKeepDoorPosition(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return default;
        }
        return *(UnmanagedVector2<Int32>*)(&resources->r_KeepDoorTilePositionX);
    }

    /// <summary>
    /// Sets the tile coordinate of a player's keep door.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="position">Position to set</param>
    /// <returns>Returns true on success; Otherwise false.</returns>
    [LuaApiExport("SetKeepDoorPosition")]
    public bool SetPlayerKeepDoorPosition(int playerId, UnmanagedVector2<UInt32> position)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return false;
        }
        resources->r_KeepDoorTilePositionX = position.X;
        resources->r_KeepDoorTilePositionY = position.Y;
        return true;
    }

    /// <summary>
    /// Gets an array of all possible player IDs (1 through MAX_PLAYERS).
    /// </summary>
    /// <returns>Returns player ids as an array.</returns>
    [LuaApiExport("GetAllIds")]
    public int[] GetAllPlayerIds()
    {
        return Enumerable.Range(1, MAX_PLAYERS).ToArray();
    }

    /// <summary>
    /// Gets an array of IDs for all players who are currently alive and not defeated.
    /// </summary>
    /// <returns>Returns alive player ids as an array.</returns>
    [LuaApiExport("GetAliveIds")]
    public int[] GetAlivePlayerIds()
    {
        List<int> results = [];

        ExecuteQuery(results, PlayerResourcePredicates.IsAlive);

        return results.ToArray();
    }

    /// <summary>
    /// Sets the popularity level for a specific player.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="popularity">Popularity to set</param>
    /// <returns>Returns true on success; Otherwise false.</returns>
    [LuaApiExport("SetPopularity")]
    public bool SetPlayerPopularity(int playerId, uint popularity)
    {
        popularity = System.Math.Min(popularity, 10000);
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return false;
        }
        resources->r_CurrentPopularity = popularity;
        return true;
    }

    /// <summary>
    /// Gets the current popularity level for a specific player.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Returns popularity on success; Otherwise 0.</returns>
    [LuaApiExport("GetPopularity")]
    public uint GetPlayerPopularity(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return 0;
        }
        return resources->r_CurrentPopularity;
    }

    /// <summary>
    /// Gets the team number for a specific player.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Returns team on success; Otherwise 0.</returns>
    [LuaApiExport("GetTeam")]
    public int GetPlayerTeam(int playerId)
    {
        if (!IsPlayerIdValid(playerId))
        {
            LogHelper.Error($"Tried to access player index that was out of range: [{playerId}/{MAX_PLAYERS}]");
            return 0;
        }
        return _teamList[playerId];
    }

    /// <summary>
    /// Sets the team number for a specific player. Players on the same team are allied.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="team">Team to set</param>
    [LuaApiExport("SetTeam")]
    public void SetPlayerTeam(int playerId, int team)
    {
        if (!IsPlayerIdValid(playerId))
        {
            LogHelper.Error($"Tried to access player index that was out of range: [{playerId}/{MAX_PLAYERS}]");
            return;
        }

        if (team < 0 || team > MAX_PLAYERS)
        {
            LogHelper.Error($"Tried to access team index that was out of range: [{team}/{MAX_PLAYERS}]");
            return;
        }
        _teamList[playerId] = team;
    }

    /// <summary>
    /// Checks if two players are on the same team.
    /// </summary>
    /// <param name="playerId1">From player</param>
    /// <param name="playerId2">To player</param>
    /// <returns>Returns allied state.</returns>
    [LuaApiExport("IsAlliedTo")]
    public bool IsPlayerAlliedTo(int playerId1, int playerId2)
    {
        int team1 = GetPlayerTeam(playerId1);
        int team2 = GetPlayerTeam(playerId2);
        return team1 == team2;
    }

    /// <summary>
    /// Clears all incoming goods for the specified player.
    /// </summary>
    /// <param name="playerId">Target player</param>
    [LuaApiExport("ClearIncomingGoods")]
    public void ClearIncomingGood(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        int* playerIncomingGoodsArray = (int*)(&_playerResources._array[playerId - 1].r_incomingNull);
        foreach (Enums.Goods good in Enum.GetValues(typeof(Enums.Goods)))
        {
            ushort v = (ushort)good;
            playerIncomingGoodsArray[(int)v] = 0;
        }
    }

    /// <summary>
    /// Adds a specified amount to a player's count of incoming goods (e.g., from scenario start).
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="good">Good to add</param>
    /// <param name="amount">Amount to add</param>
    [LuaApiExport("AddIncomingGood")]
    public void AddIncomingGood(int playerId, eGoods good, int amount)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        int* playerIncomingGoodsArray = (int*)(&_playerResources._array[playerId - 1].r_incomingNull);
        playerIncomingGoodsArray[(int)good] += amount;
    }

    /// <summary>
    /// Subtracts a specified amount from a player's count of incoming goods.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="good">Good to subtract</param>
    /// <param name="amount">Amount to subtract</param>
    [LuaApiExport("SubtractIncomingGood")]
    public void SubtractIncomingGood(int playerId, eGoods good, int amount)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        int* playerIncomingGoodsArray = (int*)(&_playerResources._array[playerId - 1].r_incomingNull);
        int newValue = Math.Max(playerIncomingGoodsArray[(int)good] - amount, 0);
        playerIncomingGoodsArray[(int)good] = newValue;
    }

    /// <summary>
    /// Subtracts a specified amount from a player's count of incoming goods.
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Pause state.</returns>
    [LuaApiExport("IsPaused")]
    public bool IsPlayerPaused(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return false;
        }
        return resources->r_IsPaused == 1;
    }

    /// <summary>
    /// Checks if the local player keep is enclosed.
    /// </summary>
    /// <returns>Keep enclosed state</returns>
    [LuaApiExport("IsLocalPlayerKeepEnclosed")]
    public bool IsLocalPlayerKeepEnclosed()
    {
        return _isLocalPlayerKeepEnclosed != null && *_isLocalPlayerKeepEnclosed == 1;
    }

    /// <summary>
    /// Checks if the local player extreme powers are enabled
    /// </summary>
    /// <returns>Extreme Powers enabled state</returns>
    [LuaApiExport("IsLocalPlayerExtremePowersEnabled")]
    public bool IsLocalPlayerExtremePowersEnabled()
    {
        return _isLocalPlayerExtremePowersEnabled != null && *_isLocalPlayerExtremePowersEnabled == 1;
    }

    /// <summary>
    /// Sets the enabled state of the extreme powers for the local player
    /// </summary>
    /// <param name="enabled">Extreme Powers enabled state</param>
    [LuaApiExport("SetLocalPlayerExtremePowersEnabled")]
    public void SetLocalPlayerExtremePowersEnabled(bool enabled)
    {
        if (_isLocalPlayerExtremePowersEnabled == null)
            return;

        *_isLocalPlayerExtremePowersEnabled = (enabled ? 1 : 0);
    }

    /// <summary>
    /// Gets the extreme powers mana of the selected player
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <returns>Extreme Powers mana on success; Otherwise -1</returns>
    [LuaApiExport("GetLocalPlayerExtremePowersMana")]
    public int GetLocalPlayerExtremePowersMana(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return -1;
        }
        return (int)resources->r_ExtremePowersMana;
    }

    /// <summary>
    /// Sets the extreme powers mana for the selected player
    /// </summary>
    /// <param name="playerId">Target player</param>
    /// <param name="mana">New Mana</param>
    [LuaApiExport("SetLocalPlayerExtremePowersMana")]
    public void SetLocalPlayerExtremePowersMana(int playerId, int mana)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_ExtremePowersMana = (UInt32)mana;
    }

    /// <summary>
    /// Sets the paused state for a player.
    /// This controls whether the player or AI can build or interact with the game in a meaningful way.
    /// </summary>
    [LuaApiExport("SetPaused")]
    public void SetPlayerIsPaused(int playerId, bool paused)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_IsPaused = paused ? (byte)1 : (byte)0;
    }

    /// <summary>
    /// Gets the current food ration mode for a specific player.
    /// </summary>
    [LuaApiExport("GetRationsMode")]
    public RationsMode GetRationsMode(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return RationsMode.None;
        }
        return resources->r_RationMode;
    }

    /// <summary>
    /// Sets the food ration mode for a specific player.
    /// </summary>
    [LuaApiExport("SetRationsMode")]
    public void SetRationsMode(int playerId, RationsMode mode)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_RationMode = mode;
    }

    /// <summary>
    /// Gets if a food is allowed
    /// </summary>
    [LuaApiExport("IsFoodAllowed")]
    public bool IsFoodAllowed(int playerId, eGoods food)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return false;
        }
        UInt16* allowedFoodsArr = (UInt16*)((byte*)resources + PLAYER_ALLOWED_FOOD_OFFSET);
        UInt16 value = 0;
        switch (food)
        {
            case eGoods.STORED_FOOD_BREAD:
                value = allowedFoodsArr[0];
                break;
            case eGoods.STORED_FOOD_CHEESE:
                value = allowedFoodsArr[1];
                break;
            case eGoods.STORED_FOOD_MEAT:
                value = allowedFoodsArr[2];
                break;
            case eGoods.STORED_FOOD_FRUIT:
                value = allowedFoodsArr[3];
                break;
        }
        return value == 0;
    }

    /// <summary>
    /// Sets currently allowed foods
    /// </summary>
    [LuaApiExport("SetFoodAllowed")]
    public void SetFoodAllowed(int playerId, eGoods food, bool allowed)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        UInt16* allowedFoodsArr = (UInt16*)((byte*)resources + PLAYER_ALLOWED_FOOD_OFFSET);
        LogHelper.Verbose($"FoodsArr: 0x{new IntPtr(allowedFoodsArr).ToString("X16")}");

        UInt16 value = allowed ? (UInt16)0 : (UInt16)1;
        switch (food)
        {
            case eGoods.STORED_FOOD_BREAD:
                allowedFoodsArr[0] = value;
                break;
            case eGoods.STORED_FOOD_CHEESE:
                allowedFoodsArr[1] = value;
                break;
            case eGoods.STORED_FOOD_MEAT:
                allowedFoodsArr[2] = value;
                break;
            case eGoods.STORED_FOOD_FRUIT:
                allowedFoodsArr[3] = value;
                break;
        }
    }

    /// <summary>
    /// Gets the current tax mode for a specific player.
    /// </summary>
    [LuaApiExport("GetTaxesMode")]
    public TaxesMode GetTaxesMode(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return TaxesMode.None;
        }
        return resources->r_TaxesMode;
    }

    /// <summary>
    /// Sets the tax mode for a specific player.
    /// </summary>
    [LuaApiExport("SetTaxesMode")]
    public void SetTaxesMode(int playerId, TaxesMode mode)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_TaxesMode = mode;
    }

    /// <summary>
    /// Sets the win or loss state for a specific player.
    /// </summary>
    [LuaApiExport("SetWinLossState")]
    public void SetWinLossState(int playerId, WinLossState state)
    {
        // public void Director.initGameOver(int state, int screen, bool victory)?
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_WinLossState = state;
    }

    /// <summary>
    /// Gets the unit ID of a player's Lord.
    /// </summary>
    [LuaApiExport("GetLordUnitId")]
    public int GetLordUnitId(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return 0;
        }
        return (int)resources->r_LordUnitId;
    }

    /// <summary>
    /// Sets the unit ID of a player's Lord.
    /// </summary>
    [LuaApiExport("SetLordUnitId")]
    public void SetLordUnitId(int playerId, int unitId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_LordUnitId = (UInt32)unitId;
    }

    /// <summary>
    /// Gets the global unit ID of a player's Lord.
    /// </summary>
    [LuaApiExport("GetLordUnitGlobalId")]
    public int GetLordUnitGlobalId(int playerId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return 0;
        }
        return (int)resources->r_LordUnitGlobalId;
    }

    /// <summary>
    /// Sets the global unit ID of a player's Lord.
    /// </summary>
    [LuaApiExport("SetLordUnitGlobalId")]
    public void SetLordUnitGlobalId(int playerId, int globalId)
    {
        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find playerresource by id: {playerId}");
            return;
        }
        resources->r_LordUnitGlobalId = (UInt32)globalId;
    }

    /// <summary>
    /// Adds a specified amount of a good to a player, distributing it among their storage buildings.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="good">The type of good to add.</param>
    /// <param name="amount">The amount to add.</param>
    /// <returns><c>true</c> if the operation was successful; otherwise, <c>false</c>.</returns>
    /// <remarks>This is the recommended function for adding goods as it calls the game's internal logic.</remarks>
    [LuaApiExport("AddGood")]
    public bool TryAddGood(int playerId, eGoods good, int amount)
    {
        if (amount == 0)
            return true;

        return BulkPlayerDetours.c_game_player_add_resources_hook_impl(GameBuildingManagerAPI.Instance.GetBuildingManager(), playerId, good, amount) == 1;
    }

    /// <summary>
    /// Removes a specified amount of a good from a player, taking it from their storage buildings.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="good">The type of good to remove.</param>
    /// <param name="amount">The amount to remove.</param>
    /// <param name="bDontSubtractResources">A flag to prevent the visual subtraction effect (unconfirmed).</param>
    /// <remarks>This is the recommended high-level function for removing goods.</remarks>
    [LuaApiExport("RemoveGood")]
    public void RemoveGood(int playerId, eGoods good, int amount, bool bDontSubtractResources = false)
    {
        if (amount == 0)
            return;

        BulkPlayerDetours.c_game_player_subtract_resources_hook_impl(GameBuildingManagerAPI.Instance.GetBuildingManager(), playerId, good, amount, bDontSubtractResources ? 1 : 0);
    }

    /// <summary>
    /// Checks if a player has at least a certain amount of a specific good.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="good">The type of good to check.</param>
    /// <param name="amount">The amount required.</param>
    /// <returns><c>true</c> if the player has the required amount or more; otherwise, <c>false</c>.</returns>
    [LuaApiExport("HasGoodsAmount")]
    public bool HasGoodsAmount(int playerId, eGoods good, int amount)
    {
        return GetGoodAmount(playerId, good) >= amount;
    }

    /// <summary>
    /// Retrieves the quantity of the specified good owned by the given player.
    /// </summary>
    /// <param name="playerId">The ID of the player.</param>
    /// <param name="good">The type of good to check.</param>
    /// <returns>The number of units of the specified good owned by the player. Returns 0 if the player does not own any of the
    /// specified good.</returns>
    [LuaApiExport("GetGoodAmount")]
    public int GetGoodAmount(int playerId, eGoods good)
    {
        int* playerGoodsArray = (int*)(&_playerResources._array[playerId - 1].r_TotalGoodsNull);
        return playerGoodsArray[(int)good];
    }

    /// <summary>
    /// A custom, experimental implementation for removing goods from storage buildings. Do not use.
    /// </summary>
    /// <remarks>This function is obsolete because its visual update component is non-functional.</remarks>
    [System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
    [Obsolete("Do not use.")]
    public bool TryRemoveGoodEx(int playerId, eGoods good, int toRemoveAmount, bool onlyRemoveIfHasEnough)
    {
        if (toRemoveAmount == 0)
            return true;

        if (!TryGetPlayerResourcesById(playerId, out GamePlayerResources* resources))
        {
            LogHelper.Warning($"Could not find player resource by id: {playerId}");
            return false;
        }

        GameStructQuery<GameBuilding> query = GameBuildingManagerAPI.Instance.QueryBuildings()
            .Where(GameBuildingManagerAPI.BuildingPredicates.IsStorage)
            .Where(GameBuildingManagerAPI.BuildingPredicates.IsOwnedByPlayer(playerId))
            .Where(GameBuildingManagerAPI.BuildingPredicates.ContainsGoodInAnyStorage(good));

        // Calculate total available and collect candidate pointers.
        long totalAvailable = 0;
        List<NativePointer<GameBuilding>> candidateBuildings = new List<NativePointer<GameBuilding>>();

        foreach (ref GameBuilding building in query)
        {
            totalAvailable += building.GetLocalGoodsAmount(good);

            // Store a pointer to the building so we can modify it later without re-querying.
            candidateBuildings.Add((GameBuilding*)Unsafe.AsPointer(ref Unsafe.AsRef(in building)));
        }
        if (onlyRemoveIfHasEnough && (totalAvailable < toRemoveAmount))
        {
            return false; // Not enough goods...
        }

        long actualAmountToRemove = System.Math.Min(toRemoveAmount, totalAvailable);
        if (actualAmountToRemove == 0)
        {
            return true; // Nothing to remove
        }

        // Iterate through the list of candidates and remove the goods.
        long amountLeftToRemove = actualAmountToRemove;
        foreach (GameBuilding* buildingPtr in candidateBuildings)
        {
            if (amountLeftToRemove <= 0)
                break;

            ref GameBuilding building = ref Unsafe.AsRef<GameBuilding>(buildingPtr);
            int amountInBuilding = building.GetLocalGoodsAmount(good);
            long amountToRemove = System.Math.Min(amountLeftToRemove, amountInBuilding);
            building.RemoveLocalGoodsAmount(good, (int)-amountToRemove);
            building.UpdateLocalGoodsResourceVisuals();

            amountLeftToRemove -= amountToRemove;
        }

        return true;
    }

    /// <summary>
    /// Gets the local frame time for the native engine.
    /// </summary>
    /// <returns>Frame time / Tickrate of the native engine.</returns>
    public double GetFrameTime()
    {
        return Director.instance.EngineFrameTime;
    }

    /// <summary>
    /// Gets the local frame time for the native engine
    /// Do not call in multiplayer.
    /// </summary>
    /// <param name="fps">The desired new tickrate.</param>
    /// <returns>Frame time / Tickrate of the native engine.</returns>
    [LuaApiExport("SetFrameTime")]
    public void SetFrameTime(double fps)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (GameNetworkAPI.IsNetworkedEnvironment())
            {
                LogHelper.Warning($"Called from a networked environment! This is not safe!");
            }

            Director.instance.SetEngineFrameRate(fps);
        });
    }

    /// <summary>
    /// Gameplay Option: Strong Walls
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsNoKnockdownWalls")]
    public bool IsNoKnockdownWalls()
    {
        if (_noKnockdownWalls == null)
            return false;

        return *_noKnockdownWalls == 1;
    }

    /// <summary>
    /// Gameplay Option: Strong Walls
    /// Warning: This can break AI behaviour.
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetNoKnockdownWalls")]
    public void SetNoKnockdownWalls(bool enabled)
    {
        if (_noKnockdownWalls == null)
            return;

        *_noKnockdownWalls = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Global Improved Siegeing Behaviour
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsGlobalImprovedSiegeBehaviour")]
    public bool IsGlobalImprovedSiegeBehaviour()
    {
        if (_globalImprovedSiegeBehaviour == null)
            return false;

        return *_globalImprovedSiegeBehaviour == 1;
    }

    /// <summary>
    /// Gameplay Option: Global Improved Siegeing Behaviour
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetGlobalImprovedSiegeBehaviour")]
    public void SetGlobalImprovedSiegeBehaviour(bool enabled)
    {
        if (_globalImprovedSiegeBehaviour == null)
            return;

        *_globalImprovedSiegeBehaviour = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Global More Aggressive Siegeing Behaviour
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsGlobalMoreAggressiveSiegeBehaviour")]
    public bool IsGlobalMoreAggressiveSiegeBehaviour()
    {
        if (_globalMoreAggressiveSiegeBehaviour == null)
            return false;

        return *_globalMoreAggressiveSiegeBehaviour == 1;
    }

    /// <summary>
    /// Gameplay Option: Global More Aggressive Siegeing Behaviour
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetGlobalMoreAggressiveSiegeBehaviour")]
    public void SetGlobalMoreAggressiveSiegeBehaviour(bool enabled)
    {
        if (_globalMoreAggressiveSiegeBehaviour == null)
            return;

        *_globalMoreAggressiveSiegeBehaviour = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Improved Arab Swordsman
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsImprovedArabSwordsman")]
    public bool IsImprovedArabSwordsman()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_ImprovedArabSwordsmen == 1;
    }

    /// <summary>
    /// Gameplay Option: Improved Arab Swordsman
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetImprovedArabSwordsman")]
    public void SetImprovedArabSwordsman(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_ImprovedArabSwordsmen = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Improved Ladderman
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsImprovedLadderman")]
    public bool IsImprovedLadderman()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_ImprovedLaddermen == 1;
    }

    /// <summary>
    /// Gameplay Option: Improved Ladderman
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetImprovedLadderman")]
    public void SetImprovedLadderman(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_ImprovedLaddermen = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Improved Spearman
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsImprovedSpearman")]
    public bool IsImprovedSpearman()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_ImprovedSpearmen == 1;
    }

    /// <summary>
    /// Gameplay Option: Improved Spearman
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetImprovedSpearman")]
    public void SetImprovedSpearman(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_ImprovedSpearmen = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Uncapped Peasants
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsUncappedPeasants")]
    public bool IsUncappedPeasants()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_UncappedPeasants == 1;
    }

    /// <summary>
    /// Gameplay Option: Uncapped Peasants
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetUncappedPeasants")]
    public void SetUncappedPeasants(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_UncappedPeasants = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Faster Peasants
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsFasterPeasants")]
    public bool IsFasterPeasants()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_FasterPeasants == 1;
    }

    /// <summary>
    /// Gameplay Option: Faster Peasants
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetFasterPeasants")]
    public void SetFasterPeasants(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_FasterPeasants = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Nerf Eunuchs
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsNerfEunuchs")]
    public bool IsNerfEunuchs()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_Eunuchs == 1;
    }

    /// <summary>
    /// Gameplay Option: Nerf Eunuchs
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetNerfEunuchs")]
    public void SetNerfEunuchs(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_Eunuchs = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Enemy Health
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("GetEnemyHealthModifier")]
    public EnemyHPModifier GetEnemyHealthModifier()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return EnemyHPModifier.Normal;

        return (EnemyHPModifier)_choreManagerOptionsInternal.AdvOpt_EnemyHPS;
    }

    /// <summary>
    /// Gameplay Option: Enemy Health
    /// </summary>
    /// <param name="modifier">Modifier</param>
    [LuaApiExport("SetEnemyHealthModifier")]
    public void SetEnemyHealthModifier(EnemyHPModifier modifier)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_EnemyHPS = (int)modifier;
    }

    /// <summary>
    /// Gameplay Option: Healers
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsBetterHealers")]
    public bool IsBetterHealers()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_Healers == 1;
    }

    /// <summary>
    /// Gameplay Option: Healers
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetBetterHealers")]
    public void SetBetterHealers(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_Healers = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Rebalanced Horse Archers
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsRebalancedHorseArchers")]
    public bool IsRebalancedHorseArchers()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_RebalancedHorseArchers == 1;
    }

    /// <summary>
    /// Gameplay Option: Rebalanced Horse Archers
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetRebalancedHorseArchers")]
    public void SetRebalancedHorseArchers(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_RebalancedHorseArchers = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Improved Fletchers
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsImprovedFletchers")]
    public bool IsImprovedFletchers()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvOpt_ImprovedFletchers == 1;
    }

    /// <summary>
    /// Gameplay Option: Improved Fletchers
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetImprovedFletchers")]
    public void SetImprovedFletchers(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvOpt_ImprovedFletchers = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Advanced Options
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsAdvancedOptionsEnabled")]
    public bool IsAdvancedOptionsEnabled()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvancedOptions == 1;
    }

    /// <summary>
    /// Gameplay Option: Advanced Options
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetAdvancedOptionsEnabled")]
    public void SetAdvancedOptionsEnabled(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvancedOptions = enabled ? 1 : 0;
    }

    /// <summary>
    /// Gameplay Option: Advanced Skirmish Options
    /// </summary>
    /// <returns>Enabled</returns>
    [LuaApiExport("IsAdvancedSkirmishOptionsEnabled")]
    public bool IsAdvancedSkirmishOptionsEnabled()
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return false;

        return _choreManagerOptionsInternal.AdvancedSkirmishOptions == 1;
    }

    /// <summary>
    /// Gameplay Option: Advanced Skirmish Options
    /// </summary>
    /// <param name="enabled">Enabled</param>
    [LuaApiExport("SetAdvancedSkirmishOptionsEnabled")]
    public void SetAdvancedSkirmishOptionsEnabled(bool enabled)
    {
        if (!_choreManagerOptionsInternal.IsValid())
            return;

        _choreManagerOptionsInternal.AdvancedSkirmishOptions = enabled ? 1 : 0;
    }

    /// <summary>
    /// Get the current base trade price of a good.
    /// </summary>
    /// <param name="good">The good to query</param>
    /// <returns>The base price.</returns>
    /// <example>
    /// print(Player_GetTradeBasePrice(eGoods.STORED_WOOD_PLANKS).SellPrice)
    /// </example>
    [LuaApiExport("GetTradeBasePrice")]
    public PackedGoodPrice GetTradeBasePrice(eGoods good)
    {
        // mov eax, [rcx+rax*8+1817B8h]
        // rcx = playerManager
        // rax = good
        PackedGoodPrice* priceTable = (PackedGoodPrice*)((UInt64)_playerManager + BASE_PRICE_TABLE_OFFSET);
        LogHelper.Verbose($"PriceTable: {new IntPtr(priceTable).ToString("X16")}");
        if ((int)good > GoodsEnumCount || (int)good < 0)
        {
            LogHelper.Warning($"Attempted to get base price of a good that is out of bounds: {(int)good}");
            return default;
        }
        return priceTable[(int)good];
    }

    /// <summary>
    /// Set the current base trade price of a good (this gets overwritten per map by defaults)
    /// </summary>
    /// <param name="good">The good to query</param>
    /// <param name="price">The new price</param>
    public void SetTradeBasePrice(eGoods good, PackedGoodPrice price)
    {
        PackedGoodPrice* priceTable = (PackedGoodPrice*)((UInt64)_playerManager + BASE_PRICE_TABLE_OFFSET);
        if ((int)good > GoodsEnumCount || (int)good < 0)
        {
            LogHelper.Warning($"Attempted to set base price of a good that is out of bounds: {(int)good}");
            return;
        }

        priceTable[(int)good] = price;
    }

    /// <summary>
    /// Get the default base trade price of a good.
    /// </summary>
    /// <param name="good">The good to query</param>
    /// <returns>The default base price.</returns>
    /// <example>
    /// print(Player_GetDefaultTradeBasePrice(eGoods.STORED_WOOD_PLANKS).BuyPrice)
    /// </example>
    [LuaApiExport("GetDefaultTradeBasePrice")]
    public PackedGoodPrice GetDefaultTradeBasePrice(eGoods good)
    {
        int sellPriceOffset = 0x70;
        Int32* buyPriceTable = (Int32*)((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + GameGlobalsManager.Instance.DefaultTradeBuyPriceTableRVA);
        Int32* sellPriceTable = (Int32*)((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + GameGlobalsManager.Instance.DefaultTradeBuyPriceTableRVA + (UInt64)sellPriceOffset);
        //LogHelper.Information($"buyPriceTable: {new IntPtr(buyPriceTable).ToString("X16")}, returning: {new IntPtr(&buyPriceTable[(int)good]).ToString("X16")}");
        //LogHelper.Information($"sellPriceTable: {new IntPtr(buyPriceTable).ToString("X16")}, returning: {new IntPtr(&sellPriceTable[(int)good]).ToString("X16")}");
        if ((int)good > GoodsEnumCount || (int)good < 0)
        {
            LogHelper.Warning($"Attempted to get default base price of a good that is out of bounds: {(int)good}");
            return default;
        }

        int goodId = (int)good;
        return new PackedGoodPrice(buyPriceTable[goodId], sellPriceTable[goodId]);
    }

    /// <summary>
    /// Set the default base trade price of a good (the current price that every map load pulls from is from this)
    /// </summary>
    /// <param name="good">The good to query</param>
    /// <param name="price">The new default price</param>
    public void SetDefaultTradeBasePrice(eGoods good, PackedGoodPrice price)
    {
        int sellPriceOffset = 0x70;
        Int32* buyPriceTable = (Int32*)((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + GameGlobalsManager.Instance.DefaultTradeBuyPriceTableRVA);
        Int32* sellPriceTable = (Int32*)((UInt64)CrusaderLibrary.Instance.LibraryModuleHandle + GameGlobalsManager.Instance.DefaultTradeBuyPriceTableRVA + (UInt64)sellPriceOffset);

        if ((int)good > GoodsEnumCount || (int)good < 0)
        {
            LogHelper.Warning($"Attempted to set default base price of a good that is out of bounds: {(int)good}");
            return;
        }

        int goodId = (int)good;
        buyPriceTable[goodId] = price.BuyPrice;
        sellPriceTable[goodId] = price.SellPrice;
    }

    /// <summary>
    /// Plays a lord message.
    /// </summary>
    /// <param name="playerId">The player id the message will be owned by.</param>
    /// <param name="lord">The AILord associated with the message.</param>
    /// <param name="msgType">The message type id.</param>
    /// <returns></returns>
    [LuaApiExport("PlayMessage")]
    public UInt64 PlayMessage(int playerId, AILords lord, AILordMessageType msgType)
    {
        return BulkAIDetours.c_game_ai_enqueue_message_wrapper_hook_impl(GameGlobalsManager.Instance.MessageManagerVA, (UInt64)(Int64)playerId, lord, (UInt64)msgType);
    }

    #region Query System

    /// <summary>
    /// Begins a high-performance query over all possible player resources slots.
    /// </summary>
    /// <returns>A <see cref="GameStructQuery{GamePlayerResources}"/> instance to build upon.</returns>
    public GameStructQuery<GamePlayerResources> QueryPlayerResources()
    {
        return new GameStructQuery<GamePlayerResources>(_playerResources._array, _playerResources.Length);
    }

    /// <summary>
    /// The core query execution method. All public query functions delegate to this.
    /// </summary>
    public void ExecuteQuery(List<int> results, RefPredicate<GamePlayerResources> basePredicate)
    {
        GameStructQuery<GamePlayerResources> query = QueryPlayerResources().Where(basePredicate);

        query.ToIdList(results);
    }

    /// <summary>
    /// A set of common, reusable predicates for convenience.
    /// </summary>
    public static class PlayerResourcePredicates
    {
        /// <summary>A predicate that matches player resources that is currently active.</summary>
        public static readonly RefPredicate<GamePlayerResources> IsPaused = static (in playerResources) => playerResources.r_IsPaused == 1;

        /// <summary>A predicate that matches player resources that has been marked for deletion.</summary>
        public static readonly RefPredicate<GamePlayerResources> IsNotPaused = static (in playerResources) => playerResources.r_IsPaused == 0;

        /// <summary>A predicate that matches player resources that are still alive</summary>
        public static readonly RefPredicate<GamePlayerResources> IsAlive = static (in playerResources) => playerResources.r_IsPaused == 0 && playerResources.r_WinLossState == WinLossState.None && playerResources.r_LordUnitId != 0;
        /// <summary>A predicate that matches any player resources, used as a default for generic queries.</summary>
        public static readonly RefPredicate<GamePlayerResources> Any = static (in playerResources) => true;
    }

    /// <summary>
    /// Fills a list with IDs of all player resources, with optional filters (no spatial constraint).
    /// </summary>
    /// <param name="results">The list to be cleared and filled with player resources IDs.</param>
    public void GetAllPlayerResources(List<int> results)
    {
        ExecuteQuery(results, PlayerResourcePredicates.Any);
    }

    #endregion
}
