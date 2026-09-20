using R3;
using SHCDESE.API.Components.SaveData;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides an extensible API for mods to save and load custom data within the Map Archive system.
/// Mods can register handlers to persist data across save/load cycles with control over when data is written.
/// Archives are created automatically on-demand when mods need to save data.
/// </summary>
/// <remarks>
/// This system distinguishes between:
/// - Save files (.sav) - game saves during gameplay
/// - Map files (.map) - maps being edited or saved in the map editor
/// 
/// Mods can choose to save data:
/// - Only for save files (gameplay saves)
/// - For all saves including map editor saves
/// - With custom logic based on the save context
/// </remarks>
/// <example>
/// Registering a mod save data handler:
/// <code>
/// // Define your data structure
/// [MessagePackObject(true)]
/// public class MyModData
/// {
///     public int PlayerScore { get; set; }
///     public Dictionary&lt;string, string&gt; CustomFlags { get; set; }
/// }
/// 
/// // Register the handler
/// ModSaveDataAPI.Instance.RegisterModDataHandler(
///     modIdentifier: "MyMod",
///     saveCallback: (context) => {
///         // Only save for actual save files, not map editor
///         if (!context.IsSaveFile) 
///             return null;
///             
///         var data = new MyModData {
///             PlayerScore = GetCurrentScore(),
///             CustomFlags = GetFlags()
///         };
///         return MessagePackSerializer.Serialize(data);
///     },
///     loadCallback: (bytes, context) => {
///         var data = MessagePackSerializer.Deserialize&lt;MyModData&gt;(bytes);
///         ApplyLoadedData(data);
///     }
/// );
/// </code>
/// </example>
public sealed class ModSaveDataAPI
{
    private static readonly Lazy<ModSaveDataAPI> _lazy = new(() => new ModSaveDataAPI());
    public static ModSaveDataAPI Instance => _lazy.Value;

    /// <summary>
    /// Prefix used for all mod data files in the map archive to avoid conflicts.
    /// </summary>
    private const string MOD_DATA_PREFIX = "_SE_ModData_";

    /// <summary>
    /// Registered mod data handlers, keyed by unique mod identifier.
    /// </summary>
    private readonly Dictionary<string, ModDataHandler> _handlers = new();

    private int _initialized = 0;

    private ModSaveDataAPI()
    {
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        // Subscribe to map unload to clear any in-memory state
        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    private static void OnUnloadMap(MapUnloadEventArgs args)
    {
        LogHelper.Information($"Map unloaded, clearing handler state");

        // Notify all handlers that the map has unloaded
        foreach (ModDataHandler handler in Instance._handlers.Values)
        {
            handler.OnUnloadCallback?.Invoke();
        }
    }

    /// <summary>
    /// Registers a mod's save/load data handler.
    /// </summary>
    /// <param name="modIdentifier">Unique identifier for the mod (e.g., "MyMod" or "com.author.modname"). Must be file-system safe.</param>
    /// <param name="saveCallback">Function called when data should be saved. Returns byte array or null if nothing to save.</param>
    /// <param name="loadCallback">Function called when data is loaded. Receives the saved bytes and context.</param>
    /// <param name="onUnloadCallback">Optional callback when map is unloaded, for cleanup.</param>
    /// <returns>True if registration succeeded, false if the identifier is already registered.</returns>
    public bool RegisterModDataHandler(
        string modIdentifier,
        Func<SaveContext, byte[]?> saveCallback,
        Action<byte[], LoadContext> loadCallback,
        Action? onUnloadCallback = null)
    {
        if (string.IsNullOrWhiteSpace(modIdentifier))
        {
            LogHelper.Error($"Mod identifier cannot be null or whitespace");
            return false;
        }

        // Validate identifier is file-system safe
        if (modIdentifier.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
        {
            LogHelper.Error($"Mod identifier [{modIdentifier}] contains invalid characters");
            return false;
        }

        if (_handlers.ContainsKey(modIdentifier))
        {
            LogHelper.Warning($"Handler for [{modIdentifier}] is already registered");
            return false;
        }

        ModDataHandler handler = new ModDataHandler
        {
            ModIdentifier = modIdentifier,
            SaveCallback = saveCallback,
            LoadCallback = loadCallback,
            OnUnloadCallback = onUnloadCallback
        };

        _handlers[modIdentifier] = handler;
        LogHelper.Information($"Registered handler for [{modIdentifier}]");
        return true;
    }

    /// <summary>
    /// Unregisters a mod's data handler.
    /// </summary>
    /// <param name="modIdentifier">The unique identifier used during registration.</param>
    /// <returns>True if the handler was found and removed.</returns>
    public bool UnregisterModDataHandler(string modIdentifier)
    {
        if (_handlers.Remove(modIdentifier))
        {
            LogHelper.Information($"Unregistered handler for [{modIdentifier}]");
            return true;
        }
        return false;
    }

    /// <summary>
    /// Called internally when a save file or map is being saved.
    /// Invokes all registered mod handlers to serialize their data into the archive.
    /// If any mod has data to save, the archive will be created automatically.
    /// </summary>
    /// <param name="context">The save context containing information about what's being saved.</param>
    internal void WriteAllModDataToArchive(SaveContext context)
    {
        LogHelper.Information($"Writing mod data to archive (IsSaveFile={context.IsSaveFile}, IsMapEditor={context.IsMapEditorSave})");

        int savedCount = 0;
        int attemptedCount = 0;

        foreach (KeyValuePair<string, ModDataHandler> kvp in _handlers)
        {
            string modId = kvp.Key;
            ModDataHandler handler = kvp.Value;

            try
            {
                byte[]? data = handler.SaveCallback(context);
                if (data == null || data.Length == 0)
                {
                    LogHelper.Debug($"Mod [{modId}] returned no data to save");
                    continue;
                }

                attemptedCount++;
                string fileName = GetModDataFileName(modId);

                // This will create the archive on-demand if needed
                bool? result = GameMapArchiveManagerAPI.Instance.TryWriteBinaryFile(fileName, data, ignoreCase: true, overwrite: true);

                if (result == true)
                {
                    savedCount++;
                    LogHelper.Information($"Saved {data.Length} bytes for mod [{modId}]");
                }
                else
                {
                    LogHelper.Warning($"Failed to write data for mod [{modId}]");
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error saving data for mod [{modId}]");
            }
        }

        if (attemptedCount > 0)
        {
            LogHelper.Information($"Saved data for {savedCount}/{attemptedCount} mods (archive created/updated)");
        }
        else
        {
            LogHelper.Debug($"No mods requested to save data");
        }
    }

    /// <summary>
    /// Called internally when a save file or map is being loaded.
    /// Invokes all registered mod handlers to deserialize their data from the archive.
    /// Gracefully handles missing archives (when loading maps without embedded data).
    /// </summary>
    /// <param name="context">The load context containing information about what's being loaded.</param>
    internal void ReadAllModDataFromArchive(LoadContext context)
    {
        LogHelper.Information($"Reading mod data from archive (IsSaveFile={context.IsSaveFile})");

        // Check if there's even an archive to read from
        if (GameMapArchiveManagerAPI.Instance.GetMapArchive() == null)
        {
            LogHelper.Information($"No archive exists - skipping mod data load (this is normal for new maps)");
            return;
        }

        int loadedCount = 0;
        foreach (KeyValuePair<string, ModDataHandler> kvp in _handlers)
        {
            string modId = kvp.Key;
            ModDataHandler handler = kvp.Value;

            try
            {
                string fileName = GetModDataFileName(modId);
                byte[]? data = GameMapArchiveManagerAPI.Instance.TryReadBinaryFile(fileName, ignoreCase: true);

                if (data == null || data.Length == 0)
                {
                    LogHelper.Debug($"No data found for mod [{modId}] (this is normal for new saves)");
                    continue;
                }

                handler.LoadCallback(data, context);
                loadedCount++;
                LogHelper.Information($"Loaded {data.Length} bytes for mod [{modId}]");
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error loading data for mod [{modId}]");
            }
        }

        LogHelper.Information($"Loaded data for {loadedCount}/{_handlers.Count} mods");
    }

    /// <summary>
    /// Gets the standardized file name for a mod's data in the archive.
    /// </summary>
    private string GetModDataFileName(string modIdentifier)
    {
        return $"{MOD_DATA_PREFIX}{modIdentifier}.msgpack";
    }

    /// <summary>
    /// Represents a registered mod data handler.
    /// </summary>
    private class ModDataHandler
    {
        public required string ModIdentifier { get; init; }
        public required Func<SaveContext, byte[]?> SaveCallback { get; init; }
        public required Action<byte[], LoadContext> LoadCallback { get; init; }
        public Action? OnUnloadCallback { get; init; }
    }
}