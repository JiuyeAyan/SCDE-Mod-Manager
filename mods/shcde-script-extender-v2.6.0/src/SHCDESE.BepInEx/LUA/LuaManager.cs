using NLua;
using R3;
using RedBird.X64.Assembly.Stateful;
using Serilog.Events;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Lua;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.Lua.EventSystem;
using SHCDESE.LUA;
using SHCDESE.LUA.Noesis;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace SHCDESE.Lua;

/// <summary>
/// Manages the lifecycle of the NLua state for map scripting and per-lord AI scripting.
/// </summary>
/// <remarks>
/// <para>
/// The manager is a singleton that subscribes to map load/unload events and coordinates:
/// <list type="bullet">
///   <item>Creating and tearing down the shared <see cref="NLua.Lua"/> state on map transitions.</item>
///   <item>Executing the map's <c>init.lua</c> entry point at the correct lifecycle phase.</item>
///   <item>Running per-lord AI scripts inside isolated table environments.</item>
/// </list>
/// </para>
/// <para>
/// Initialize the event subscriptions by calling <see cref="InitializeSubscribers"/> once during plugin startup.
/// </para>
/// </remarks>
public sealed class LuaManager
{
    // ---------------------------------------------------------------------------------------
    // Singleton
    // ---------------------------------------------------------------------------------------

    private static readonly Lazy<LuaManager> lazy = new Lazy<LuaManager>(() => new LuaManager());

    public static LuaManager Instance { get { return lazy.Value; } }

    // ---------------------------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------------------------

    private const string DEFAULT_LUA_INIT_FILE = "init.lua";

    // ---------------------------------------------------------------------------------------
    // Public State
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Gets the active NLua state, or <see langword="null"/> when no map is loaded.
    /// </summary>
    public NLua.Lua? Lua { get; internal set; }

    /// <summary>
    /// Gets the hook manager that bridges C# R3 events to Lua callbacks for the current map session.
    /// <see langword="null"/> when no Lua state is active.
    /// </summary>
    public LuaHookManager? EventManager { get; internal set; }

    // ---------------------------------------------------------------------------------------
    // Private State
    // ---------------------------------------------------------------------------------------

    /// <summary>Cached source of the map's <c>init.lua</c> (or legacy <c>.map.lua</c> file).</summary>
    private string? _initCodeContent;

    /// <summary>Guards against duplicate subscriber registration.</summary>
    private bool _subscribersRegistered = false;

    /// <summary>Network packet hook for table-sync packets. Kept alive for the session duration.</summary>
    private R3PacketEventHook<LuaNetworkTablePacket>? _tableEventHook;

    /// <summary>Network packet hook for RPC packets. Kept alive for the session duration.</summary>
    private R3PacketEventHook<LuaNetworkRPCPacket>? _rpcEventHook;

    // ---------------------------------------------------------------------------------------
    // Lord AI State
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Resolved Lua table environments for each active lord AI, keyed by lowercase lord name.
    /// Populated during <see cref="InitLordAIs"/> and cleared in <see cref="UnloadLordAIs"/>.
    /// </summary>
    private Dictionary<string, LuaTable> _lordEnvironments = new Dictionary<string, LuaTable>();

    /// <summary>
    /// Holds the pending lord AI registrations. Entries are consumed during <see cref="InitLordAIs"/>.
    /// </summary>
    private readonly Dictionary<string, LuaLordInfo> _registeredLordAIs = new Dictionary<string, LuaLordInfo>();

    // ---------------------------------------------------------------------------------------
    // Asset Mod Lua State
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Resolved Lua table environments for each active asset mod, keyed by mod GUID.
    /// Populated during <see cref="InitAssetMods"/> and cleared in <see cref="UnloadAssetMods"/>.
    /// </summary>
    private readonly Dictionary<string, LuaTable> _assetModEnvironments = new();

    /// <summary>
    /// Holds pending asset mod registrations (GUID → <see cref="LuaAssetModInfo"/>).
    /// Entries are added by <see cref="RegisterAssetMod"/> and consumed during <see cref="InitAssetMods"/>.
    /// </summary>
    private readonly Dictionary<string, LuaAssetModInfo> _registeredAssetMods = new();

    /// <summary>
    /// Maintains the BepInEx-style load order for <see cref="_registeredAssetMods"/>.
    /// Sorted by the mod's plugin folder name using ordinal (byte-value) comparison,
    /// which produces the same ordering as BepInEx: digits -> uppercase -> lowercase (e.g. "000_MyMod" -> "AAA_Mod" -> "zzz_Mod").
    /// Updated on every <see cref="RegisterAssetMod"/> call.
    /// </summary>
    private readonly List<string> _assetModLoadOrder = new();

    // ---------------------------------------------------------------------------------------
    // Nested Types
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Describes the mode in which the Lua state should initialize its entry points.
    /// </summary>
    public enum LuaLoadMode
    {
        /// <summary>A fresh map has started. Calls <c>init()</c>.</summary>
        NewGame = 0,

        /// <summary>A save game has been loaded. Calls <c>load(false)</c>.</summary>
        LoadSave = 1,

        /// <summary>A map has been opened in the Map Editor. Calls <c>load(true)</c>.</summary>
        LoadMapEditor = 2
    }

    /// <summary>Pairs the Lua init script path with the player ID for a single lord AI entry.</summary>
    private sealed class LuaLordInfo(string luaInitPath, int playerId)
    {
        /// <summary>Gets or sets the absolute path to the lord's <c>init.lua</c>.</summary>
        public string LuaInitPath { get; set; } = luaInitPath;

        /// <summary>Gets or sets the 1-based player ID this lord occupies in the current session.</summary>
        public int PlayerId { get; set; } = playerId;
    }

    /// <summary>
    /// Pairs the Lua init script path with the mod root directory for a single asset mod entry.
    /// </summary>
    private sealed class LuaAssetModInfo(string luaInitPath, string modDirectory)
    {
        /// <summary>Gets the absolute path to the mod's <c>Scripts/init.lua</c>.</summary>
        public string LuaInitPath { get; } = luaInitPath;

        /// <summary>Gets the absolute path to the mod's root directory (used for sandboxed I/O).</summary>
        public string ModDirectory { get; } = modDirectory;
    }

    /// <summary>
    /// Compares asset mod GUIDs by their plugin <b>folder name</b> using ordinal (byte-value) ordering, replicating BepInEx's load order: digits (0–9) -> uppercase (A–Z) -> lowercase (a–z).
    /// When two mods share the same folder name the GUID is used as a stable tiebreaker, so no entry is ever silently dropped from the sorted list.
    /// </summary>
    /// <example>
    /// Folder names sort as: "000_Patch" → "AAA_Compat" → "zzz_Extras"
    /// </example>
    private sealed class BepInExFolderOrderComparer : IComparer<string>
    {
        private readonly Dictionary<string, LuaAssetModInfo> _mods;

        public BepInExFolderOrderComparer(Dictionary<string, LuaAssetModInfo> mods)
        {
            _mods = mods;
        }

        public int Compare(string? xGuid, string? yGuid)
        {
            if (xGuid == yGuid) return 0;
            if (xGuid == null) return -1;
            if (yGuid == null) return 1;

            string xFolder = GetFolderName(xGuid);
            string yFolder = GetFolderName(yGuid);

            int folderCmp = string.Compare(xFolder, yFolder, StringComparison.Ordinal);
            if (folderCmp != 0) return folderCmp;

            // Stable tiebreaker: fall back to ordinal GUID comparison so that two
            // mods in different-GUID but same-named folders never collide to 0.
            return string.Compare(xGuid, yGuid, StringComparison.Ordinal);
        }

        private string GetFolderName(string guid)
        {
            if (_mods.TryGetValue(guid, out LuaAssetModInfo? info) && !string.IsNullOrEmpty(info.ModDirectory))
                return Path.GetFileName(info.ModDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

            // Fallback: sort unresolved GUIDs after known ones by prefixing a high-codepoint char.
            return "\xFF" + guid;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------------------

    private LuaManager()
    {
        _initCodeContent = string.Empty;
    }

    // ---------------------------------------------------------------------------------------
    // Initialization
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Registers all map lifecycle event subscriptions.
    /// Must be called once during plugin startup. Subsequent calls are no-ops.
    /// </summary>
    internal static void InitializeSubscribers()
    {
        if (Instance._subscribersRegistered)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnLoadSave.Observable.Subscribe(OnLoadSave);
        MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(OnLoadMap);
        MapLoaderR3EventHooks.OnStartMap.Observable.Subscribe(OnStartMap);
        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);

        Instance._subscribersRegistered = true;
    }

    // ---------------------------------------------------------------------------------------
    // Map Lifecycle Event Handlers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Handles the <c>OnLoadSave</c> event (post-phase). Loads the Lua source and calls
    /// the appropriate entry point depending on whether this is a save or editor load.
    /// </summary>
    private static void OnLoadSave(LoadSaveGameEventArgs e)
    {
        if (e.Phase != EventHookPhase.Post)
        {
            return;
        }

        LogHelper.Information($"Attempting to init lua state due to event");
        Instance.TryLoad(e.FileName);

        LuaLoadMode mode = e.LoadingEditorMap ? LuaLoadMode.LoadMapEditor : LuaLoadMode.LoadSave;
        Instance.TryRunInit(mode);
    }

    /// <summary>
    /// Handles the <c>OnLoadMap</c> event (pre-phase). Reads the Lua source before the map loads
    /// so it is ready for execution when <c>OnStartMap</c> fires.
    /// </summary>
    private static void OnLoadMap(MapLoadEventArgs e)
    {
        if (e.Phase != EventHookPhase.Pre)
            return;

        LogHelper.Information($"Attempting to create lua state due to event, phase: {e.Phase}");
        Instance.TryLoad(e.FileName);
    }

    /// <summary>
    /// Handles the <c>OnStartMap</c> event (post-phase). Executes <c>init()</c> to start the map script.
    /// </summary>
    private static void OnStartMap(MapStartEventArgs e)
    {
        if (e.Phase != EventHookPhase.Post)
            return;

        LogHelper.Information($"Attempting to run init due to event, phase: {e.Phase}");
        Instance.TryRunInit(LuaLoadMode.NewGame);
    }

    /// <summary>
    /// Handles the <c>OnUnloadMap</c> event (pre-phase).
    /// Notifies the Lua script via <c>unload()</c>, then tears down the full Lua state.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs e)
    {
        if (e.Phase != EventHookPhase.Pre)
        {
            return;
        }

        LogHelper.Information($"Attempting to unload lua state due to event, phase: {e.Phase}");
        Instance.TryCallUnload();
        Instance.TryUnload();

        // Pop all values from the ManagedAssemblyImmediate system.
        GameGlobalsManager.Instance.GetManagedAssemblyStateManager().PopAll();
    }

    // ---------------------------------------------------------------------------------------
    // Internal: Load
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Reads the Lua source for the given map file, either from a legacy <c>.lua</c> companion file or from the <c>init.lua</c> entry inside the map's ZIP archive.
    /// Initializes the Lua state if it is not already active.
    /// </summary>
    /// <param name="fileName">The full path to the <c>.map</c> file being loaded.</param>
    /// <returns><see langword="true"/> if Lua source was found and the state is ready; otherwise <see langword="false"/>.</returns>
    internal bool TryLoad(string fileName)
    {
        LogHelper.Information($"Loading file: [{fileName}]");
        try
        {
            // Check if map lua file exists (legacy mode)
            string _mapLuaFile = $"{fileName}.lua";
            if (File.Exists(_mapLuaFile))
            {
                _initCodeContent = File.ReadAllText(_mapLuaFile);
                LogHelper.Information($"Loading [{fileName}] in Legacy mode.");
            }
            else
            {
                // Check if init.lua exists within the map archive.
                _initCodeContent = GameMapArchiveManagerAPI.Instance.TryReadTextFile(DEFAULT_LUA_INIT_FILE);
                LogHelper.Information($"Loading [{fileName}] - Archive mode.");
                LogHelper.Debug($"InitCode: {_initCodeContent}");
            }

            bool hasSource = !string.IsNullOrEmpty(_initCodeContent);
            bool hasLordAIs = _registeredLordAIs.Count > 0;
            bool hasAssetMods = _registeredAssetMods.Count > 0;
            if (!hasSource && !hasLordAIs && !hasAssetMods)
            {
                LogHelper.Warning($"No Lua source found for [{fileName}] and no lord AI or asset mod scripts registered. Skipping Lua init.");
                return false;
            }

            TryInitState();
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Exception during load");
        }
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // Internal: State Initialization
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Creates a new NLua state, registers all enums, API functions, events, and managed globals, and applies the VM sandbox restrictions.
    /// Does nothing if a state is already active.
    /// </summary>
    internal void TryInitState()
    {
        LogHelper.Information($"Attempting to initialize lua state");
        if (Lua != null)
        {
            LogHelper.Warning("Lua state already exists!");
            return;
        }

        try
        {

            Lua = new NLua.Lua(openLibs: true);

            if (Plugin.Instance.ExposeCoreCLRToLua.Value)
            {
                LogHelper.Warning("CLR HAS BEEN EXPOSED TO LUA. THIS IS NOT RECOMMENDED.");
                LogHelper.Warning("DO NOT REPORT ISSUES OR BUGS WITH THIS OPTION ENABLED.");
                Lua.LoadCLRPackage();
            }

            Lua.State.Encoding = Encoding.UTF8;
            Lua["IS_INIT"] = false;

            CreateStateEventArgs eventArgs = new(EventHookPhase.Pre, Lua);
            LuaR3EventHooks.OnCreateState.Raise(eventArgs);

            EventManager = new LuaHookManager();
            Lua["Hooks"] = EventManager;
            Lua["MapUI"] = LuaUIManagerAPI.Instance;

            RegisterEnums();
            RegisterFunctions();
            RegisterEvents();
            RegisterManagedAssemblyValues();
            PrepareVMSandbox();

            CreateStateEventArgs postEventArgs = new(EventHookPhase.Post, Lua);
            LuaR3EventHooks.OnCreateState.Raise(postEventArgs);

            LogHelper.Information("Lua state initialized");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during initialization");
        }
    }

    // ---------------------------------------------------------------------------------------
    // Internal: State Execution
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Forces a call to <c>init()</c> on the active Lua state, bypassing the <c>IS_INIT</c> guard.
    /// </summary>
    /// <remarks>Intended for debug/editor use only.</remarks>
    internal void TryRunInitForce()
    {
        if (Lua == null)
        {
            LogHelper.Warning($"LUA is null");
            return;
        }
        Lua.DoString("init();");
    }

    /// <summary>
    /// Loads and executes the map's init code then invokes the appropriate Lua entry point (<c>init()</c>, <c>load(false)</c>, or <c>load(true)</c>) based on <paramref name="loadMode"/>.
    /// Also runs all registered lord AI scripts via <see cref="InitLordAIs"/>.
    /// </summary>
    /// <param name="loadMode">The reason the Lua state is being initialized, which controls which entry point is called.</param>
    /// <returns><see langword="true"/> on success; <see langword="false"/> if the state is null, already initialized, or an error occurs.</returns>
    internal bool TryRunInit(LuaLoadMode loadMode)
    {
        LogHelper.Information($"Attempting to execute lua-side init functions. Reason={loadMode}");
        try
        {
            if (Lua == null)
            {
                LogHelper.Warning($"Lua state is null");
                return false;
            }

            if ((bool)Lua["IS_INIT"])
            {
                LogHelper.Warning("TryRunInit: state already initialized (IS_INIT=true), skipping");
                return false;
            }

            // Load and execute the map's init chunk if source is available.
            if (!string.IsNullOrEmpty(_initCodeContent))
            {
                LuaFunction chunk = Lua.LoadString(_initCodeContent, DEFAULT_LUA_INIT_FILE);
                chunk.Call();

                switch (loadMode)
                {
                    case LuaLoadMode.NewGame:
                        Lua.DoString("init();");
                        break;
                    case LuaLoadMode.LoadSave:
                        Lua.DoString("load(false);");
                        break;
                    case LuaLoadMode.LoadMapEditor:
                        Lua.DoString("load(true);");
                        break;
                }
            }

            InitAssetMods(loadMode);
            InitLordAIs(loadMode);
            Lua["IS_INIT"] = true;

            LogHelper.Information("TryRunInit completed successfully");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during TryRunInit");
        }
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // Internal: State Teardown
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts to call the <c>unload()</c> function in the active Lua state, giving scripts a chance to clean up before the state is destroyed.
    /// Exceptions are swallowed since the state may already be partially invalid.
    /// </summary>
    private void TryCallUnload()
    {
        try
        {
            LuaFunction? func = Lua?.GetFunction("unload");
            func?.Call();
        }
        catch (Exception ex)
        {
            // The state can be in a degraded condition at this point; log and continue.
            LogHelper.Error(ex, "Exception during Lua unload() call (non-fatal)");
        }
    }

    /// <summary>
    /// Tears down and disposes the active Lua state and all associated lord environments.
    /// Clears network packet hooks and the UI layer.
    /// </summary>
    internal void TryUnload()
    {
        try
        {
            LogHelper.Information($"Attempting to unload lua state");

            if (Lua == null)
            {
                LogHelper.Warning("Lua state already unloaded!");
                return;
            }

            UnloadAssetMods();
            UnloadLordAIs();
            LuaUIManagerAPI.Instance.ClearAll();

            _tableEventHook = null;
            _rpcEventHook = null;

            Lua.Close();
            Lua.Dispose();
            Lua = null;

            LogHelper.Information("Lua state unloaded!");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during lua state unload");
        }
    }

    /// <summary>
    /// Initializes all pending lord AI scripts by loading each one into an isolated Lua table environment and calling <c>ai_init(playerId)</c> if it is defined.
    /// </summary>
    /// <remarks>
    /// Each lord's environment uses its own table with <c>_G</c> as the <c>__index</c> fallback,
    /// ensuring lords share access to global Script Extender APIs without polluting each other's scope.
    /// </remarks>
    public void InitLordAIs(LuaLoadMode mode)
    {
        if (Lua == null)
        {
            LogHelper.Warning("Lua state unloaded!");
            return;
        }
        LogHelper.Information($"Initializing {_registeredLordAIs.Count} lord AI scripts");

        foreach (KeyValuePair<string, LuaLordInfo> kvp in _registeredLordAIs)
        {
            string lordName = kvp.Key;
            LuaLordInfo lordInfo = kvp.Value;

            // Clear any stale table from a previous session.
            if (Lua[lordName] != null)
                Lua[lordName] = null;

            // Create and configure an isolated table environment.
            Lua.NewTable(lordName);
            LuaTable lordEnv = (LuaTable)Lua[lordName];

            string envKey = $"__env_{lordInfo.PlayerId}";
            Lua[envKey] = lordEnv;
            Lua.DoString($"setmetatable({envKey}, {{ __index = _G }})");

            // Load and execute the lord script inside the isolated environment.
            string chunkContent = File.ReadAllText(lordInfo.LuaInitPath);
            string chunkName = "@" + Path.GetFileNameWithoutExtension(lordInfo.LuaInitPath);

            string srcKey = $"{envKey}_src";
            string nameKey = $"{envKey}_name";
            Lua[srcKey] = chunkContent;
            Lua[nameKey] = chunkName;
            Lua.DoString($"local chunk = assert(load({srcKey}, {nameKey}, 't', {envKey})) chunk(); {srcKey} = nil; {nameKey} = nil");

            // Clean up the temporary environment reference.
            Lua[envKey] = null;

            // Fire ai_init if defined in the script.
            LuaFunction? aiInit = lordEnv["ai_init"] as LuaFunction;
            if (aiInit != null)
                aiInit.Call(lordInfo.PlayerId, mode);
            else
                LogHelper.Debug($"Lord [{lordName}] has no ai_init function");

            _lordEnvironments[lordName] = lordEnv;
            LogHelper.Information($"Lord AI initialized: [{lordName}]");
        }
    }

    /// <summary>
    /// Clears all active lord AI environments and pending registrations from the Lua state.
    /// Called during map unload.
    /// </summary>
    public void UnloadLordAIs()
    {
        foreach (string lordName in _lordEnvironments.Keys.ToList())
        {
            Lua?[lordName] = null;

            LogHelper.Information($"Unloaded lord AI environment: [{lordName}]");
        }

        _lordEnvironments.Clear();
        _registeredLordAIs.Clear();
    }

    internal void RegisterLordAI(string lordName, string pathToInitLua, int playerId)
    {
        _registeredLordAIs[lordName] = new LuaLordInfo(pathToInitLua, playerId);
        LogHelper.Information($"Registered lord AI: {lordName} -> {pathToInitLua}");

        // AIV imports occur before the map-load event.
        // We keep the registration pending so TryLoad can create the shared state and OnStartMap can invoke ai_init only after the map has finished starting.
    }

    // ---------------------------------------------------------------------------------------
    // Internal: Asset Mod Lifecycle
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Registers an Asset Mod as a candidate for per-map Lua execution.
    /// Must be called during plugin startup (before maps load).
    /// If a Lua state is already active, the mod is initialised immediately.
    /// </summary>
    /// <param name="guid">The mod's unique GUID (used as the global table name, e.g. <c>MyMod</c>).</param>
    /// <param name="pathToInitLua">Absolute path to <c>Scripts/init.lua</c>.</param>
    /// <param name="modDirectory">Absolute path to the mod root (used for sandboxed file I/O).</param>
    internal void RegisterAssetMod(string guid, string pathToInitLua, string modDirectory)
    {
        bool isNew = !_registeredAssetMods.ContainsKey(guid);
        _registeredAssetMods[guid] = new LuaAssetModInfo(pathToInitLua, modDirectory);
        LogHelper.Information($"Registered Asset Mod Lua script: GUID={guid} -> {pathToInitLua}");

        // Keep _assetModLoadOrder in BepInEx folder-name ordinal order.
        if (isNew)
        {
            // Insert into the sorted position rather than sorting the entire list each time.
            var comparer = new BepInExFolderOrderComparer(_registeredAssetMods);
            int insertAt = _assetModLoadOrder.BinarySearch(guid, comparer);
            if (insertAt < 0) insertAt = ~insertAt; // BinarySearch returns bitwise complement of insertion point when not found
            _assetModLoadOrder.Insert(insertAt, guid);
        }

        // If its a re-registration (same GUID), the folder name is unlikely to change,
        // but re-sort defensively to keep the list consistent.
        else
        {
            _assetModLoadOrder.Sort(new BepInExFolderOrderComparer(_registeredAssetMods));
        }

        // If the state is already live (late registration), init immediately.
        if (Lua != null && (bool)Lua["IS_INIT"])
        {
            LogHelper.Warning($"Late Lua registration for asset mod [{guid}] - state is already initialised. Initialising now.");
            InitSingleAssetMod(guid, _registeredAssetMods[guid], LuaLoadMode.NewGame);
        }
    }

    /// <summary>
    /// Initialises all pending asset mod Lua scripts. Each mod receives its own isolated table environment (with <c>_G</c> as the <c>__index</c> fallback), mirrors the Lord AI pattern,
    /// and has its I/O sandbox rooted at its own directory.
    /// </summary>
    /// <remarks>
    /// Called from <see cref="TryRunInit"/> after the map script but before Lord AI init.
    /// Entry points called per mod:
    /// <list type="bullet">
    ///   <item><c>mod_init()</c> - fresh map start</item>
    ///   <item><c>mod_load(false)</c> - save game loaded</item>
    ///   <item><c>mod_load(true)</c> - map editor opened</item>
    /// </list>
    /// </remarks>
    /// <param name="loadMode">The map lifecycle mode, used to select the correct entry point.</param>
    public void InitAssetMods(LuaLoadMode loadMode)
    {
        if (Lua == null)
        {
            LogHelper.Warning("Lua state is null, cannot initialise asset mod scripts.");
            return;
        }

        LogHelper.Information($"Initialising {_registeredAssetMods.Count} asset mod Lua script(s)");
        foreach (string guid in _assetModLoadOrder)
        {
            if (_registeredAssetMods.TryGetValue(guid, out LuaAssetModInfo? info))
                InitSingleAssetMod(guid, info, loadMode);
        }
    }

    /// <summary>
    /// Loads and executes one asset mod script inside an isolated table environment.
    /// </summary>
    private void InitSingleAssetMod(string guid, LuaAssetModInfo info, LuaLoadMode loadMode)
    {
        if (Lua == null)
            return;

        try
        {
            // Clear any stale table from a previous session.
            if (Lua[guid] != null)
                Lua[guid] = null;

            Lua.NewTable(guid);
            LuaTable modEnv = (LuaTable)Lua[guid];

            // Use a temporary key to set the metatable without polluting globals.
            string envKey = "__assetmod_env_" + Guid.NewGuid().ToString("N");
            Lua[envKey] = modEnv;
            Lua.DoString($"setmetatable({envKey}, {{ __index = _G }})");

            // Give each mod a require() closure with its own module cache and explicit indexed namespace.
            // It remains correct when callbacks run after initialization.
            string guidKey = envKey + "_guid";
            Lua[guidKey] = guid;
            Lua.DoString($"{envKey}.require = __create_mod_require({guidKey}, {envKey}); {guidKey} = nil");

            if (!GameAssetManagerAPI.Instance.GetModFileTextContent(guid, info.LuaInitPath, out string chunkContent))
                throw new FileNotFoundException($"Indexed Lua entry point was not found for [{guid}]", info.LuaInitPath);
            string chunkName = "@" + guid + "/" + info.LuaInitPath.Replace('\\', '/');

            // Make legacy IO_GetFileText calls use this mod's indexed namespace while its
            // entry point and lifecycle callback execute.
            LuaIO.SetExecutionContext(LuaExecutionContext.AssetMod, info.ModDirectory, guid);

            string srcKey = $"{envKey}_src";
            string nameKey = $"{envKey}_name";
            Lua[srcKey] = chunkContent;
            Lua[nameKey] = chunkName;
            Lua.DoString($"local chunk = assert(load({srcKey}, {nameKey}, 't', {envKey})) chunk(); {srcKey} = nil; {nameKey} = nil");

            // Clean up the temporary environment reference.
            Lua[envKey] = null;

            // Callbacks
            switch (loadMode)
            {
                case LuaLoadMode.NewGame:
                    {
                        LuaFunction? modInit = modEnv["mod_init"] as LuaFunction;
                        if (modInit != null)
                            modInit.Call();
                        else
                            LogHelper.Debug($"Asset mod [{guid}] has no mod_init() function - skipping.");
                        break;
                    }

                case LuaLoadMode.LoadSave:
                    {
                        LuaFunction? modLoad = modEnv["mod_load"] as LuaFunction;
                        if (modLoad != null)
                            modLoad.Call(false);
                        else
                            LogHelper.Debug($"Asset mod [{guid}] has no mod_load() function - skipping.");
                        break;
                    }

                case LuaLoadMode.LoadMapEditor:
                    {
                        LuaFunction? modLoad = modEnv["mod_load"] as LuaFunction;
                        if (modLoad != null)
                            modLoad.Call(true);
                        else
                            LogHelper.Debug($"Asset mod [{guid}] has no mod_load() function - skipping.");
                        break;
                    }
            }

            _assetModEnvironments[guid] = modEnv;
            LogHelper.Information($"Asset mod Lua script initialised: [{guid}]");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Exception initialising asset mod Lua script [{guid}]");
        }
        finally
        {
            LuaIO.ResetExecutionContext();
        }
    }

    /// <summary>
    /// Calls <c>mod_unload()</c> on every active asset mod environment and then removes the environments from the Lua state. Called during map unload.
    /// </summary>
    public void UnloadAssetMods()
    {
        foreach (KeyValuePair<string, LuaTable> kvp in _assetModEnvironments)
        {
            try
            {
                LuaFunction? modUnload = kvp.Value["mod_unload"] as LuaFunction;
                modUnload?.Call();
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Exception during mod_unload() for asset mod [{kvp.Key}] (non-fatal)");
            }

            if (Lua != null)
                Lua[kvp.Key] = null;

            LogHelper.Information($"Unloaded asset mod Lua environment: [{kvp.Key}]");
        }

        _assetModEnvironments.Clear();
        // NOTE: _registeredAssetMods and _assetModLoadOrder are intentionally NOT cleared here.
        // Asset mods persist across map loads and are re-initialised each session.
    }

    // ---------------------------------------------------------------------------------------
    // Internal: Hook Registration
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Scans the given assembly for public static fields decorated with <see cref="LuaApiExportAttribute"/> and registers them with the <see cref="EventManager"/> as Lua hooks or reactive properties.
    /// </summary>
    /// <param name="assembly">The assembly to scan.</param>m>
    public void RegisterHooksFromAssembly(Assembly assembly)
    {
        if (EventManager == null)
            return;

        LogHelper.Information($"Scanning assembly [{assembly.GetName().Name}] for Lua Attributes...");
        int count = 0;

        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException ex)
        {
            types = ex.Types.Where(t => t != null).ToArray()!;
        }

        foreach (Type type in types)
        {
            RegisterHooksFromType(type, ref count);
        }

        LogHelper.Information($"Auto-registered {count} items from [{assembly.GetName().Name}].");
    }

    /// <summary>
    /// Scans a single type for <see cref="LuaApiExportAttribute"/> fields and registers them with the <see cref="EventManager"/>.
    /// </summary>
    /// <param name="type">The type to scan.</param>
    public void RegisterHooksFromType(Type type)
    {
        int count = 0;
        RegisterHooksFromType(type, ref count);
    }

    /// <summary>
    /// Core implementation for attribute-driven hook registration.
    /// Handles both <c>R3EventHook&lt;T&gt;</c> fields and <c>ReactiveProperty&lt;T&gt;</c> fields.
    /// </summary>
    private void RegisterHooksFromType(Type type, ref int count)
    {
        foreach (FieldInfo field in type.GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            LuaApiExportAttribute? attribute = field.GetCustomAttribute<LuaApiExportAttribute>();
            if (attribute == null)
                continue;

            try
            {
                object? fieldValue = field.GetValue(null);
                if (fieldValue == null)
                    continue;

                Type fieldType = fieldValue.GetType();

                if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(R3EventHook<>))
                {
                    Type eventArgsType = fieldType.GetGenericArguments()[0];
                    MethodInfo? registerMethod = typeof(LuaHookManager)
                        .GetMethod("RegisterHook")
                        ?.MakeGenericMethod(eventArgsType);

                    registerMethod?.Invoke(EventManager, new object[] { attribute.LuaFunctionName, fieldValue });
                    count++;
                }
                else if (fieldType.IsGenericType && fieldType.GetGenericTypeDefinition() == typeof(ReactiveProperty<>))
                {
                    Type propType = fieldType.GetGenericArguments()[0];
                    MethodInfo? registerMethod = typeof(LuaHookManager)
                        .GetMethod("RegisterProperty")
                        ?.MakeGenericMethod(propType);

                    registerMethod?.Invoke(EventManager, new object[] { attribute.LuaFunctionName, fieldValue });
                    count++;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Failed to auto-register field [{type.Name}.{field.Name}]");
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // Internal: State Setup Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Registers all game enums into the active Lua state under their standard <c>e*</c> global names.
    /// </summary>
    internal void RegisterEnums()
    {
        if (Lua == null)
            return;

        LuaUtil.RegisterEnum(Lua, typeof(eChimps), "eChimps");
        LuaUtil.RegisterEnum(Lua, typeof(eStructs), "eStructs");
        LuaUtil.RegisterEnum(Lua, typeof(eMappers), "eMappers");
        LuaUtil.RegisterEnum(Lua, typeof(eGoods), "eGoods");
        LuaUtil.RegisterEnum(Lua, typeof(eTroops), "eTroops");
        LuaUtil.RegisterEnum(Lua, typeof(Dircs), "eDircs");
        LuaUtil.RegisterEnum(Lua, typeof(EventHookPhase), "eEventHookPhase");
        LuaUtil.RegisterEnum(Lua, typeof(ProjectileType), "eProjectileType");
        LuaUtil.RegisterEnum(Lua, typeof(GM), "eGM");
        LuaUtil.RegisterEnum(Lua, typeof(TribeStance), "eTribeStance");
        LuaUtil.RegisterEnum(Lua, typeof(TribeAICommand), "eTribeAICommand");
        LuaUtil.RegisterEnum(Lua, typeof(AliveState), "eAliveState");
        LuaUtil.RegisterEnum(Lua, typeof(TribeMoveType), "eTribeMoveType");
        LuaUtil.RegisterEnum(Lua, typeof(VegetationType), "eVegetationType");
        LuaUtil.RegisterEnum(Lua, typeof(TreeGrowthStage), "eTreeGrowthStage");
        LuaUtil.RegisterEnum(Lua, typeof(TribePatrolMode), "eTribePatrolMode");
        LuaUtil.RegisterEnum(Lua, typeof(PlayerRelationship), "ePlayerRelationship");
        LuaUtil.RegisterEnum(Lua, typeof(TileType), "eTileType");
        LuaUtil.RegisterEnum(Lua, typeof(TilePropertyFlag), "eTilePropertyFlag");
        LuaUtil.RegisterEnum(Lua, typeof(WinLossState), "eWinLossState");
        LuaUtil.RegisterEnum(Lua, typeof(TaxesMode), "eTaxesMode");
        LuaUtil.RegisterEnum(Lua, typeof(RationsMode), "eRationsMode");
        LuaUtil.RegisterEnum(Lua, typeof(eTimerModes), "eTimerModes");
        LuaUtil.RegisterEnum(Lua, typeof(Enums.eTextSections), "eTextSections");
        LuaUtil.RegisterEnum(Lua, typeof(Enums.eTextValues), "eTextValues");
        LuaUtil.RegisterEnum(Lua, typeof(UnitSelectionType), "eUnitSelectionType");
        LuaUtil.RegisterEnum(Lua, typeof(LogEventLevel), "eLogEventLevel");
        LuaUtil.RegisterEnum(Lua, typeof(Enums.eSFX), "eSFX");
        LuaUtil.RegisterEnum(Lua, typeof(UnityEngine.KeyCode), "eKeyCode");
        LuaUtil.RegisterEnum(Lua, typeof(PitchState), "ePitchState");
        LuaUtil.RegisterEnum(Lua, typeof(EnemyHPModifier), "eEnemyHPModifier");
        LuaUtil.RegisterEnum(Lua, typeof(eSkirmishGameMode), "eSkirmishGameMode");
        LuaUtil.RegisterEnum(Lua, typeof(SkirmishMode), "eDifficultyMode");
        LuaUtil.RegisterEnum(Lua, typeof(eGameTypeModes), "eGameTypeModes");
        LuaUtil.RegisterEnum(Lua, typeof(AIAdvantage), "eAIAdvantage");
        LuaUtil.RegisterEnum(Lua, typeof(AISellBuyPhase), "eAISellBuyPhase");
        LuaUtil.RegisterEnum(Lua, typeof(AILords), "eAILords");
        LuaUtil.RegisterEnum(Lua, typeof(AILordMessageType), "eAILordMessageType");
        LuaUtil.RegisterEnum(Lua, typeof(LuaLoadMode), "eLuaLoadMode");
        LuaUtil.RegisterEnum(Lua, typeof(GatePathOverrideMode), "eGatePathOverrideMode");
    }


    /// <summary>
    /// Registers all Script Extender Lua API function modules into the active Lua state.
    /// </summary>
    internal void RegisterFunctions()
    {
        if (Lua == null)
            return;

        LogHelper.Information($"Registering Lua API functions");

        LuaMisc.RegisterFunctions(Lua);
        LuaUnitAPI.RegisterFunctions(Lua);
        LuaBuildingAPI.RegisterFunctions(Lua);
        LuaTileAPI.RegisterFunctions(Lua);
        LuaPlayerAPI.RegisterFunctions(Lua);
        LuaSoundAPI.RegisterFunctions(Lua);
        LuaProjectileAPI.RegisterFunctions(Lua);
        LuaTribeAPI.RegisterFunctions(Lua);
        LuaVegetationAPI.RegisterFunctions(Lua);
        LuaTimeAPI.RegisterFunctions(Lua);
        LuaTriggerAPI.RegisterFunctions(Lua);
        LuaMetadataAPI.RegisterFunctions(Lua);
        LuaPersistentAPI.RegisterFunctions(Lua);
        LuaIO.RegisterFunctions(Lua);
        LuaNetworkAPI.RegisterFunctions(Lua);
        LuaTranslateAPI.RegisterFunctions(Lua);
        LuaBepInEx.RegisterFunctions(Lua);
        LuaAIAPI.RegisterFunctions(Lua);
        LuaPitchAPI.RegisterFunctions(Lua);
    }

    /// <summary>
    /// Registers all R3 event hooks from the Script Extender assembly and the two network packet hooks.
    /// </summary>
    internal void RegisterEvents()
    {
        if (Lua == null || EventManager == null)
            return;

        RegisterHooksFromAssembly(Assembly.GetExecutingAssembly());

        _tableEventHook = GameNetworkAPI.Instance.GetPacketEventFor<LuaNetworkTablePacket>(CustomNetworkPacketType.LuaTable);
        EventManager.RegisterHook("OnReceiveTablePacket", _tableEventHook.GetBaseHook());

        _rpcEventHook = GameNetworkAPI.Instance.GetPacketEventFor<LuaNetworkRPCPacket>(CustomNetworkPacketType.LuaRPC);
        EventManager.RegisterHook("OnReceiveRpcPacket", _rpcEventHook.GetBaseHook());
    }

    /// <summary>
    /// Exposes all <see cref="StatefulAssemblyItemEntry"/> globals marked as <c>Exposed</c> in the <see cref="GameGlobalsManager"/> into the active Lua state.
    /// Each item is wrapped in a <see cref="LuaAssemblyGlobalProxy"/> so that NLua's automatic double coercion does not cause "Invalid arguments" errors when Lua
    /// calls Push/SetValue with a number on a byte/sbyte/int typed wrapper.
    /// </summary>
    internal void RegisterManagedAssemblyValues()
    {
        if (Lua == null)
            return;

        LogHelper.Information("Registering managed game globals...");
        int count = 0;

        IReadOnlyDictionary<string, StatefulAssemblyItemEntry> globalsToRegister =
            GameGlobalsManager.Instance.GetManagedAssemblyStateManager().ManagedGlobals;

        foreach (KeyValuePair<string, StatefulAssemblyItemEntry> entry in globalsToRegister)
        {
            if (!entry.Value.Exposed)
                continue;

            // Try to resolve the generic value type T from IAssemblyGetSet<T>.
            // This handles ManagedAssemblyImmediate<T>, ManagedAssemblyDisplacement<T>, and their Multi variants.
            Type? valueType = TryGetAssemblyGetSetValueType(entry.Value.Site);

            if (valueType != null)
            {
                // Wrap in the proxy so Lua's double coercion is handled transparently.
                Lua[entry.Key] = new LuaAssemblyGlobalProxy(entry.Value.Site, valueType);
                LogHelper.Information($"Registering [{entry.Key}] as LuaAssemblyGlobalProxy<{valueType.Name}>");
            }
            else
            {
                // Fallback: register the raw item (e.g. custom IStatefulAssemblyItem implementations).
                Lua[entry.Key] = entry.Value.Site;
                LogHelper.Warning($"Registering [{entry.Key}] directly (could not resolve value type - Lua type coercion may fail)");
            }

            count++;
        }

        LogHelper.Information($"Registered {count} managed game globals.");
    }

    /// <summary>
    /// Walks the type hierarchy of <paramref name="item"/> to find the first closed <c>IAssemblyGetSet&lt;T&gt;</c> implementation and returns <c>T</c>.
    /// Returns <see langword="null"/> if none is found.
    /// </summary>
    private static Type? TryGetAssemblyGetSetValueType(object item)
    {
        Type assemblyGetSetOpenGeneric = typeof(IAssemblyValueSite<>);

        foreach (Type iface in item.GetType().GetInterfaces())
        {
            if (!iface.IsGenericType)
                continue;

            if (iface.GetGenericTypeDefinition() == assemblyGetSetOpenGeneric)
                return iface.GetGenericArguments()[0];
        }

        return null;
    }

    /// <summary>
    /// Applies the Lua sandbox by removing dangerous standard library access and replacing
    /// the require function with a context-aware sandboxed version.
    /// </summary>
    /// <param name="context">The execution context for I/O operations.</param>
    /// <param name="contextRootDirectory">Optional root directory for file-based contexts.</param>
    internal void PrepareVMSandbox(
        LuaExecutionContext context = LuaExecutionContext.MapArchive,
        string? contextRootDirectory = null,
        string? modGuid = null)
    {
        if (Lua == null)
            return;

        LogHelper.Information($"Preparing VM Sandbox with context: {context}");

        // Set the I/O execution context
        LuaIO.SetExecutionContext(context, contextRootDirectory, modGuid);

        // Remove dangerous standard library access.
        Lua.DoString(@"
os = nil
io = nil
package = nil
debug = nil
dofile = nil
loadfile = nil
import = function () end
");

        // Replace require with a context-aware sandboxed version
        Lua.DoString(@"
-- Private cache for loaded modules. Not exposed globally.
local loaded_modules = {}

-- Sandboxed require function that respects the execution context
require = function(module_name)
    -- Check if the module is already cached
    if loaded_modules[module_name] then
        return loaded_modules[module_name]
    end

    -- Convert module name to file path (e.g., ""utils.helpers"" -> ""utils/helpers.lua"")
    local file_path = string.gsub(module_name, ""%."", ""/"") .. "".lua""

    -- Use the context-aware IO function to get the file
    -- This function knows about the execution context (MapArchive, LordAI, or AssetMod)
    -- and will search the appropriate locations
    local file_content = IO_GetFileText(file_path)

    -- Check if the file was found
    if not file_content or file_content == """" then
        error(""Module '"" .. module_name .. ""' not found at path: "" .. file_path)
    end

    -- Load the script content into a function chunk
    local chunk, err = load(file_content, ""@"" .. file_path)
    if not chunk then
        error(""Error loading module '"" .. module_name .. ""': "" .. tostring(err))
    end

    -- Execute the chunk to load the module
    -- Because this require is running in the sandbox, the chunk inherits the same environment
    local success, result = pcall(chunk)
    if not success then
        error(""Error executing module '"" .. module_name .. ""': "" .. tostring(result))
    end

    -- Cache the result (use 'true' if module returns nothing)
    local module_result = result or true
    loaded_modules[module_name] = module_result

    return module_result
end

-- Helper function to clear the module cache (useful for development/debugging)
require_clear_cache = function()
    loaded_modules = {}
end

-- Helper function to check if a module is loaded
require_is_loaded = function(module_name)
    return loaded_modules[module_name] ~= nil
end
");

        Lua.DoString(@"
-- Creates a require closure bound to one asset mod's indexed namespace.
__create_mod_require = function(mod_guid, env)
    local loaded = {}
    return function(module_name)
        if loaded[module_name] then
            return loaded[module_name]
        end

        local file_path = string.gsub(module_name, ""%."", ""/"") .. "".lua""
        local content = IO_GetModFileText(mod_guid, ""Scripts/"" .. file_path)
        if not content or content == """" then
            content = IO_GetModFileText(mod_guid, file_path)
        end
        if not content or content == """" then
            error(""Module '"" .. module_name .. ""' was not found in mod '"" .. mod_guid .. ""'"")
        end

        local chunk, err = load(content, ""@"" .. mod_guid .. ""/"" .. file_path, ""t"", env)
        if not chunk then error(err) end
        local result = chunk()
        loaded[module_name] = result or true
        return loaded[module_name]
    end
end
");
    }

    /// <summary>
    /// Prepares the VM sandbox specifically for a Lord AI with its own isolated environment.
    /// This sets the execution context to allow the lord to access its own directory.
    /// </summary>
    /// <param name="lordDirectory">The absolute path to the lord's directory.</param>
    internal void PrepareLordAISandbox(string lordDirectory)
    {
        PrepareVMSandbox(LuaExecutionContext.LordAI, lordDirectory);
        LogHelper.Information($"Lord AI sandbox prepared for directory: [{lordDirectory}]");
    }

    /// <summary>
    /// Prepares the VM sandbox for an Asset Mod with access to its own directory
    /// and other registered asset mods.
    /// </summary>
    /// <param name="assetModDirectory">The absolute path to the asset mod's directory.</param>
    internal void PrepareAssetModSandbox(string assetModDirectory, string? modGuid = null)
    {
        PrepareVMSandbox(LuaExecutionContext.AssetMod, assetModDirectory, modGuid);
        LogHelper.Information($"Asset Mod sandbox prepared for directory: [{assetModDirectory}]");
    }

}
