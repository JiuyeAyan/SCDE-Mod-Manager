using MessagePack;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides a persistent, key-value storage system for custom data that is automatically saved and loaded with the game.
/// This manager is intended for tracking custom entity stats (like kill counts) or global quest states.
/// </summary>
/// <example> 
/// This Lua script uses the metadata system to track how many kills a unit has.
/// <code>
/// -- Get the event hook
/// local onDeathHook = Hooks:GetHook("OnUnitKilledByMelee")
/// 
/// -- Subscribe a function to the event
/// onDeathHook:Subscribe(function(eventArgs)
/// 	local attackerId = eventArgs.AttackingUnitId
/// 	local damagedId = eventArgs.DamagedUnitId
/// 	local attackerGlobalId = Unit_GetGlobalId(attackerId)
/// 	
///     -- Get the current kill count, defaulting to 0 if it doesn't exist
/// 	local killCount = tonumber(Metadata_GetInt(attackerGlobalId)) or 0
/// 	local newKillCount = killCount + 1
/// 	
/// 	print("Unit id="..attackerId.."/gid="..attackerGlobalId.." has killed "..damagedId..", it now has "..newKillCount.." kills!")
/// 	
///     -- Save the new kill count back to the metadata system
/// 	Metadata_SetInt(attackerGlobalId, tostring(newKillCount))
/// 	
///     -- NOTE: Keep in mind that you need to handle cleanup yourself. Metadata persists after unit deletion and or death.
/// end)
/// </code>
/// </example>
[LuaApiNamespace("Metadata")]
public sealed class GameMetadataManagerAPI
{
    private static readonly Lazy<GameMetadataManagerAPI> _lazy = new(() => new GameMetadataManagerAPI());
    public static GameMetadataManagerAPI Instance => _lazy.Value;

    /// <summary>
    /// The entity meta data system is responsible for storing per-entity specific metadata.
    /// Useful for keeping track of custom statistics, for example: experience, total kills, etc.
    /// The key used is the r_GlobalId entry preferrably, but can be anything.
    /// </summary>
    [MessagePackObject(true)]
    public class EntityMetadataHolder
    {
        public Dictionary<int, string> IntEntityMetadata { get; set; }
        public Dictionary<string, string> StringEntityMetadata { get; set; }
    }
    private EntityMetadataHolder _metadataHolder;


    internal const string MAP_ARCHIVE_IDENTIFIER_FILENAME = "_SE_Metadata.msgpack";

    private int _initialized = 0;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameUnitManagerAPI"/> class.
    /// </summary>
    private GameMetadataManagerAPI()
    {
        _metadataHolder = new EntityMetadataHolder() { 
            IntEntityMetadata = [], 
            StringEntityMetadata = [] 
        };
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    internal void Unload()
    {
        _metadataHolder?.IntEntityMetadata.Clear();
        _metadataHolder?.StringEntityMetadata.Clear();
    }

    /// <summary>
    /// Event handler called when a map is unloaded.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs args)
    {
        LogHelper.Information($"Unloading metadata");
        Instance.Clear();
    }

    /// <summary>
    /// Checks if a metadata entry exists for the given global ID.
    /// </summary>
    [LuaApiExport("HasInt")]
    public bool HasMetadataInt(int globalId)
    {
        return _metadataHolder.IntEntityMetadata.ContainsKey(globalId);
    }


    /// <summary>
    /// Gets a metadata value for a given integer key.
    /// </summary>
    /// <param name="globalId">The integer key, usually a unit's `r_GlobalId`.</param>
    /// <returns>The stored string value, or an empty string if the key is not found.</returns>
    [LuaApiExport("GetInt")]
    public string GetMetadataInt(int globalId)
    {
        return _metadataHolder.IntEntityMetadata.TryGetValue(globalId, out string? value) ? value : string.Empty;
    }

    /// <summary>
    /// Sets a metadata value for a given integer key (typically a unit's r_GlobalId).
    /// </summary>
    /// <param name="globalId">The integer key, usually a unit's `r_GlobalId`.</param>
    /// <param name="metadata">The string value to store.</param>
    [LuaApiExport("SetInt")]
    public void SetMetadataInt(int globalId, string metadata)
    {
        if (metadata == null) 
            metadata = string.Empty;

        _metadataHolder.IntEntityMetadata[globalId] = metadata;
    }

    /// <summary>
    /// Removes a metadata entry for a given integer key. 
    /// Useful for cleaning up data when a unit permanently dies or is deleted.
    /// </summary>
    /// <param name="globalId">The integer key, usually a unit's `r_GlobalId`.</param>
    [LuaApiExport("RemoveInt")]
    public bool RemoveMetadataInt(int globalId)
    {
        return _metadataHolder.IntEntityMetadata.Remove(globalId);
    }

    /// <summary>
    /// Checks if a metadata entry exists for the given string key.
    /// </summary>
    /// <param name="key">The unique string identifier for the data.</param>
    [LuaApiExport("HasString")]
    public bool HasMetadataString(string key)
    {
        if (string.IsNullOrEmpty(key)) 
            return false;
        return _metadataHolder.StringEntityMetadata.ContainsKey(key);
    }

    /// <summary>
    /// Gets a metadata value for a given string key.
    /// </summary>
    /// <param name="key">The unique string identifier for the data.</param>
    /// <returns>The stored string value, or an empty string if the key is not found.</returns>
    [LuaApiExport("SetString")]
    public string GetMetadataString(string key)
    {
        if (string.IsNullOrEmpty(key)) 
            return string.Empty;
        return _metadataHolder.StringEntityMetadata.TryGetValue(key, out string? value) ? value : string.Empty;
    }

    /// <summary>
    /// Sets a metadata value for a given string key.
    /// If an entry for the key already exists, it is overwritten.
    /// </summary>
    /// <param name="key">The unique string identifier for the data.</param>
    /// <param name="metadata">The string value to store.</param>
    /// <returns>The previous value associated with the key, or an empty string if the key was not found.</returns>
    [LuaApiExport("SetString")]
    public void SetMetadataString(string key, string metadata)
    {
        if (string.IsNullOrEmpty(key)) 
            return;
        metadata ??= string.Empty;

        _metadataHolder.StringEntityMetadata[key] = metadata;
    }

    /// <summary>
    /// Removes a metadata entry for a given string key.
    /// </summary>
    /// <param name="key">The unique string identifier for the data.</param>
    [LuaApiExport("RemoveString")]
    public bool RemoveMetadataString(string key)
    {
        if (string.IsNullOrEmpty(key)) 
            return false;
        return _metadataHolder.StringEntityMetadata.Remove(key);
    }


    /// <summary>
    /// Clears all stored metadata. This is automatically called when a map is unloaded.
    /// </summary>
    public void Clear()
    {
        _metadataHolder.IntEntityMetadata?.Clear();
        _metadataHolder.StringEntityMetadata?.Clear();
    }

    /// <summary>
    /// Serializes the current metadata state into a MessagePack byte array.
    /// </summary>
    /// <returns>A byte array representing the metadata, or <c>null</c> if there is no data to save.</returns>
    public byte[]? Serialize()
    {
        LogHelper.Information($"Serializing");
        if (_metadataHolder?.IntEntityMetadata == null || ((_metadataHolder?.IntEntityMetadata.Count == 0) && _metadataHolder?.StringEntityMetadata.Count == 0))
        {
            LogHelper.Warning($"EntityMetadataHolder or its primary property is/are not initialized or is empty!");
            return null;
        }

        byte[] bytes = MessagePackSerializer.Serialize(_metadataHolder);
        LogHelper.Information($"Serialized [bytes={bytes.Length}]");

        return bytes;
    }

    /// <summary>
    /// Deserializes a MessagePack byte array and loads it as the current metadata state.
    /// </summary>
    /// <param name="bytes">The byte array to deserialize.</param>
    public void LoadFromMessagePack(byte[] bytes)
    {
        if (bytes == null)
        {
            LogHelper.Error($"Input is null!");
            return;
        }

        try
        {
            EntityMetadataHolder? loadedHolder = MessagePackSerializer.Deserialize<EntityMetadataHolder>(bytes);
            if (loadedHolder != null)
            {
                _metadataHolder = loadedHolder;

                // Ensure dictionaries are initialized if the deserialized object had nulls
                if (_metadataHolder.IntEntityMetadata == null) 
                    _metadataHolder.IntEntityMetadata = new();

                if (_metadataHolder.StringEntityMetadata == null) 
                    _metadataHolder.StringEntityMetadata = new();

                LogHelper.Information($"Metadata Loaded: {_metadataHolder.IntEntityMetadata.Count} Int entries, {_metadataHolder.StringEntityMetadata.Count} String entries.");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to deserialize metadata from MessagePack. Data may be corrupt.");
            _metadataHolder = new EntityMetadataHolder();
        }
    }
}