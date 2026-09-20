using ICSharpCode.SharpZipLib.Zip;
using R3;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.SaveData;
using SHCDESE.API.Components.Timer;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.IO;
using SHCDESE.Logging;
using System;
using System.IO;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for interacting with custom zip archives embedded in Stronghold Crusader map files.
/// This class is implemented as a singleton and manages the lifecycle of the currently loaded map archive.
/// It automatically loads archives when a map or savegame is loaded by subscribing to game events.
/// Archives are created on-demand when mods need to save data.
/// </summary>
public sealed class GameMapArchiveManagerAPI
{
    private static readonly Lazy<GameMapArchiveManagerAPI> _lazy = new(() => new GameMapArchiveManagerAPI());
    public static GameMapArchiveManagerAPI Instance => _lazy.Value;

    // the currently loaded map archive (if any)
    private MapArchive? _mapArchive;

    // Track the current file context
    private string? _currentFilePath;
    private bool _currentFileIsSaveFile;

    public const int DEFAULT_MINIMUM_MAP_FILE_SIZE = 10000;         // Game default: 9.77KB
    public const int DEFAULT_MAXIMUM_MAP_FILE_SIZE = 0x7A12000;     // Game default: 8.58MB, new default: 128MB

    public const string DEFAULT_PREVIEW_FILE_NAME = "preview.png";

    private int _initialized = 0;

    private GameMapArchiveManagerAPI()
    {

    }

    internal void Unload()
    {
        _mapArchive?.Dispose();
    }

    /// <summary>
    /// Gets the currently loaded map archive, if one exists.
    /// </summary>
    /// <returns>The active <see cref="MapArchive"/> instance, or <c>null</c> if no map with an archive is loaded.</returns>
    public MapArchive? GetMapArchive()
    {
        return _mapArchive;
    }

    /// <summary>
    /// Gets whether the currently loaded file is a save file (.sav) or a map file (.map).
    /// </summary>
    /// <returns>True if the current file is a save file, false if it's a map file.</returns>
    public bool IsCurrentFileSaveFile()
    {
        return _currentFileIsSaveFile;
    }

    /// <summary>
    /// Gets the file path of the currently loaded map/save.
    /// </summary>
    public string? GetCurrentFilePath()
    {
        return _currentFilePath;
    }

    /// <summary>
    /// Sets the current file path and whether it's a save file.
    /// This is called from detours when we know a save is happening but haven't loaded the file yet.
    /// </summary>
    /// <param name="filePath">The file path being saved to.</param>
    /// <param name="isSaveFile">True if this is a .sav file being saved during gameplay.</param>
    internal void SetCurrentFileContext(string filePath, bool isSaveFile)
    {
        _currentFilePath = filePath;
        _currentFileIsSaveFile = isSaveFile;
        LogHelper.Debug($"File context set: Path=[{filePath}], IsSaveFile={isSaveFile}");
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnLoadSave.Observable.Subscribe(OnLoadSave);
        MapLoaderR3EventHooks.OnLoadMap.Observable.Subscribe(OnLoadMap);
        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    /// <summary>
    /// Event handler called when a map is unloaded.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs e)
    {
        LogHelper.Information($"Unloading archive");
        Instance.RemoveActiveArchive();
        Instance._currentFilePath = null;
        Instance._currentFileIsSaveFile = false;
    }

    /// <summary>
    /// Event handler called when a save game is loaded.
    /// NOTE: This is called twice, but this is fine.
    /// </summary>
    private static void OnLoadSave(LoadSaveGameEventArgs e)
    {
        //if (e.Phase == EventHookPhase.Post)
        //    return;

        LogHelper.Information($"Loading archive from save file");
        Instance._currentFileIsSaveFile = true;
        Instance.LoadMapArchive(e.FileName);

        LogHelper.Information($"Archive Contents: [\n{Instance._mapArchive?.PrintAllEntries()}]");
    }

    /// <summary>
    /// Event handler called when a map is loaded.
    /// NOTE: This is called twice, but this is fine.
    /// </summary>
    private static void OnLoadMap(MapLoadEventArgs e)
    {
        //if (e.Phase == EventHookPhase.Post)
        //    return;

        LogHelper.Information($"Loading archive from map file");
        Instance._currentFileIsSaveFile = false;
        Instance.LoadMapArchive(e.FileName);

        LogHelper.Information($"Archive Contents: [\n{Instance._mapArchive?.PrintAllEntries()}]");
    }

    //
    // Public Functions
    //

    /// <summary>
    /// Tries to read the contents of a text file from the loaded map archive.
    /// </summary>
    /// <param name="fileName">The name of the entry within the zip archive.</param>
    /// <param name="ignoreCase">Whether to perform a case-insensitive search for the entry.</param>
    /// <returns>The string contents of the file, or <c>null</c> if not found or if no archive is loaded.</returns>
    public string? TryReadTextFile(string fileName, bool ignoreCase = true)
    {
        return _mapArchive?.TryReadTextFile(fileName, ignoreCase);
    }


    /// <summary>
    /// Tries to read the contents of a binary file from the loaded map archive.
    /// </summary>
    /// <param name="fileName">The name of the entry within the zip archive.</param>
    /// <param name="ignoreCase">Whether to perform a case-insensitive search for the entry.</param>
    /// <returns>A byte array of the file's contents, or <c>null</c> if not found or if no archive is loaded.</returns>
    public byte[]? TryReadBinaryFile(string fileName, bool ignoreCase = true)
    {
        return _mapArchive?.TryReadBinaryFile(fileName, ignoreCase);
    }

    /// <summary>
    /// Tries to write or overwrite a text file in the loaded map archive.
    /// If no archive exists, one will be created automatically.
    /// </summary>
    /// <param name="fileName">The name of the entry within the zip archive.</param>
    /// <param name="contents">The string content to write to the file.</param>
    /// <param name="ignoreCase">Whether to perform a case-insensitive search for an existing entry.</param>
    /// <param name="overwrite">If true, an existing file with the same name will be overwritten.</param>
    /// <returns>True if the file was written successfully, false otherwise. Returns null if no archive is loaded.</returns>
    public bool? TryWriteTextFile(string fileName, string contents, bool ignoreCase = true, bool overwrite = true)
    {
        // Create archive on-demand if needed
        EnsureArchiveExists();

        return _mapArchive?.TryWriteTextFile(fileName, contents, ignoreCase, overwrite);
    }

    /// <summary>
    /// Tries to write or overwrite a binary file in the loaded map archive.
    /// If no archive exists, one will be created automatically.
    /// </summary>
    /// <param name="fileName">The name of the entry within the zip archive.</param>
    /// <param name="bytes">The byte content to write to the file.</param>
    /// <param name="ignoreCase">Whether to perform a case-insensitive search for an existing entry.</param>
    /// <param name="overwrite">If true, an existing file with the same name will be overwritten.</param>
    /// <returns>True if the file was written successfully, false otherwise. Returns null if no archive is loaded.</returns>
    public bool? TryWriteBinaryFile(string fileName, byte[] bytes, bool ignoreCase = true, bool overwrite = true)
    {
        LogHelper.Debug($"Write (binary) [{fileName}, {bytes.Length} bytes]");

        // Create archive on-demand if needed
        EnsureArchiveExists();

        if (_mapArchive == null)
        {
            LogHelper.Error($"Write (binary) impossible - archive could not be created");
            return false;
        }

        return _mapArchive?.TryWriteBinaryFile(fileName, bytes, ignoreCase, overwrite);
    }

    //
    // Internal Functions
    //

    /// <summary>
    /// Ensures that a map archive exists. If one doesn't exist, it creates an empty one.
    /// This is called automatically when mods try to write data.
    /// </summary>
    private void EnsureArchiveExists()
    {
        if (_mapArchive != null)
        {
            // Archive already exists
            return;
        }

        if (string.IsNullOrEmpty(_currentFilePath))
        {
            LogHelper.Warning($"Cannot create archive - no current file path");
            return;
        }

        LogHelper.Information($"Creating on-demand archive for [{_currentFilePath}]");

        try
        {
            // Create a new empty MapArchive - use CreateEmpty variant
            _mapArchive = MapArchive.CreateEmpty(_currentFilePath);
            LogHelper.Information($"On-demand archive created successfully");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to create on-demand archive for [{_currentFilePath}]");
        }
    }

    /// <summary>
    /// Serializes the current metadata from the <see cref="GameMetadataManagerAPI"/> and writes it to the in-memory archive.
    /// This is called just before the game is saved.
    /// </summary>
    internal void WriteMetadataToArchive()
    {
        LogHelper.Information($"Writing metadata to archive");
        byte[]? bytes = GameMetadataManagerAPI.Instance.Serialize();
        if (bytes == null)
        {
            LogHelper.Warning($"Nothing to write");
            return;
        }
        TryWriteBinaryFile(GameMetadataManagerAPI.MAP_ARCHIVE_IDENTIFIER_FILENAME, bytes);
        LogHelper.Information($"Metadata written");
    }

    /// <summary>
    /// Reads serialized metadata from the in-memory archive and loads it into the <see cref="GameMetadataManagerAPI"/>.
    /// This is called immediately after a map or savegame is loaded.
    /// </summary>
    internal void ReadMetadataFromArchive()
    {
        LogHelper.Information($"Reading metadata from archive");
        byte[]? bytes = TryReadBinaryFile(GameMetadataManagerAPI.MAP_ARCHIVE_IDENTIFIER_FILENAME);
        if (bytes == null)
        {
            LogHelper.Warning($"Nothing to read");
            return;
        }
        GameMetadataManagerAPI.Instance.LoadFromMessagePack(bytes);
        LogHelper.Information($"Metadata read");
    }

    /// <summary>
    /// Serializes the current timer data from the <see cref="TimerEngine"/> and writes it to the in-memory archive.
    /// This is called just before the game is saved.
    /// </summary>
    internal void WriteTimerDataToArchive()
    {
        LogHelper.Information($"Writing timerdata to archive");
        byte[]? bytes = GameTimeManagerAPI.Instance.GetTimerEngine().Serialize();
        if (bytes == null)
        {
            LogHelper.Warning($"Nothing to write");
            return;
        }
        TryWriteBinaryFile(TimerEngine.MAP_ARCHIVE_IDENTIFIER_FILENAME, bytes);
        LogHelper.Information($"Written timerdata");
    }

    /// <summary>
    /// Reads serialized timer data from the in-memory archive and loads it into the <see cref="TimerEngine"/>.
    /// This is called immediately after a map or savegame is loaded.
    /// </summary>
    internal void ReadTimerDataFromArchive()
    {
        LogHelper.Information($"Reading timerdata from archive");
        byte[]? bytes = TryReadBinaryFile(TimerEngine.MAP_ARCHIVE_IDENTIFIER_FILENAME);
        if (bytes == null)
        {
            LogHelper.Warning($"Nothing to read");
            return;
        }
        GameTimeManagerAPI.Instance.GetTimerEngine().LoadFromMessagePack(bytes);
        LogHelper.Information($"Timerdata read");
    }

    /// <summary>
    /// Loads a <see cref="MapArchive"/> from the specified file path.
    /// If an archive is found, it replaces the currently active one.
    /// If no archive exists, that's OK - one will be created on-demand when needed.
    /// </summary>
    /// <param name="filePath">The path to the .map or .sav file.</param>
    internal void LoadMapArchive(string filePath)
    {
        LogHelper.Information($"Loading archive: [{filePath}]");
        _currentFilePath = filePath;

        if (!MapArchive.TryLoad(filePath, out _mapArchive))
        {
            LogHelper.Information($"No existing archive found - will create on-demand if needed");

            // The archive will be created later if any mod tries to write data
        }
        else
        {
            // Make sure to sync with the metadata (if exists)
            ReadMetadataFromArchive();

            // make sure to read any timer data (if exists)
            ReadTimerDataFromArchive();
        }

        // Load all mod-specific save data (even if archive doesn't exist yet)
        LoadContext loadContext = new LoadContext(
            isSaveFile: _currentFileIsSaveFile,
            filePath: filePath
        );
        ModSaveDataAPI.Instance.ReadAllModDataFromArchive(loadContext);

        LogHelper.Information($"Archive loaded (or will be created on-demand)");
    }

    /// <summary>
    /// Prepares the current MapArchive and serializes the current in-memory map archive to a byte array.
    /// This is called just before writing the file to disk.
    /// If there's no archive and no data to save, returns null (no archive will be written).
    /// </summary>
    /// <param name="isMapEditorSave">True if this save is coming from the map editor.</param>
    /// <returns>A byte array representing the zip archive, or <c>null</c> if no archive is loaded.</returns>
    internal byte[]? PrepareMapArchiveAndGetBytes(bool isMapEditorSave = false)
    {
        LogHelper.Information($"Preparing map archive (isMapEditorSave={isMapEditorSave})");

        // First, let mods write their data - this might create the archive
        SaveContext saveContext = new SaveContext(
            isSaveFile: _currentFileIsSaveFile,
            isMapEditorSave: isMapEditorSave,
            filePath: _currentFilePath
        );
        ModSaveDataAPI.Instance.WriteAllModDataToArchive(saveContext);

        // Also write metadata and timer data
        WriteMetadataToArchive();
        WriteTimerDataToArchive();

        // Now check if we have an archive
        if (_mapArchive == null)
        {
            LogHelper.Information($"No archive exists and no data was written - returning empty archive");
            // Return an empty zip so the game file structure is consistent
            return CreateEmptyZip();
        }

        // Commit all pending changes to the archive stream.
        _mapArchive.CommitChanges();

        return _mapArchive.ArchiveStream?.ToArray();
    }

    /// <summary>
    /// Creates a byte array representing an empty, valid zip archive.
    /// </summary>
    /// <returns>A byte array for an empty zip file.</returns>
    internal static byte[] CreateEmptyZip()
    {
        using MemoryStream memoryStream = new MemoryStream();
        using ZipOutputStream zipOutput = new ZipOutputStream(memoryStream);
        zipOutput.IsStreamOwner = false;
        zipOutput.Finish();
        return memoryStream.ToArray();
    }

    //
    // Map Editor functions
    //

    /// <summary>
    /// Creates a new, empty <see cref="MapArchive"/> for the map currently being edited.
    /// </summary>
    /// <remarks>
    /// This is intended for use within the map editor to initialize an archive for a map that doesn't have one yet.
    /// </remarks>
    internal void CreateForActiveMapEditorMap()
    {
        LogHelper.Information($"Attempting to create archive for active map");
        string mapPath = Path.Combine(DirectoryHelpers.GameMapsDirectory, GamePlayerManagerAPI.Instance.GetCurrentMapName());
        if (!Path.HasExtension(mapPath))
            mapPath += ".map";

        if (!File.Exists(mapPath))
        {
            LogHelper.Error($"Failed to find map: [{mapPath}]");
            return;
        }
        _mapArchive = MapArchive.CreateEmpty(mapPath);
        _currentFilePath = mapPath;
        _currentFileIsSaveFile = false; // Map editor maps are not save files
    }

    /// <summary>
    /// Disposes the currently active map archive and sets it to null.
    /// </summary>
    internal void RemoveActiveArchive()
    {
        LogHelper.Information($"Attempting to remove active archive");
        _mapArchive?.Dispose();
        _mapArchive = null;
    }

}
