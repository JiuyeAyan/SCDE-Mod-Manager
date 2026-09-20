using BepInEx;
using Microsoft.Extensions.Logging;
using RedBird.Abstractions.Hooks;
using RedBird.Backends.NativeX64;
using RedBird.Core.Utilities;
using Serilog;
using Serilog.Core;
using Serilog.Extensions.Logging;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Logging;
using SHCDESE.API.LowLevel;
using SHCDESE.BepInEx.Logging;
using SHCDESE.DebugMenu;
using SHCDESE.Detours;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Logging;
using SHCDESE.Lua;
using SHCDESE.Lua.DocsGen;
using SHCDESE.ManagedHooks.Callbacks;
using SHCDESE.NativeHooks;
using SHCDESE.UI;
using SHCDESE.ViewModels;
using Steamworks;
using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using UUIMGUI.Core;

namespace SHCDESE.BepInEx.Bootstrap;

/// <summary>
/// The main entry point for the SHCDE Script Extender BepInEx plugin.
/// </summary>
/// <remarks>
/// This class is responsible for the entire bootstrap process, including setting up logging,
/// applying critical native hooks to intercept the game's library loading, and orchestrating the
/// initialization of all script extender managers and APIs once the game is ready.
/// </remarks>
[BepInDependency("uuimgui", BepInDependency.DependencyFlags.HardDependency)]
[BepInPlugin(PLUGIN_GUID, PLUGIN_NAME, PLUGIN_VERSION)]
public partial class Plugin : BaseUnityPlugin
{
    /// <summary>The unique identifier for this BepInEx plugin.</summary>
    public const string PLUGIN_GUID = "000shcdese";

    /// <summary>The public display name of this plugin.</summary>
    public const string PLUGIN_NAME = "SHCDE-SE";

    /// <summary>The current version of this plugin.</summary>
    public const string PLUGIN_VERSION = "1.0.0";

    /// <summary>
    /// A static reference to the singleton instance of this plugin.
    /// </summary>
    public static Plugin Instance;
    public LoggingLevelSwitch LogLevelSwitch;

    public ILoggerFactory LoggerFactory;
    public static MainMenuViewModel ViewModel;
    public static LobbyModSettingsHubViewModel ModSettingsHubViewModel = new LobbyModSettingsHubViewModel();

    private Plugin()
    {
        Instance = this;

        AntiTamper.AntiTamper.Instance.CreateSnapshot();
    }

    private string _currentCultureName = string.Empty;

    /// <summary>
    /// The primary entry point called by BepInEx when the plugin is loaded.
    /// </summary>
    private void Awake()
    {
        try
        {
            // Ensure dispatcher is available early
            _ = UnityMainThreadDispatcher.Instance;

            PluginConfigAwake();

            // Create Serilog logger with BepInEx sink
            LogLevelSwitch = new LoggingLevelSwitch
            {
                MinimumLevel = LogLevel.Value
            };

            LoggerConfiguration loggerConfig = new Serilog.LoggerConfiguration()
                .MinimumLevel.ControlledBy(LogLevelSwitch)
                .Enrich.FromLogContext();
            if (LogThreadIds.Value)
                loggerConfig.Enrich.With(new ThreadIdEnricher());

            if (AsyncLogging.Value)
                loggerConfig.WriteTo.Async(a => a.Sink(new BepInExSink(Logger, LogTimestamps.Value, LogThreadIds.Value)));
            else
                loggerConfig.WriteTo.Sink(new BepInExSink(Logger, LogTimestamps.Value, LogThreadIds.Value));

            Log.Logger = loggerConfig.CreateLogger();

            LoggerFactory = new SerilogLoggerFactory(Log.Logger, dispose: false);
            ModLoggerFactory.Initialize(LogLevelSwitch);

            // Init steam early (we dont really care if this works or not)
            if (EnableEarlySteamInitialization.Value)
                SteamAPI.Init();

            InitLanguageProviderEarly();
            LogHelper.Information($"Plugin {PLUGIN_NAME} is loading!");

            // Set detouring engine
            if (!HookBackends.TrySetDefault(NativeDetourBackend.Instance))
            //if (!HookBackends.TrySetDefault(RedBird.Backends.PolyHook2.PolyHook2Backend.Instance))
            {
                LogHelper.Fatal("Failed to set the default detouring backend.");
            }

            if (DumpEmbeddedAIVs.Value)
                GameAIManagerAPI.DumpEmbeddedAIVs();

            if (GenerateLuaDocumentation.Value)
                LuaApiDocGenerator.Generate();

            // Register mod folder with the windows PATH to find any native libs
            string pluginLibPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
            LibraryLoaderManager.Instance.AddLibrarySearchPath(pluginLibPath);
            LogHelper.Information($"Added to library search path: {pluginLibPath}");

            // Enforce crash-dump retention on every launch, even when the handler is disabled for this session.
            try
            {
                int deletedCrashDumpFiles = NativeCrashHandler.TrimCrashDumps(MaxNativeCrashDumpFiles.Value);
                if (deletedCrashDumpFiles > 0)
                    LogHelper.Information($"Deleted {deletedCrashDumpFiles} old native crash-handler file(s)");
            }
            catch (Exception ex)
            {
                // Retention failure must not prevent the rest of the script extender from starting.
                LogHelper.Error(ex, "Failed to trim old native crash-handler files");
            }

            // Experimental minidump writer
            if (EnableNativeCrashHandler.Value)
            {
                try
                {
                    string crashDumpDirectory = NativeCrashHandler.Install();
                    LogHelper.Information($"Native crash handler installed; dumps will be written to {crashDumpDirectory}");
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, "Failed to install the native crash handler");
                }
            }

            // Make sure our CrusaderDE.dll hooks get applied once the library gets loaded asap.
            API.LowLevel.CrusaderLibrary.Instance.LibraryLoaded += CrusaderLibrary_LibraryLoaded;

            // Notify PolyHook2 we want to see its logs
            PolyHook2.API.Logger.Log.Initialize();
            PolyHook2.API.Logger.Log.LogReceived += Log_LogReceived;

            // Force Noesis to init early
            NoesisUnity.InitCore();

            DetourManager.Instance.ApplyManagedEarly();

            // Init custom ViewModel aka Logo
            ViewModel = new MainMenuViewModel(LogoVideoEnabled.Value, LogoVideos.Value, LogoVideoMuted.Value, LogoStaticEffect.Value, LogoScanlineEffect.Value, LogoVideoOpacity.Value, PokemonCaptionChance.Value);
            GameXAMLManagerAPI.Instance.RegisterBinding("SEMainMenuContainer", ViewModel);
            ViewModel.IsSEMenuVisible = ShowLogo.Value ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;
            _ = ViewModel.CheckForUpdateAsync(PLUGIN_VERSION);

            // Init custom lobby mod options
            GameXAMLManagerAPI.Instance.RegisterBinding("SE_ModOptionsToggle", ModSettingsHubViewModel);
            GameXAMLManagerAPI.Instance.RegisterBinding("SE_ModOptionsModal", ModSettingsHubViewModel);

            if (EnableAntiTamper.Value)
                AntiTamper.AntiTamper.Instance.Compare();

            // Start the asset manager for this mod.
            GameAssetModManager.Instance.RegisterAll();
            if (LogoVideoEnabled.Value)
            {
                AnalogLogoMediaPlayer.PreloadShader();
            }
            GameXAMLManagerAPI.Instance.Setup();

            DetourManager.Instance.ApplyManaged();

            NoesisCallbacks.Install();

            UnityEngine.Application.quitting += Application_quitting;
            LoadNativeLibrary();

            ShowFirstTimeMessage();

            // Plugin startup logic
            LogHelper.Information($"Plugin {PLUGIN_NAME} is loaded!");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during plugin startup");
        }
    }

    /// <summary>
    /// When unity is closing, we need to restore the original game VEH for "clean" shutdown.
    /// Thanks FireFly.
    /// </summary>
    private static void Application_quitting()
    {
        LogHelper.Information($"Restoring original VEH");
        BulkVEHDetours.RestoreOriginalHandler();
        NativeCrashHandler.Uninstall();
    }

    private unsafe void LoadNativeLibrary()
    {
        string executableName = Path.GetFileNameWithoutExtension(Process.GetCurrentProcess().MainModule.FileName);

        IntPtr lpAddr = MinWinAPI.LoadLibraryA($"{executableName}_Data/Plugins/x86_64/CrusaderDE.dll");
        if (lpAddr == IntPtr.Zero)
        {
            LogHelper.Error($"CrusaderDE.dll not found at {executableName}_Data/Plugins/x86_64/CrusaderDE.dll");
            return;
        }
        LogHelper.Information($"CrusaderDE: {lpAddr.ToString("X16")}");

        MinWinAPI.MODULEINFO modInfo;
        if (!MinWinAPI.GetModuleInformation(Process.GetCurrentProcess().Handle, lpAddr, out modInfo, (uint)Marshal.SizeOf(typeof(MinWinAPI.MODULEINFO))))
        {
            LogHelper.Error($"Failed to retrieve module information for CrusaderDE.dll");
            return;
        }
        Log.Information($"Base address: 0x{modInfo.lpBaseOfDll.ToString("X16")}");
        Log.Information($"Module size: 0x{modInfo.SizeOfImage.ToString("X16")} bytes");

        ReadOnlySpan<byte> memory = new((byte*)modInfo.lpBaseOfDll, (int)modInfo.SizeOfImage);
        CrusaderLibrary.Instance.SignalLibraryLoaded(lpAddr, memory);
        CrusaderLibrary.Instance.RaiseLibraryLoaded();
    }

    /// <summary>
    /// Detect the soon to be used language code.
    /// </summary>
    internal void InitLanguageProviderEarly()
    {
        _currentCultureName = string.Equals(LanguageProvider.Value, "detect", StringComparison.InvariantCultureIgnoreCase) ? Steamworks.SteamApps.GetCurrentGameCultureName() : LanguageProvider.Value;
        GameAssetManagerAPI.Instance.CurrentLanguage = _currentCultureName;
        LogHelper.Information($"Set language provider to [{_currentCultureName}]");
    }

    /// <summary>
    /// Initialize the language provider. Allows auto-detect or forceful set.
    /// </summary>
    internal void InitLanguageProviderLate()
    {
        CultureInfo culture = new(_currentCultureName);

        LogHelper.Information($"Set language provider to {_currentCultureName}");
        LocalizationManager.Instance.SetCulture(culture);
        GameAssetManagerAPI.Instance.CurrentLanguage = _currentCultureName;
    }

    /// <summary>
    /// Callback for receiving log messages from the underlying PolyHook2 native library.
    /// </summary>
    private void Log_LogReceived(object sender, PolyHook2.API.Logger.LogEventArgs e)
    {
        Log.Debug($"PLH::{e.Level}: {e.Message}");
    }

    /// <summary>
    /// Displays a one-time message box to the user on the first run of the script extender.
    /// </summary>
    internal static void ShowFirstTimeMessage()
    {
        if (!Instance.FirstStart.Value)
            return;

        MinWinAPI.MessageBoxA(IntPtr.Zero, @"
You're playing Stronghold Crusader Definitive Edition with SHCDE Script Extender (SHCDE:SE)!

Website: gitlab.com/rawra-stronghold-crusader/shcde-script-extender
Make sure to report any bugs that may arise.

Caution: 
- Do not play maps from untrusted sources.
- Only use the script extender if you downloaded it from the official sources (GitLab, NexusMods, ModDB).

This message will only appear once.
Enjoy!", "Script Extender", 0x40);

        Instance.FirstStart.Value = false;
    }

    /// <summary>
    /// The primary event handler that is called once the game's main library (`CrusaderDE.dll`) has been loaded into memory.
    /// </summary>
    /// <param name="context">The loaded library context.</param>
    /// <remarks>
    /// This method is the entry point for all game-specific initialization. 
    /// It is responsible for finding game globals, applying all game-function detours, and setting up all high-level APIs like the Lua manager.
    /// </remarks>
    private void CrusaderLibrary_LibraryLoaded(CrusaderLibraryLoadContext context)
    {
        try
        {
            LogHelper.Information($"Initializing native-side...");

            GameGlobalsManager.Instance.FindGameGlobals(context.Memory, context.Region);

            DetourManager.Instance.ApplyNative(context.Memory, context.Region);
            //DetourManager.Instance.ApplyManaged();

            GameMapArchiveManagerAPI.InitializeSubscribers();
            LuaManager.InitializeSubscribers();
            GameNetworkAPI.InitializeSubscribers();

            GamePlayerManagerAPI.InitializeSubscribers();
            GameUnitManagerAPI.InitializeSubscribers();
            GameBuildingManagerAPI.InitializeSubscribers();
            GameMetadataManagerAPI.InitializeSubscribers();
            GameSoundManagerAPI.InitializeSubscribers();
            GameTranslateAPI.InitializeSubscribers();
            GameTriggerManager.InitializeSubscribers();
            GameTimeManagerAPI.InitializeSubscribers();
            GameAfterImageManager.InitializeSubscribers();
            GameBundleManagerAPI.InitializeSubscribers();

            DebugMenuManager.Instance.Initialize();

            // Start the Map Mod Manager setup
            MapModManager.Instance.Setup();

            InitLanguageProviderLate();

            if (DumpEmbeddedAICs.Value)
                GameAIManagerAPI.Instance.DumpEmbeddedAICs();

            /*
            unsafe
            {
                UInt32* pArr = (UInt32*)((UInt64)GameGlobalsManager.Instance.CrusaderLibraryHandle + 0x2E4A70);
                var count = Enum.GetValues(typeof(Enums.eStructs)).Length;
                for (int i = 0; i < count; i++)
                {
                    //eStructs.STRUCT_CHURCH3 = 0x26h, 38d
                    LogHelper.Information($"eStruct[{(Enums.eStructs)(i)}]={pArr[i]}");
                }
            }
            */
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during LibraryLoad event");
        }
        finally
        {
            LogHelper.Information($"Native-side initialization done");
        }
    }
}
