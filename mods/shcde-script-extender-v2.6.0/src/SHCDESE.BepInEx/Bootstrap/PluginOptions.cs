using BepInEx;
using BepInEx.Configuration;
using Serilog.Events;
using System;

namespace SHCDESE.BepInEx.Bootstrap;

public partial class Plugin : BaseUnityPlugin
{
#pragma warning disable 8618

    /// <summary>
    /// This setting controls what other lobbies a script extender modified game can see.
    /// </summary>
    public ConfigEntry<bool> LobbyIsolation;

    /// <summary>
    /// Configuration entry to enable or disable some features that are only useful if debugging or otherwise analysing the game state, these
    /// may be impactful on performance and are not recommended for normal gameplay.
    /// </summary>
    public ConfigEntry<bool> DebugMode;

    /// <summary>
    /// Configuration entry to expose the CoreCLR (.NET runtime) to the Lua environment.
    /// </summary>
    /// <remarks>
    /// WARNING: This is a dangerous setting intended for advanced developers only. Enabling this gives Lua scripts
    /// the ability to interact with the entire .NET runtime, which can easily lead to game instability or crashes if misused.
    /// </remarks>
    public ConfigEntry<bool> ExposeCoreCLRToLua;

    /// <summary>
    /// Configuration entry to set the minimum game speed multiplier.
    /// </summary>
    public ConfigEntry<UInt16> MinGameSpeed;

    /// <summary>
    /// Configuration entry to set the maximum game speed multiplier.
    /// </summary>
    public ConfigEntry<UInt16> MaxGameSpeed;

    /// <summary>
    /// Configuration entry to set the increment value for game speed.
    /// </summary>
    public ConfigEntry<UInt16> GameSpeedChangeIncrement;

    /// <summary>
    /// A flag used to determine if this is the first time the user is running the script extender, used to show an informational message.
    /// </summary>
    public ConfigEntry<bool> FirstStart;

    /// <summary>
    /// The log level use for the internal library.
    /// </summary>
    public ConfigEntry<LogEventLevel> LogLevel;

    /// <summary>
    /// Show the mod logo in the main menu
    /// </summary>
    public ConfigEntry<bool> ShowLogo;

    /// <summary>
    /// Ordered, semicolon-separated list of .webm or .mp4 files played inside the main-menu diamond.
    /// </summary>
    public ConfigEntry<string> LogoVideos;

    /// <summary>Enable the decorative main-menu logo video player.</summary>
    public ConfigEntry<bool> LogoVideoEnabled;

    /// <summary>Mute decorative main-menu logo videos.</summary>
    public ConfigEntry<bool> LogoVideoMuted;

    /// <summary>Enable the animated static/vignette overlay on logo videos.</summary>
    public ConfigEntry<bool> LogoStaticEffect;

    /// <summary>Enable the animated scanline overlay on logo videos.</summary>
    public ConfigEntry<bool> LogoScanlineEffect;

    /// <summary>Opacity from 0.0 (transparent) to 1.0 (opaque) for the processed logo video.</summary>
    public ConfigEntry<double> LogoVideoOpacity;

    /// <summary>Chance from 0.0 to 1.0 of showing the rare logo caption per clip.</summary>
    public ConfigEntry<double> PokemonCaptionChance;

    /// <summary>
    /// The language provider to use for existing localization.
    /// Set to "detect" to auto-detect it.
    /// </summary>
    public ConfigEntry<string> LanguageProvider;

    /// <summary>
    /// Dumps the embedded encoded AIVs into binary blobs in the _EXPORT folder in the main game directory.
    /// </summary>
    public ConfigEntry<bool> DumpEmbeddedAIVs;

    /// <summary>
    /// Dumps the embedded AICs into binary blobs in the _EXPORT folder in the main game directory.
    /// </summary>
    public ConfigEntry<bool> DumpEmbeddedAICs;

    /// <summary>
    /// Generate a lua reference md file from existing DocFX summaries.
    /// </summary>
    public ConfigEntry<bool> GenerateLuaDocumentation;

    /// <summary>
    /// Should the SE make use of async logging? (might make debugging harder, otherwise its a perf. gain)
    /// </summary>
    public ConfigEntry<bool> AsyncLogging;

    /// <summary>
    /// Prefix log messages with the local date and time at which the event was created.
    /// </summary>
    public ConfigEntry<bool> LogTimestamps;

    /// <summary>
    /// Prefix log messages with the managed ID of the thread that created the event.
    /// </summary>
    public ConfigEntry<bool> LogThreadIds;

    /// <summary>
    /// Enables the native vectored-exception reporter and minidump writer.
    /// </summary>
    public ConfigEntry<bool> EnableNativeCrashHandler;

    /// <summary>
    /// Maximum number of native crash-handler files retained between launches.
    /// </summary>
    public ConfigEntry<int> MaxNativeCrashDumpFiles;

    /// <summary>
    /// Should the SE allow the chat in non-multiplayer matches?
    /// </summary>
    public ConfigEntry<bool> AllowChatInNonMultiplayer;

    /// <summary>
    /// Allows multiple instances of the game by bypassing the game's SteamAPI.RestartAppIfNecessary call which enforces
    /// that the game was launched through steam: something that you can only do once.
    /// Do note that this will likely result in steam not being able to initialize properly alongside steamworks, how you 
    /// deal with that will be up to you.
    /// </summary>
    public ConfigEntry<bool> AllowMultipleInstances;

    /// <summary>
    /// Enables some anti-tamper functionalities.
    /// </summary>
    public ConfigEntry<bool> EnableAntiTamper;

    /// <summary>
    /// Initializes steam early.
    /// </summary>
    public ConfigEntry<bool> EnableEarlySteamInitialization;

    /// <summary>
    /// Ignores any found dependency incompatibilities, mainly used for testing.
    /// </summary>
    public ConfigEntry<bool> IgnoreDependencyIncompatibilities;

#pragma warning restore 8618
    /// <summary>
    /// Binds all the plugin's configuration entries to the BepInEx configuration system.
    /// </summary>
    private void PluginConfigAwake()
    {
        DebugMode = Config.Bind("General", "DebugMode", false, "Toggle debug mode");
        ExposeCoreCLRToLua = Config.Bind("LUA", "ExposeCoreCLR", false, "Expose CoreCLR to LUA (Dangerous)");

        MinGameSpeed = Config.Bind("General", "MinGameSpeed", (UInt16)5, "Minimum game speed (do not go below 1)");
        MaxGameSpeed = Config.Bind("General", "MaxGameSpeed", (UInt16)1500, "Maximum game speed (do not go above 1500)");
        GameSpeedChangeIncrement = Config.Bind("General", "GameSpeedChangeIncrement", (UInt16)5, "Game speed change increment");

        LobbyIsolation = Config.Bind("General", "LobbyIsolation", true, "Toggle lobby isolation");

        AsyncLogging = Config.Bind("General", "AsyncLogging", true, "Should the script extender use async logging?");
        LogTimestamps = Config.Bind("Bootstrap", "LogTimestamps", true, "Prefix log messages with a local timestamp");
        LogThreadIds = Config.Bind("Bootstrap", "LogThreadIds", true, "Prefix log messages with the originating managed thread ID");

        EnableNativeCrashHandler = Config.Bind("Bootstrap", "EnableNativeCrashHandler", true, "Write native crash diagnostics and minidumps to BepInEx/crashdumps");
        MaxNativeCrashDumpFiles = Config.Bind("Bootstrap", "MaxNativeCrashDumpFiles", 5, "Maximum number of files retained in BepInEx/crashdumps. The oldest files are deleted during startup; set to 0 to clear all existing files.");
        AllowChatInNonMultiplayer = Config.Bind("General", "AllowChatInNonMultiplayer", false, "Should the script extender allow the chat in non-multiplayer matches");
        IgnoreDependencyIncompatibilities = Config.Bind("General", "IgnoreDependencyIncompatibilities", false, "Should the script extender ignore any mod dependency incompatibilities");

        LogLevel = Config.Bind("Bootstrap", "LogLevel", LogEventLevel.Information, "Log level");

        FirstStart = Config.Bind("Bootstrap", "FirstStart", true, "Set to false after first start");
        ShowLogo = Config.Bind("Visual", "ShowLogo", true, "Show the mod logo in the main menu");
        LogoVideoEnabled = Config.Bind("Visual", "LogoVideoEnabled", false, "Enable the decorative video player inside the main-menu logo");
        LogoVideos = Config.Bind("Visual", "LogoVideos", string.Empty, "Ordered, semicolon-separated .webm/.mp4 paths for the diamond logo video (for example: Assets/GUI/Video/SELogo/one.webm;Assets/GUI/Video/SELogo/two.mp4)");
        LogoVideoMuted = Config.Bind("Visual", "LogoVideoMuted", true, "Mute audio from decorative logo videos");
        LogoStaticEffect = Config.Bind("Visual", "LogoStaticEffect", true, "Overlay animated analog static and a vignette on the logo video only");
        LogoScanlineEffect = Config.Bind("Visual", "LogoScanlineEffect", true, "Overlay animated scanlines on the logo video only");
        LogoVideoOpacity = Config.Bind("Visual", "LogoVideoOpacity", 0.85, "Processed logo-video opacity from 0.0 (transparent) to 1.0 (opaque)");
        PokemonCaptionChance = Config.Bind("Visual", "PokemonCaptionChance", 0.03, "Chance from 0.0 to 1.0 of showing 'Not a pokemon!' below Zhuqiaomon for each logo-video loop");

        LanguageProvider = Config.Bind("Visual", "LanguageProvider", "detect", "Language provider to use. Set to detect for auto-detection.");

        DumpEmbeddedAIVs = Config.Bind("Export", "DumpEmbeddedAIVs", false, "Dumps the embedded encoded AIVs into binary blobs in the _EXPORT folder in the main game directory");
        DumpEmbeddedAICs = Config.Bind("Export", "DumpEmbeddedAICs", false, "Dumps the embedded encoded AICs into binary blobs in the _EXPORT folder in the main game directory");
        GenerateLuaDocumentation = Config.Bind("Export", "GenerateLuaDocumentation", false, "Generate a lua reference md file from existing DocFX summaries");

        AllowMultipleInstances = Config.Bind("General", "AllowMultipleInstances", false, "Allows multiple instances of the game by bypassing the game's SteamAPI.RestartAppIfNecessary call which enforces that the game was launched through steam: something that you can only do once.");

        EnableEarlySteamInitialization = Config.Bind("Steam", "EarlyInitialization", true, "Initializes steam early");

        EnableAntiTamper = Config.Bind("Security", "EnableAntiTamper", true, "Enables some Anti-Tamper functionalities to protect users. This only a very surface-level protection which can be bypassed easily. You still need to trust the author(s) of the mods you download. Do not rely on this. But do not disable this unless you know what youre doing.");
    }
}
