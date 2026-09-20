using SHCDESE.API.Components.Assets;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;
using SHCDESE.Lua;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Represents the core mod manager for Asset Mods.
/// </summary>
public sealed class GameAssetModManager
{
    // ---------------------------------------------------------------------------------------
    // Singleton
    // ---------------------------------------------------------------------------------------

    private static readonly Lazy<GameAssetModManager> _lazy = new(() => new GameAssetModManager());
    public static GameAssetModManager Instance => _lazy.Value;

    // ---------------------------------------------------------------------------------------
    // Constants
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Relative path inside a mod folder that is treated as the Lua entry point.
    /// </summary>
    private const string LUA_INIT_RELATIVE_PATH = "Scripts/init.lua";

    /// <summary>
    /// Version used for mods whose <see cref="ModInfo.Version"/> is missing or unparsable.
    /// Any mod that does declare a usable version therefore wins over one that does not.
    /// </summary>
    private static readonly Version UNKNOWN_VERSION = new(0, 0, 0, 0);

    /// <summary>
    /// Characters that terminate the numeric part of a SemVer-style version string,
    /// e.g. the <c>-</c> in <c>1.4.0-beta.2</c> or the <c>+</c> in <c>1.4.0+build9</c>.
    /// </summary>
    private static readonly char[] VERSION_SUFFIX_SEPARATORS = ['-', '+', ' '];

    // ---------------------------------------------------------------------------------------
    // State
    // ---------------------------------------------------------------------------------------

    private readonly Dictionary<ModInfo, string> _registeredAssetDirectories = new();

    /// <summary>
    /// Index of everything that has actually been registered, keyed by mod GUID. 
    /// This is what makes registration idempotent per GUID: a second folder claiming an already-registered
    /// GUID is rejected instead of registering its assets, localizations, menu entry, XAML patches and mod-hash entry a second time.
    /// </summary>
    private readonly Dictionary<string, AssetModCandidate> _registeredByGuid = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Maps a mod's GUID to its absolute directory path for all mods that ship a
    /// <c>Scripts/init.lua</c>. Populated in <see cref="RegisterAssetMod"/> and consumed
    /// by <see cref="LuaManager"/> during map init.
    /// </summary>
    private readonly Dictionary<string, string> _pendingLuaMods = [];

    // ---------------------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------------------

    private GameAssetModManager()
    {

    }

    // ---------------------------------------------------------------------------------------
    // Public API
    // ---------------------------------------------------------------------------------------

    public IEnumerable<KeyValuePair<ModInfo, string>> GetRegisteredAssetDirectories() => _registeredAssetDirectories;

    /// <summary>
    /// Returns all mods that have a Lua entry point, keyed by GUID → absolute directory.
    /// The <see cref="LuaManager"/> calls this during map initialisation.
    /// </summary>
    internal IReadOnlyDictionary<string, string> GetPendingLuaMods() => _pendingLuaMods;

    /// <summary>
    /// Returns <see langword="true"/> when a mod with the given GUID has already been registered. GUIDs are compared case-insensitively.
    /// </summary>
    public bool IsRegistered(string guid) => !string.IsNullOrWhiteSpace(guid) && _registeredByGuid.ContainsKey(guid);

    /// <summary>
    /// Looks up the absolute directory of an already-registered mod by its GUID.
    /// </summary>
    /// <param name="guid">The GUID declared in the mod's <c>info.json</c>.</param>
    /// <param name="directory">The absolute path the mod was registered from.</param>
    /// <returns><see langword="true"/> if the GUID is registered.</returns>
    public bool TryGetRegisteredDirectory(string guid, out string directory)
    {
        if (!string.IsNullOrWhiteSpace(guid) && _registeredByGuid.TryGetValue(guid, out AssetModCandidate? registered))
        {
            directory = registered.Directory;
            return true;
        }

        directory = string.Empty;
        return false;
    }

    /// <summary>
    /// Finds and registers all directories and top-level .semod packages within BepInEx/plugins/.
    /// </summary>
    /// <remarks>
    /// Discovery runs in three phases. Every immediate subdirectory is first read and parsed without registering anything, then candidates that declare the same <c>info.json</c> GUID
    /// are collapsed down to the one with the highest <see cref="ModInfo.Version"/>, and only the surviving candidates are registered. 
    /// Losing copies are logged and otherwise ignored, so a mod that is installed twice no longer loads as well.
    ///
    /// Winners are registered by container name using ordinal comparison, matching BepInEx-style numeric/uppercase/lowercase folder ordering for both loose and packed mods.
    /// </remarks>
    public void RegisterAll()
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        string pluginsDirectory = IO.DirectoryHelpers.BepInExPluginsDirectory;

        if (!Directory.Exists(pluginsDirectory))
        {
            LogHelper.Warning($"BepInEx plugins directory does not exist at [{pluginsDirectory}] - no asset mods will be registered.");
            return;
        }

        // Phase 1: read metadata for every candidate source, registering nothing yet.
        List<AssetModCandidate> candidates = [];
        IEnumerable<string> packagePaths = Directory.EnumerateFiles(pluginsDirectory, "*", SearchOption.TopDirectoryOnly)
            .Where(path => string.Equals(Path.GetExtension(path), ".semod", StringComparison.OrdinalIgnoreCase));
        IEnumerable<string> candidatePaths = Directory.EnumerateDirectories(pluginsDirectory)
            .Concat(packagePaths)
            .OrderBy(path => Path.GetFileName(path), StringComparer.Ordinal);

        foreach (string candidatePath in candidatePaths)
        {
            AssetModCandidate? candidate = TryReadCandidate(candidatePath);
            if (candidate != null)
                candidates.Add(candidate);
        }

        // Phase 2: collapse duplicate GUIDs down to the highest declared version.
        Dictionary<string, AssetModCandidate> winners = new(StringComparer.OrdinalIgnoreCase);
        foreach (AssetModCandidate candidate in candidates)
        {
            if (!winners.TryGetValue(candidate.Info.GUID, out AssetModCandidate? incumbent))
            {
                winners[candidate.Info.GUID] = candidate;
                continue;
            }

            if (candidate.Version > incumbent.Version)
            {
                winners[candidate.Info.GUID] = candidate;
                LogShadowedCandidate(incumbent, candidate);
            }
            else
            {
                LogShadowedCandidate(candidate, incumbent);
            }
        }

        // Phase 3: register the winners in discovery order.
        foreach (AssetModCandidate candidate in candidates)
        {
            if (!winners.TryGetValue(candidate.Info.GUID, out AssetModCandidate? winner) || !ReferenceEquals(winner, candidate))
            {
                candidate.Source.Dispose();
                continue;
            }

            LogHelper.Information($"Loading Asset Mod: [{candidate.Directory}]");
            RegisterAssetModCore(candidate);
        }

        stopwatch.Stop();
        LogHelper.Information($"Asset mod discovery and indexing completed in {stopwatch.ElapsedMilliseconds} ms ({_registeredByGuid.Count} mod(s)).");
    }

    /// <summary>
    /// Registers a directory or .semod package as an Asset Mod, wiring up asset overrides, localisations, and when a <c>Scripts/init.lua</c> is present, a pending Lua registration that will be
    /// picked up by <see cref="LuaManager"/> on the next map load.
    /// </summary>
    /// <remarks>
    /// If a mod with the same <c>info.json</c> GUID is already registered, the call is ignored and a warning is logged..
    /// </remarks>
    /// <param name="directory">Absolute path to the mod folder.</param>
    public void RegisterAssetMod(string directory)
    {
        AssetModCandidate? candidate = TryReadCandidate(directory);
        if (candidate == null)
        {
            return;
        }

        RegisterAssetModCore(candidate);
    }

    internal void Unload()
    {
        _registeredAssetDirectories.Clear();
        _registeredByGuid.Clear();
        _pendingLuaMods.Clear();
    }

    // ---------------------------------------------------------------------------------------
    // Internals
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Reads and validates a mod folder's <c>info.json</c> without touching any registry.
    /// </summary>
    /// <returns>The parsed candidate, or <see langword="null"/> when the folder is unusable.</returns>
    private static AssetModCandidate? TryReadCandidate(string directory)
    {
        IModResourceSource? source = null;
        try
        {
            if (Directory.Exists(directory))
                source = new DirectoryResourceSource(directory);
            else if (File.Exists(directory) && string.Equals(Path.GetExtension(directory), ".semod", StringComparison.OrdinalIgnoreCase))
                source = new SEmodResourceSource(directory);
            else
                return null;

            return TryReadCandidate(source);
        }
        catch (Exception ex)
        {
            source?.Dispose();
            LogHelper.Error(ex, $"Failed to open asset mod source [{directory}]");
            return null;
        }
    }

    private static AssetModCandidate? TryReadCandidate(IModResourceSource source)
    {
        const string modInfoPath = "info.json";
        if (!source.Contains(modInfoPath))
        {
            LogHelper.Error($"Mod has no info.json. Expected one in: [{source.DisplayName}]");
            source.Dispose();
            return null;
        }

        ModInfo? modInfo;
        try
        {
            using Stream stream = source.OpenRead(modInfoPath);
            using StreamReader reader = new StreamReader(stream);
            string modInfoJson = reader.ReadToEnd();
            modInfo = JsonSerializer.Deserialize<ModInfo>(modInfoJson);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to parse mod json in [{source.DisplayName}]");
            source.Dispose();
            return null;
        }

        if (modInfo == null)
        {
            LogHelper.Error($"Failed to parse mod json in [{source.DisplayName}]");
            source.Dispose();
            return null;
        }

        if (string.IsNullOrWhiteSpace(modInfo.GUID))
        {
            LogHelper.Error($"Mod at [{source.DisplayName}] declares no GUID in its info.json and cannot be registered.");
            source.Dispose();
            return null;
        }

        if (source is SEmodResourceSource && modInfo.Manifest != ModManifest.Asset)
        {
            LogHelper.Error($"Packed mod [{source.DisplayName}] declares Manifest [{modInfo.Manifest}]. .semod packages support asset/Lua mods only.");
            source.Dispose();
            return null;
        }

        if (!TryParseModVersion(modInfo.Version, out Version version))
        {
            LogHelper.Warning($"Mod [{modInfo.GUID}] at [{source.DisplayName}] declares an unusable Version [{modInfo.Version ?? "<null>"}]. It will be treated as [{UNKNOWN_VERSION}] when resolving duplicates, so any copy with a valid version wins.");
        }

        return new AssetModCandidate(source, modInfo, version);
    }

    /// <summary>
    /// Performs the actual registration for a candidate that has already been parsed and validated, rejecting it if its GUID is already registered.
    /// </summary>
    private void RegisterAssetModCore(AssetModCandidate candidate)
    {
        ModInfo modInfo = candidate.Info;
        string directory = candidate.Directory;

        if (_registeredByGuid.TryGetValue(modInfo.GUID, out AssetModCandidate? existing))
        {
            if (candidate.Version > existing.Version)
            {
                LogHelper.Warning($"Asset mod [{modInfo.GUID}] is already registered from [{existing.Directory}] at version [{existing.DisplayVersion}]. The newer copy at [{directory}] (version [{candidate.DisplayVersion}]) was discovered too late to replace it and will be ignored. Delete the older copy if you want the newer one to load.");
            }
            else
            {
                LogHelper.Warning($"Asset mod [{modInfo.GUID}] is already registered from [{existing.Directory}] at version [{existing.DisplayVersion}]. Ignoring the duplicate at [{directory}] (version [{candidate.DisplayVersion}]).");
            }

            candidate.Source.Dispose();
            return;
        }

        // One pass indexes the entire mod and derives all specialized views.
        if (!GameAssetManagerAPI.Instance.RegisterModProvider(modInfo.GUID, candidate.Source))
            return;

        _registeredByGuid[modInfo.GUID] = candidate;
        _registeredAssetDirectories.Add(modInfo, directory);

        // Register optional logo
        string logoSpritePath = $"Override/Assets/GUI/Sprites/{modInfo.GUID}.png";
        if (candidate.Source.Contains(logoSpritePath))
        {
            Plugin.ViewModel.AddMod($"/Assets/GUI/Sprites/{modInfo.GUID}", modInfo);
        }
        else LogHelper.Warning($"Mod has no sprite logo at [{logoSpritePath}] in [{directory}] - This may be ignored.");

        // Register localization
        int localesLoaded = GameTranslateAPI.Instance.LoadModLocalizations(modInfo.GUID, directory);
        if (localesLoaded > 0)
        {
            LogHelper.Information($"Loaded {localesLoaded} localization file(s) for mod [{modInfo.Name}]");
        }

        // Register optional Lua entry point
        string luaInitPath = LUA_INIT_RELATIVE_PATH;
        if (candidate.Source.Contains(luaInitPath))
        {
            _pendingLuaMods[modInfo.GUID] = directory;
            LuaManager.Instance.RegisterAssetMod(modInfo.GUID, luaInitPath, directory);
            LogHelper.Information($"Registered Lua script for mod [{modInfo.Name}] at [{luaInitPath}]");
        }
        else LogHelper.Information($"Mod [{modInfo.Name}] has no Lua scripts ({LUA_INIT_RELATIVE_PATH} not found) - This is fine.");
    }

    /// <summary>
    /// Logs that <paramref name="skipped"/> lost the duplicate-GUID contest against <paramref name="winner"/>.
    /// </summary>
    private static void LogShadowedCandidate(AssetModCandidate skipped, AssetModCandidate winner)
    {
        if (skipped.Version == winner.Version)
        {
            LogHelper.Warning($"Skipping asset mod [{skipped.Info.Name}] at [{skipped.Directory}] because GUID [{skipped.Info.GUID}] is provided at the same version [{skipped.DisplayVersion}] by [{winner.Directory}]. Two identical copies are installed; remove one of them.");
            return;
        }

        LogHelper.Warning($"Skipping asset mod [{skipped.Info.Name} {skipped.DisplayVersion}] at [{skipped.Directory}] because a newer version exists ([{winner.Info.Name} {winner.DisplayVersion}] at [{winner.Directory}]).");
    }

    /// <summary>
    /// Converts the free-form <see cref="ModInfo.Version"/> string into a comparable <see cref="System.Version"/>.
    /// </summary>
    /// <remarks>
    /// Accepts an optional <c>v</c> prefix, a bare major (<c>"3"</c> becomes <c>3.0</c>), and SemVer pre-release or build metadata, which is discarded: <c>"1.4.0-beta.2"</c> compares
    /// equal to <c>"1.4.0"</c>. Anything else yields <see cref="UNKNOWN_VERSION"/>.
    /// </remarks>
    /// <returns><see langword="true"/> when the string produced a real version.</returns>
    internal static bool TryParseModVersion(string? rawVersion, out Version version)
    {
        version = UNKNOWN_VERSION;

        if (string.IsNullOrWhiteSpace(rawVersion))
        {
            return false;
        }

        string text = rawVersion!.Trim();

        if (text.Length > 1 && (text[0] == 'v' || text[0] == 'V'))
        {
            text = text.Substring(1);
        }

        int suffixIndex = text.IndexOfAny(VERSION_SUFFIX_SEPARATORS);
        if (suffixIndex >= 0)
        {
            text = text.Substring(0, suffixIndex);
        }

        // System.Version needs at least "major.minor".
        if (text.IndexOf('.') < 0)
        {
            text += ".0";
        }

        if (!Version.TryParse(text, out Version? parsed) || parsed == null)
        {
            return false;
        }

        version = parsed;
        return true;
    }

    /// <summary>
    /// A mod folder plus its parsed <c>info.json</c> metadata and comparable version.
    /// </summary>
    private sealed class AssetModCandidate(IModResourceSource source, ModInfo info, Version version)
    {

        /// <summary>Absolute path to the mod folder.</summary>
        public string Directory { get; } = source.ContainerPath;
        public IModResourceSource Source { get; } = source;
        public ModInfo Info { get; } = info;
        public Version Version { get; } = version;
        public string DisplayVersion => string.IsNullOrWhiteSpace(Info.Version) ? "<none>" : Info.Version;
    }
}
