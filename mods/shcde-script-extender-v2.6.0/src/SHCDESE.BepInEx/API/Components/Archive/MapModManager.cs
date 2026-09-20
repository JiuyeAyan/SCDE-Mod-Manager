using R3;
using SHCDESE.API.Components.ModManager;
using SHCDESE.EventAPI;
using SHCDESE.Interop;
using SHCDESE.IO;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading;

namespace SHCDESE.API.Components.Archive;

/// <summary>
/// Map Mod Manager. Takes care of loading BepInEx mods hidden in Map Archives.
/// </summary>
internal sealed class MapModManager
{
    private static readonly Lazy<MapModManager> _lazy = new(() => new MapModManager());
    public static MapModManager Instance => _lazy.Value;

    /// <summary>
    /// Consecutive failed deployments before we stop trying. Without this, a
    /// deployment that launches successfully but never applies its files would
    /// restart the game on every boot.
    /// </summary>
    private const int MaxDeployAttempts = 3;

    private readonly string _seDirectory;
    private readonly string _stagingDirectory;
    private readonly string _deleteManifestPath;
    private readonly string _deployAttemptsPath;

    private int _initialized = 0;

    private MapModManager()
    {
        _seDirectory = Path.Combine(DirectoryHelpers.GameDirectory, "_SE");
        _stagingDirectory = Path.Combine(_seDirectory, ".staging");
        _deleteManifestPath = Path.Combine(_stagingDirectory, "delete_list.txt");
        _deployAttemptsPath = Path.Combine(_seDirectory, ".deploy_attempts");
    }

    internal void Setup()
    {
        // Staging is always rebuilt from the workshop, so leftovers from a failed
        // or interrupted deployment are discarded rather than reused.
        if (Directory.Exists(_stagingDirectory))
        {
            try
            {
                Directory.Delete(_stagingDirectory, true);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Exception during staging cleanup (this might be okay)");
            }
        }
        InitializeSubscribers();
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        SteamworksR3EventHooks.OnSteamworksInitialized.Observable.Subscribe(OnSteamworksInitialized);
    }

    private static void OnSteamworksInitialized(EventAPI.Steamworks.SteamworksInitializedEventArgs e)
    {
        if (e.Phase == EventHookPhase.Post && SteamManager.s_instance.m_bInitialized)
        {
            Instance.TryUpdateModsFromRemote();
        }
    }

    /// <summary>
    /// True when this Windows process is running inside Wine/Proton
    /// </summary>
    private static bool IsWine()
    {
        try
        {
            IntPtr ntdll = MinWinAPI.GetModuleHandle("ntdll.dll");
            return ntdll != IntPtr.Zero && MinWinAPI.GetProcAddress(ntdll, "wine_get_version") != IntPtr.Zero;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during IsWine check");
            return false;
        }
    }

    /// <summary>
    /// Converts a Wine Z:-drive path to its host path. Returns false for any other drive.
    /// </summary>
    private static bool TryToHostPath(string winPath, out string unixPath)
    {
        unixPath = string.Empty;
        if (string.IsNullOrEmpty(winPath) || winPath.Length < 2 || winPath[1] != ':')
            return false;
        if (char.ToUpperInvariant(winPath[0]) != 'Z')
            return false;

        unixPath = winPath[2..].Replace('\\', '/');
        if (!unixPath.StartsWith("/"))
            unixPath = "/" + unixPath;
        return true;
    }

    /// <summary>
    /// Tries to extract all map mods to the game directory.
    /// Local Maps saved in: C:\Users\Rawra\AppData\LocalLow\Firefly Studios\Stronghold Crusader Definitive Edition\Maps
    /// Steam Maps saved in: h:\steamlibrary\steamapps\workshop\content\3024040\3588254906\ruins of atlantis [arena][8 player][ki battle][balanced].map
    /// Target: H:\SteamLibrary\steamapps\common\Stronghold Crusader Definitive Edition
    /// </summary>
    private void TryUpdateModsFromRemote()
    {
        // If local does not exist yet: Copy the BepInEx folder into the game root
        // If exists already: do version check.
        // Copy the info.json into the game root/_SE/<map-file-name> as folder/ folder
        // ^ we use this as a way to do version diff between currently installed mods and the version in the remote steam workshop depot.
        // ^ where we only copy over the files from steam-remote to local if the remote version is newer than our local one.
        LogHelper.Information("Trying to update mods from steam-remote...");

        List<string> workshopSubscribedItemPaths = Platform_Workshop.Instance.GetListOfSubscribedItemsPaths();

        LogHelper.Information($"Retrieved {workshopSubscribedItemPaths.Count} items");
        bool stateChanged = false;

        // Check for Uninstalls (Unsubscribes)
        if (DetectAndStageUninstalls(workshopSubscribedItemPaths))
        {
            stateChanged = true;
        }

        // Check for Installs/Updates
        foreach (string workshopItemPath in workshopSubscribedItemPaths)
        {
            string workshopMapPath = GetFirstMapFileInDirectory(workshopItemPath);
            if (string.IsNullOrEmpty(workshopMapPath))
                continue;

            if (MapArchive.TryLoad(workshopMapPath, out MapArchive? archive) && (archive.Info?.Manifest == ModManifest.BepInEx || archive.Info?.Manifest == ModManifest.Asset))
            {
                if (TryStageModUpdate(workshopMapPath, archive))
                    stateChanged = true;
            }
        }

        if (!stateChanged)
        {
            // Nothing pending means the previous deployment (if any) landed.
            ResetDeployAttempts();
            return;
        }

        int attempts = ReadDeployAttempts();
        if (attempts >= MaxDeployAttempts)
        {
            LogHelper.Error($"Mod deployment has failed {attempts} times in a row. Not retrying. " +
                            $"Delete [{_deployAttemptsPath}] to try again.");
            return;
        }

        WriteDeployAttempts(attempts + 1);

        MinWinAPI.MessageBoxA(IntPtr.Zero, "Mods have been updated or removed.\nThe game will restart to apply changes.", "Script Extender", 0);
        LaunchUpdaterAndExit();
    }

    /// <summary>
    /// Number of consecutive deployments that were started but never confirmed
    /// complete. Reset once a run finds nothing left to deploy.
    /// </summary>
    private int ReadDeployAttempts()
    {
        try
        {
            return File.Exists(_deployAttemptsPath) && int.TryParse(File.ReadAllText(_deployAttemptsPath).Trim(), out int value) ? value : 0;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Could not read deploy attempt counter");
            return 0;
        }
    }

    private void WriteDeployAttempts(int value)
    {
        try
        {
            Directory.CreateDirectory(_seDirectory);
            File.WriteAllText(_deployAttemptsPath, value.ToString());
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Could not write deploy attempt counter");
        }
    }

    private void ResetDeployAttempts()
    {
        try
        {
            if (File.Exists(_deployAttemptsPath))
            {
                File.Delete(_deployAttemptsPath);
                LogHelper.Information("Previous mod deployment completed successfully.");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Could not clear deploy attempt counter");
        }
    }

    /// <summary>
    /// Compares locally installed mods (in _SE/*.json) against the active subscription list.
    /// If a mod is found locally but not in the subscription list, it is staged for deletion.
    /// </summary>
    private bool DetectAndStageUninstalls(List<string> subscribedItemPaths)
    {
        LogHelper.Information($"Scanning for uninstalls in: [{_seDirectory}]");

        if (!Directory.Exists(_seDirectory))
        {
            LogHelper.Warning($"_SE Directory not found. Skipping uninstall check.");
            return false;
        }

        // Build a HashSet of currently subscribed map filenames
        HashSet<string> subscribedMapNames = new(StringComparer.OrdinalIgnoreCase);
        foreach (string path in subscribedItemPaths)
        {
            string mapFile = GetFirstMapFileInDirectory(path);
            if (!string.IsNullOrEmpty(mapFile))
            {
                subscribedMapNames.Add(Path.GetFileNameWithoutExtension(mapFile));
            }
        }

        // Get all local installed mod manifests
        string[] localManifests = Directory.GetFiles(_seDirectory, "*.json");

        LogHelper.Information($"Found {localManifests.Length} local manifests.");

        bool uninstallsFound = false;
        List<string> pathsToDelete = [];

        foreach (string manifestPath in localManifests)
        {
            string mapName = Path.GetFileNameWithoutExtension(manifestPath);

            // If the local map name is NOT in the subscribed list, its an orphan.
            if (!subscribedMapNames.Contains(mapName))
            {
                LogHelper.Information($"Detected unsubscribe for mod: [{mapName}]. Staging for removal.");

                pathsToDelete.Add(manifestPath);

                try
                {
                    ModInfo? info = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(manifestPath));
                    if (info != null && !string.IsNullOrWhiteSpace(info.GUID))
                    {
                        // Reject anything that is not a plain folder name; the manifest is mod-supplied and is handed to a recursive delete.
                        if (info.GUID.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0
                            || info.GUID.Contains("..")
                            || info.GUID.Contains('/')
                            || info.GUID.Contains('\\'))
                        {
                            LogHelper.Warning($"Refusing unsafe GUID [{info.GUID}] from [{mapName}]; skipping folder deletion.");
                        }
                        else
                        {
                            string pluginFolder = Path.Combine(DirectoryHelpers.GameDirectory, "BepInEx", "plugins", info.GUID);

                            if (Directory.Exists(pluginFolder))
                            {
                                pathsToDelete.Add(pluginFolder);
                                LogHelper.Information($"Scheduled directory deletion: [{pluginFolder}]");
                            }
                            else
                            {
                                LogHelper.Warning($"Could not find plugin folder for GUID [{info.GUID}]. It may already be deleted.");
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, $"Failed to parse manifest for [{mapName}] during uninstall");
                }

                uninstallsFound = true;
            }
        }

        if (uninstallsFound && pathsToDelete.Count > 0)
        {
            try
            {
                if (!Directory.Exists(_stagingDirectory))
                    Directory.CreateDirectory(_stagingDirectory);

                File.WriteAllLines(_deleteManifestPath, pathsToDelete);
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Failed to write delete manifest.");
            }
        }

        return uninstallsFound;
    }


    private string GetFirstMapFileInDirectory(string path)
    {
        if (!Directory.Exists(path))
            return string.Empty;

        return Directory.EnumerateFiles(path, "*.map").FirstOrDefault() ?? string.Empty;
    }


    /// <summary>
    /// Extracts the mod to a staging folder. Does NOT touch the live BepInEx
    /// folder or the installed version file directly.
    /// </summary>
    private bool TryStageModUpdate(string path, MapArchive archive)
    {
        string fileName = Path.GetFileNameWithoutExtension(path);
        LogHelper.Information($"Querying workshop mod (MAS): [{fileName}], full: [{path}]");

        if (!ShouldUpdate(fileName, archive))
        {
            LogHelper.Information($"Skipping: [{fileName}]");
            return false;
        }

        try
        {
            // Prepare Staging Directory
            if (!Directory.Exists(_stagingDirectory))
                Directory.CreateDirectory(_stagingDirectory);

            // Extract Content to Staging
            // We extract "BepInEx" from the zip to "_SE/.staging/BepInEx"
            string stagingDestination = Path.Combine(_stagingDirectory, "BepInEx");

            LogHelper.Information($"Staging files for [{fileName}]...");

            if (!archive.TryExtractFolder("BepInEx", stagingDestination, true))
            {
                throw new Exception("Archive extraction to staging failed.");
            }

            if (archive.Info != null)
            {
                string infoJson = archive.TryReadTextFile("info.json", true);
                if (!string.IsNullOrEmpty(infoJson))
                {
                    string pluginFolderName = archive.Info.GUID;
                    if (string.IsNullOrEmpty(archive.Info.GUID))
                    {
                        LogHelper.Error($"info.json does not contain GUID!");
                        return false;
                    }

                    string pluginStagingPath = Path.Combine(stagingDestination, "plugins", pluginFolderName);
                    Directory.CreateDirectory(pluginStagingPath);

                    string infoJsonPath = Path.Combine(pluginStagingPath, "info.json");
                    File.WriteAllText(infoJsonPath, infoJson);
                    LogHelper.Information($"Staged info.json to [{infoJsonPath}]");
                }
                else
                {
                    LogHelper.Warning($"Could not read info.json from archive for [{fileName}]");
                }
            }

            string pendingInfoDirectory = Path.Combine(_stagingDirectory, "_SE");
            Directory.CreateDirectory(pendingInfoDirectory);

            string pendingInfoPath = Path.Combine(pendingInfoDirectory, fileName + ".json");
            File.WriteAllText(pendingInfoPath, JsonSerializer.Serialize(archive.Info, new JsonSerializerOptions() { WriteIndented = true }));
            LogHelper.Information($"Staged mod info at [{pendingInfoPath}]");

            LogHelper.Information($"Successfully staged mod [{fileName}]");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to stage mod [{fileName}].");

            // Cleanup specific staging mess if possible, though global cleanup handles it on next run
            return false;
        }
    }

    private bool ShouldUpdate(string fileName, MapArchive archive)
    {
        string localInfoFilePath = Path.Combine(_seDirectory, fileName) + ".json";
        LogHelper.Information($"Querying local mod info: [{localInfoFilePath}]");

        if (!File.Exists(localInfoFilePath))
        {
            LogHelper.Information($"File does not exist (yet): [{localInfoFilePath}]");
            return true;
        }

        ModInfo? localInfo = JsonSerializer.Deserialize<ModInfo>(File.ReadAllText(localInfoFilePath));
        if (localInfo == null)
        {
            LogHelper.Warning($"Failed to parse local mod info: [{localInfoFilePath}]. Forcing update.");
            return true;
        }

        string localV = localInfo.Version ?? "0.0.0";
        string remoteV = archive.Info?.Version ?? "0.0.0";

        LogHelper.Information($"Local mod info version: [{localV}], Remote version: [{remoteV}]");

        try
        {
            SimpleSemVer vLocal = new(localV);
            SimpleSemVer vRemote = new(remoteV);

            // If Local is Greater OR Equal to Remote, we do NOT update.
            if (vLocal.AtleastMajorMinorPatch(vRemote))
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Version parsing failed. Defaulting to Update.");
            return true;
        }

        return true;
    }

    /// <summary>
    /// Arguments shared by both updater backends, resolved once before dispatch.
    /// </summary>
    private readonly struct UpdaterContext(string scriptDirectory, string stagingDirectory, string targetDirectory, string deleteManifest, string gameExe, int gamePid)
    {
        public string ScriptDirectory { get; } = scriptDirectory;
        public string StagingDirectory { get; } = stagingDirectory;
        public string TargetDirectory { get; } = targetDirectory;
        public string DeleteManifest { get; } = deleteManifest;   // "" when absent
        public string GameExe { get; } = gameExe;
        public int GamePid { get; } = gamePid;
    }


    /// <summary>
    /// Quotes an argument for the Windows command-line parser. Wine applies the same rules before handing argv to a native binary, so this is used by both backends.
    /// </summary>
    private static string Quote(string value)
    {
        string trimmed = value.TrimEnd('\\');
        return "\"" + trimmed.Replace("\"", "\\\"") + "\"";
    }

    /// <summary>
    /// Locates the host <c>sh</c> binary through the Wine Z: mapping.
    /// </summary>
    private static string? ResolveHostShell()
    {
        foreach (string candidate in new[] { @"Z:\bin\sh", @"Z:\usr\bin\sh" })
        {
            if (File.Exists(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// Stages the post-exit updater and terminates the game so it can run.
    /// </summary>
    private void LaunchUpdaterAndExit()
    {
        try
        {
            string assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;
            string scriptDir = Path.Combine(assemblyDir, "data");

            Process self = Process.GetCurrentProcess();
            string gameExe = self.MainModule?.FileName ?? "Stronghold Crusader Definitive Edition.exe";

            string staging = _stagingDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string target = DirectoryHelpers.GameDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string manifest = File.Exists(_deleteManifestPath) ? _deleteManifestPath : string.Empty;

            UpdaterContext ctx = new(scriptDir, staging, target, manifest, gameExe, self.Id);

            bool launched = IsWine()
                ? LaunchUpdaterAndExit_Unix(ctx)
                : LaunchUpdaterAndExit_Win32(ctx);

            if (!launched)
            {
                // Staged files stay on disk but are discarded and rebuilt next start. The version file was never committed, so nothing should be lost.
                LogHelper.Error("Updater could not be launched. Updates will be retried on next start.");
                return;
            }

            LogHelper.Information("Updater launched. Terminating game so files can be replaced.");
            self.Kill();
        }
        catch (Exception ex)
        {
            LogHelper.Fatal(ex, "Failed to launch updater script. Updates will not be applied.");
        }
    }

    /// <summary>
    /// Windows backend. Runs <c>mod-updater.ps1</c> under pwsh, falling back to Windows PowerShell.
    /// </summary>
    private bool LaunchUpdaterAndExit_Win32(in UpdaterContext ctx)
    {
        string scriptPath = Path.Combine(ctx.ScriptDirectory, "mod-updater.ps1");

        if (!File.Exists(scriptPath))
        {
            LogHelper.Error($"Updater script not found at: [{scriptPath}]");
            return false;
        }

        string arguments =
            $"-ExecutionPolicy Bypass -File {Quote(scriptPath)} " +
            $"-GamePid {ctx.GamePid} " +
            $"-StagingDir {Quote(ctx.StagingDirectory)} " +
            $"-TargetDir {Quote(ctx.TargetDirectory)} " +
            $"-GameExe {Quote(ctx.GameExe)} " +
            $"-DeleteManifest {Quote(ctx.DeleteManifest)}";

        foreach (string shell in new[] { "pwsh", "powershell" })
        {
            ProcessStartInfo psi = new()
            {
                FileName = shell,
                Arguments = arguments,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            };

            try
            {
                Process.Start(psi);
                LogHelper.Information($"Updater started via [{shell}].");
                return true;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                LogHelper.Warning($"[{shell}] is not available; trying next interpreter.");
            }
        }

        LogHelper.Error("Neither pwsh nor powershell could be started.");
        return false;
    }

    /// <summary>
    /// Wine/Proton backend. Escapes the prefix and runs <c>mod-updater.sh</c>
    /// through the host shell, translating all paths to host form.
    /// </summary>
    private bool LaunchUpdaterAndExit_Unix(in UpdaterContext ctx)
    {
        string scriptPath = Path.Combine(ctx.ScriptDirectory, "mod-updater.sh");

        if (!File.Exists(scriptPath))
        {
            LogHelper.Error($"Updater script not found at: [{scriptPath}]");
            return false;
        }

        string? shell = ResolveHostShell();
        if (shell is null)
        {
            LogHelper.Error("Running under Wine but no host sh binary is reachable via the Z: drive.");
            return false;
        }

        if (!TryToHostPath(scriptPath, out string scriptHost)
            || !TryToHostPath(ctx.StagingDirectory, out string stagingHost)
            || !TryToHostPath(ctx.TargetDirectory, out string targetHost))
        {
            LogHelper.Error("Game paths are not on the Wine Z: drive; cannot translate to host paths. Updates will not be applied.");
            return false;
        }

        string manifestHost = string.Empty;
        if (!string.IsNullOrEmpty(ctx.DeleteManifest) && !TryToHostPath(ctx.DeleteManifest, out manifestHost))
        {
            LogHelper.Warning("Delete manifest path could not be translated; uninstalls will be skipped.");
            manifestHost = string.Empty;
        }

        string processName = Path.GetFileName(ctx.GameExe);
        string arguments =
            $"{Quote(scriptHost)} " +
            $"--proc {Quote(processName)} " +
            $"--staging {Quote(stagingHost)} " +
            $"--target {Quote(targetHost)} " +
            $"--delete-manifest {Quote(manifestHost)}";

        ProcessStartInfo psi = new()
        {
            FileName = shell,
            Arguments = arguments,
            UseShellExecute = false
        };

        try
        {
            Process.Start(psi);
            LogHelper.Information($"Updater started via host shell [{shell}].");
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to start host shell [{shell}].");
            return false;
        }
    }
}