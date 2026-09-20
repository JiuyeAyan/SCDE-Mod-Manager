using MessagePack;
using SHCDESE.API.Components.Network;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Handles persistent storage for lobby mod settings, using encoded
/// files stored in a dedicated folder next to each plugin's assembly.
/// </summary>
/// <remarks>
/// <para>
/// Each mod gets its own <c>.mpsettings</c> file stored at:
/// <code>
/// BepInEx/plugins/YourMod/LobbyModSettings/ModName.bin
/// </code>
/// </para>
/// <para>
/// The file contains a encoded <c>Dictionary&lt;string, byte[]&gt;</c>
/// where each key is a property name and each value is the serialized
/// property value. This allows arbitrary types (primitives, arrays, nested objects)
/// to survive a round-trip without any type restriction.
/// </para>
/// <para>
/// Which properties are persisted is decided by <see cref="LobbyModSettingsRouting"/>: the two
/// sync attributes, plus <see cref="PersistLocalAttribute"/> for values that are stored but never
/// sent, minus anything marked <see cref="DoNotPersistAttribute"/>. Undecorated properties are
/// local UI state and are not written to disk.
/// </para>
/// </remarks>
public sealed class LobbyModSettingsStorage
{
    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    /// <summary>
    /// Subdirectory created next to each plugin assembly where settings files live.
    /// </summary>
    public const string STORAGE_FOLDER_NAME = "LobbyModSettings";

    /// <summary>File extension for settings files.</summary>
    public const string FILE_EXTENSION = ".msgpack";

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>Absolute path to the folder where this mod's settings file lives.</summary>
    private readonly string _storageDirectory;

    /// <summary>Absolute path to this mod's settings file.</summary>
    private readonly string _filePath;

    /// <summary>Display name of the mod, used only for logging.</summary>
    private readonly string _modName;

    /// <summary>
    /// In-memory cache of the last successfully loaded or saved payload.
    /// Avoids redundant disk reads when <see cref="Save"/> and <see cref="Load"/>
    /// are called in quick succession.
    /// </summary>
    private Dictionary<string, byte[]> _cache = new();

    // -------------------------------------------------------------------------
    // Construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a storage instance for a specific mod.
    /// </summary>
    /// <param name="pluginAssemblyLocation">
    /// The full path to the plugin assembly (typically <c>plugin.Info.Location</c>
    /// or <c>Assembly.GetExecutingAssembly().Location</c>). The storage folder is
    /// created as a sibling of this file.
    /// </param>
    /// <param name="modName">
    /// The mod's unique name, used as the settings filename. Must be consistent
    /// across versions: renaming it will abandon the previous settings file.
    /// </param>
    public LobbyModSettingsStorage(string pluginAssemblyLocation, string modName)
    {
        _modName = modName;

        string pluginDir = Path.GetDirectoryName(pluginAssemblyLocation) ?? throw new ArgumentException($"Cannot determine directory for [{pluginAssemblyLocation}]");

        _storageDirectory = Path.Combine(pluginDir, STORAGE_FOLDER_NAME);

        // Sanitise the mod name so it is safe as a file name
        string safeFileName = string.Concat(modName.Split(Path.GetInvalidFileNameChars()));
        _filePath = Path.Combine(_storageDirectory, safeFileName + FILE_EXTENSION);

        LogHelper.Debug($"LobbyModSettingsStorage [{_modName}]: storage path = [{_filePath}]");
    }

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Saves the persisted properties of <paramref name="viewModel"/> that the local player owns.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Each property value is serialized individually using MessagePack, then the
    /// resulting <c>Dictionary&lt;string, byte[]&gt;</c> is serialized as a whole.
    /// This means the file is always a complete snapshot — partial saves do not occur.
    /// </para>
    /// <para>
    /// Because the snapshot is complete, ownership matters. On a client the live ViewModel holds
    /// <see cref="SyncHostOnlyAttribute"/> values received from the host, so writing the ViewModel
    /// verbatim would overwrite this player's own settings with someone else's — even though the
    /// incoming update itself was never persisted. Host-only values are therefore skipped on a
    /// client, and the previously persisted value is carried forward so the player's own choice
    /// survives the session unchanged.
    /// </para>
    /// </remarks>
    /// <param name="viewModel">The ViewModel whose persisted properties should be written.</param>
    public void Save(object viewModel)
    {
        PropertyInfo[] props = viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        Dictionary<string, byte[]> payload = [];
        bool ownsHostOnlyValues = GameNetworkAPI.IsLocalHost();

        foreach (PropertyInfo prop in props)
        {
            if (!prop.CanRead || !prop.IsPersisted())
                continue;

            if (prop.IsHostOnly() && !ownsHostOnlyValues)
            {
                // The current value belongs to the host, not to this player. Keep whatever was
                // last persisted locally rather than either overwriting it or dropping it.
                if (_cache.TryGetValue(prop.Name, out byte[]? persisted))
                {
                    payload[prop.Name] = persisted;
                    LogHelper.Debug($"[{_modName}]: Preserving stored [{prop.Name}], host-only value is not owned locally");
                }
                else
                {
                    LogHelper.Debug($"[{_modName}]: Skipping [{prop.Name}], host-only value is not owned locally");
                }

                continue;
            }

            try
            {
                object? val = prop.GetValue(viewModel);
                if (val == null)
                {
                    LogHelper.Debug($"[{_modName}]: Skipping null property [{prop.Name}]");
                    continue;
                }

                // Serialize the value with its concrete type so arrays, nested objects,
                // and any MessagePack-compatible type round-trip correctly.
                byte[] bytes = MessagePackSerializer.Serialize(prop.PropertyType, val);
                payload[prop.Name] = bytes;

                LogHelper.Debug($"[{_modName}]: Serialized [{prop.Name}] ({bytes.Length}B)");
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"[{_modName}]: Failed to serialize [{prop.Name}], skipping");
            }
        }

        if (payload.Count == 0)
        {
            LogHelper.Debug($"[{_modName}]: Nothing to save, skipping disk write");
            return;
        }

        WriteToDisk(payload);
        _cache = payload;
    }

    /// <summary>
    /// Loads persisted settings from disk and applies them to <paramref name="viewModel"/>.
    /// <para>
    /// A stored <see cref="SyncHostOnlyAttribute"/> value is this player's own configuration, kept
    /// for when they host. Joining a lobby overwrites it in the ViewModel with the host's value,
    /// which <see cref="Save"/> then declines to write back.
    /// </para>
    /// </summary>
    /// <remarks>
    /// Properties not present in the settings file (new properties added in a mod update)
    /// are left at their ViewModel-declared defaults. Deserialization failures for
    /// individual properties are logged and skipped so a single corrupt entry cannot
    /// prevent the rest from loading.
    /// </remarks>
    /// <param name="viewModel">The ViewModel whose persisted properties should be restored.</param>
    public void Load(object viewModel)
    {
        Dictionary<string, byte[]>? payload = ReadFromDisk();
        if (payload == null)
        {
            LogHelper.Debug($"[{_modName}]: No settings file found, keeping ViewModel defaults");
            return;
        }

        _cache = payload;

        PropertyInfo[] props = viewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo prop in props)
        {
            if (!prop.CanWrite || !prop.IsPersisted())
                continue;

            if (!payload.TryGetValue(prop.Name, out byte[]? bytes))
            {
                LogHelper.Debug($"[{_modName}]: No persisted value for [{prop.Name}], keeping default");
                continue;
            }

            try
            {
                object? restored = MessagePackSerializer.Deserialize(prop.PropertyType, bytes);
                if (restored == null)
                {
                    LogHelper.Warning($"[{_modName}]: Deserialized null for [{prop.Name}], keeping default");
                    continue;
                }

                prop.SetValue(viewModel, restored);
                LogHelper.Debug($"[{_modName}]: Restored [{prop.Name}]");
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"[{_modName}]: Failed to deserialize [{prop.Name}], keeping default");
            }
        }

        LogHelper.Information($"[{_modName}]: Settings restored from [{_filePath}]");
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <paramref name="payload"/> and writes it to <see cref="_filePath"/>.
    /// Creates the storage directory if it does not yet exist.
    /// </summary>
    private void WriteToDisk(Dictionary<string, byte[]> payload)
    {
        try
        {
            if (!Directory.Exists(_storageDirectory))
            {
                Directory.CreateDirectory(_storageDirectory);
                LogHelper.Information($"[{_modName}]: Created storage directory [{_storageDirectory}]");
            }

            byte[] fileBytes = MessagePackSerializer.Serialize(payload);
            File.WriteAllBytes(_filePath, fileBytes);

            LogHelper.Debug($"[{_modName}]: Wrote {fileBytes.Length}B to [{_filePath}]");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"[{_modName}]: Failed to write settings to [{_filePath}]");
        }
    }

    /// <summary>
    /// Reads and deserializes the settings file from disk.
    /// </summary>
    /// <returns>
    /// The deserialized payload, or <c>null</c> if the file does not exist or cannot be read.
    /// </returns>
    private Dictionary<string, byte[]>? ReadFromDisk()
    {
        if (!File.Exists(_filePath))
            return null;

        try
        {
            byte[] fileBytes = File.ReadAllBytes(_filePath);
            Dictionary<string, byte[]>? payload =
                MessagePackSerializer.Deserialize<Dictionary<string, byte[]>>(fileBytes);

            LogHelper.Debug($"[{_modName}]: Read {fileBytes.Length}B from [{_filePath}]");
            return payload;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"[{_modName}]: Failed to read or deserialize [{_filePath}]: file may be corrupt");
            return null;
        }
    }
}