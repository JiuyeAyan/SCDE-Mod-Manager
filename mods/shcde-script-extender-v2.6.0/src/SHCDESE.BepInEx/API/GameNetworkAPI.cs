using BepInEx;
using BepInEx.Bootstrap;
using CrusaderDE;
using MessagePack;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Network;
using SHCDESE.GameGlobals;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using Steamworks;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO.Hashing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides the central API for all networking operations within SHCDE:SE.
/// <para>
/// Responsibilities include:
/// <list type="bullet">
///   <item>Custom packet type registration and dispatch via R3 observable streams.</item>
///   <item>Lobby-phase and in-game packet sending (channel 2, reliable).</item>
///   <item>Chat message interception and command parsing.</item>
///   <item>Player identity helpers (host check, local player ID).</item>
/// </list>
/// </para>
/// <para>
/// <b>Thread safety:</b> All public methods must be called from the Unity main thread.
/// The packet ID registry is the only section protected by a lock, as it may be
/// touched during initialization from a background thread.
/// </para>
/// </summary>
public unsafe sealed class GameNetworkAPI
{
#pragma warning disable 0618

    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    private static readonly Lazy<GameNetworkAPI> _lazy = new(() => new GameNetworkAPI());
    public static GameNetworkAPI Instance => _lazy.Value;

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    internal const char COMMAND_PREFIX = '/';

    /// <summary>
    /// Steam lobby metadata key used to mark lobbies that are running under SHCDE:SE.
    /// Clients check for this token before joining, ensuring vanilla players are filtered out.
    /// </summary>
    internal const string LOBBY_IDENTIFIER_TOKEN = "_SE_";

    /// <summary>
    /// Steam lobby metadata key that stores a hash of the host's active mod set
    /// (BepInEx plugins + asset mods). Retained as a conservative compatibility
    /// fallback for older SE lobbies that do not advertise a detailed mod list.
    /// </summary>
    internal const string LOBBY_MOD_HASH_TOKEN = "_SE_MODHASH_";

    /// <summary>
    /// Steam lobby metadata key containing a compact JSON list of the host's active mods.
    /// Unlike the hash, this data is also used for lobby tooltips and client-side-only
    /// compatibility decisions.
    /// </summary>
    internal const string LOBBY_MOD_LIST_TOKEN = "_SE_MODS_";

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>Maps packet IDs to their registered R3 handler instances.</summary>
    private readonly Dictionary<Int16, IPacketR3Handler> _packetHandlers = new();

    /// <summary>Ensures <see cref="InitializeSubscribers"/> runs exactly once.</summary>
    private int _initialized = 0;

    /// <summary>Maps .NET types to their dynamically assigned network packet IDs.</summary>
    private readonly Dictionary<Type, short> _typeToIdRegistry = new();

    /// <summary>
    /// Next available dynamic packet ID. Starts well above the built-in game range
    /// (<see cref="CustomNetworkPacketType.CustomPacketStart"/> + 100) to leave
    /// headroom for any future built-in additions.
    /// </summary>
    private short _nextAvailablePacketId = (short)(CustomNetworkPacketType.CustomPacketStart + 100);

    private readonly object _registryLock = new object();

    private IntPtr _choreManager;
    internal Int32* IncomingRecord;
    internal Int32* IncomingPayload;
    internal Int32* CurrentPayloadSize;
    internal Int32* CurrentIncomingRecordSize;
    internal ChorePhase* ChorePhase;
    internal static byte[]? ChorePendingSendPayload;

    /// <summary>
    /// Safe cap for the whole [packetId][body] blob. 
    /// </summary>
    internal const int MAX_CHORE_PAYLOAD_BYTES = 1200;

    internal const int CHORE_RECORD_NATIVE_SIZE = 8;


    // -------------------------------------------------------------------------
    // Initialization / teardown
    // -------------------------------------------------------------------------

    private GameNetworkAPI()
    {
        _choreManager = (IntPtr)GameGlobalsManager.Instance.ChoreManagerVA;
        IncomingRecord = (Int32*)(_choreManager + 0x0EF4);
        IncomingPayload = (Int32*)(IncomingRecord + 8);
        CurrentPayloadSize = (Int32*)(_choreManager + 0x84CD4);
        CurrentIncomingRecordSize = (Int32*)(_choreManager + 0x84CC0);
        ChorePhase = (ChorePhase*)(GameGlobalsManager.Instance.ChoreSendPhaseVA);
    }

    /// <summary>
    /// Called once after <c>CrusaderDE.dll</c> has loaded. Sets up all network-related
    /// R3 event subscribers, including the mod-settings sync packet handler.
    /// </summary>
    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        DeterministicRandom.InitializeSubscribers();
        GameXAMLManagerAPI.Instance.SetupNetworking();
    }

    /// <summary>
    /// Clears all registered packet handlers and resets the dynamic ID counter.
    /// </summary>
    internal void Unload()
    {
        _packetHandlers.Clear();
        _typeToIdRegistry.Clear();
        _nextAvailablePacketId = (short)(CustomNetworkPacketType.CustomPacketStart + 100);
    }

    // -------------------------------------------------------------------------
    // Mod hash
    // -------------------------------------------------------------------------

    /// <summary>
    /// Computes a deterministic hash of all active BepInEx plugins and registered asset mods. The result is a short hex string suitable for lobby metadata.
    /// </summary>
    /// <remarks>
    /// The hash is built from sorted GUID+Version pairs so that load order does not affect the result. 
    /// Both BepInEx plugins (from <see cref="Chainloader.PluginInfos"/>) and asset mods (from <see cref="GameAssetModManager"/>) are included.
    /// </remarks>
    internal static string ComputeActiveModHash()
    {
        List<string> entries = [];

        foreach (KeyValuePair<string, PluginInfo> kvp in Chainloader.PluginInfos)
        {
            BepInPlugin meta = kvp.Value.Metadata;
            entries.Add($"{meta.GUID}@{meta.Version}");
        }

        foreach (KeyValuePair<ModInfo, string> kvp in GameAssetModManager.Instance.GetRegisteredAssetDirectories())
        {
            entries.Add($"asset:{kvp.Key.GUID}@{kvp.Key.Version}");
        }

        entries.Sort(StringComparer.Ordinal);

        string combined = string.Join("|", entries);

        ulong hash64 = XxHash64.HashToUInt64(Encoding.UTF8.GetBytes(combined));

        string hash = hash64.ToString("X16");
        LogHelper.Debug($"Computed mod hash: {hash} from {entries.Count} entries");
        return hash;
    }

    /// <summary>
    /// Serializes the active mod set for storage in Steam lobby metadata.
    /// Asset-mod metadata takes precedence over the matching BepInEx plugin entry because it also supplies network mode and workshop information.
    /// </summary>
    internal static string SerializeActiveModMetadata()
    {
        try
        {
            return JsonSerializer.Serialize(BuildActiveModMetadata());
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to serialize active mods for lobby metadata.");
            return string.Empty;
        }
    }

    /// <summary>
    /// Returns a readable active-mod list for a lobby-row tooltip.
    /// </summary>
    internal static string GetLobbyModsToolTip(CSteamID lobbyId)
    {
        if (!TryReadLobbyModMetadata(lobbyId, out List<LobbyModMetadataEntry> mods))
        {
            return "Active mods:\n- Details unavailable (older SE lobby)";
        }

        if (mods.Count == 0)
        {
            return "Active mods:\n- None";
        }

        StringBuilder result = new StringBuilder("Active mods:");
        foreach (LobbyModMetadataEntry mod in mods)
        {
            result.Append("\n- ");
            result.Append(mod.Name);
            result.Append(" v");
            result.Append(mod.Version);
            if (mod.Clientside)
            {
                result.Append(" (client-side)");
            }
        }

        return result.ToString();
    }

    /// <summary>
    /// Checks a joined lobby against the local mod list. 
    /// Differences are allowed only when every mod involved in the difference is declared client-side-only.
    /// </summary>
    internal static bool CanJoinLobby(CSteamID lobbyId)
    {
        if (TryReadLobbyModMetadata(lobbyId, out List<LobbyModMetadataEntry> remoteMods))
        {
            List<LobbyModMetadataEntry> localMods = BuildActiveModMetadata();
            if (AreLobbyModsCompatible(localMods, remoteMods, out string mismatch))
            {
                return true;
            }

            return RejectLobby(lobbyId, mismatch);
        }

        // Previously, older SE lobbies only advertised the aggregate hash.
        // We retain that conservative behavior when detailed metadata is missing or malformed.
        string remoteHash = SteamMatchmaking.GetLobbyData(lobbyId, LOBBY_MOD_HASH_TOKEN);
        if (string.IsNullOrEmpty(remoteHash) || string.Equals(remoteHash, ComputeActiveModHash(), StringComparison.Ordinal))
        {
            return true;
        }

        return RejectLobby(lobbyId, "legacy mod hash differs");
    }

    private static List<LobbyModMetadataEntry> BuildActiveModMetadata()
    {
        Dictionary<string, LobbyModMetadataEntry> byGuid = new Dictionary<string, LobbyModMetadataEntry>(StringComparer.OrdinalIgnoreCase);

        foreach (KeyValuePair<ModInfo, string> kvp in GameAssetModManager.Instance.GetRegisteredAssetDirectories())
        {
            ModInfo info = kvp.Key;
            string guid = CleanLobbyMetadataText(info.GUID, 200);
            if (string.IsNullOrWhiteSpace(guid))
            {
                continue;
            }

            byGuid[guid] = new LobbyModMetadataEntry
            {
                Guid = guid,
                Name = CleanLobbyMetadataText(info.Name, 160, guid),
                Version = CleanLobbyMetadataText(info.Version, 80, "0.0.0"),
                WorkshopUrl = NormalizeWorkshopUrl(info.WorkshopUrl),
                Clientside = info.NetworkMode == ModNetworkMode.Clientside,
            };
        }

        foreach (KeyValuePair<string, PluginInfo> kvp in Chainloader.PluginInfos)
        {
            BepInPlugin meta = kvp.Value.Metadata;
            string guid = CleanLobbyMetadataText(meta.GUID, 200);
            if (string.IsNullOrWhiteSpace(guid) || byGuid.ContainsKey(guid))
            {
                continue;
            }

            // A bare BepInEx plugin has no ModInfo.NetworkMode declaration.
            // Treat it as gameplay-affecting since we have no idea what this is.
            byGuid[guid] = new LobbyModMetadataEntry
            {
                Guid = guid,
                Name = CleanLobbyMetadataText(meta.Name, 160, guid),
                Version = CleanLobbyMetadataText(meta.Version?.ToString(), 80, "0.0.0"),
                WorkshopUrl = string.Empty,
                Clientside = false,
            };
        }

        List<LobbyModMetadataEntry> result = new List<LobbyModMetadataEntry>(byGuid.Values);
        result.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Guid, right.Guid));
        return result;
    }

    private static bool TryReadLobbyModMetadata(CSteamID lobbyId, out List<LobbyModMetadataEntry> mods)
    {
        mods = new List<LobbyModMetadataEntry>();
        string json = SteamMatchmaking.GetLobbyData(lobbyId, LOBBY_MOD_LIST_TOKEN);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        try
        {
            List<LobbyModMetadataEntry>? parsed = JsonSerializer.Deserialize<List<LobbyModMetadataEntry>>(json);
            if (parsed == null)
            {
                return false;
            }

            HashSet<string> seenGuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (LobbyModMetadataEntry mod in parsed)
            {
                if (mod == null)
                {
                    return false;
                }

                mod.Guid = CleanLobbyMetadataText(mod.Guid, 200);
                if (string.IsNullOrWhiteSpace(mod.Guid) || !seenGuids.Add(mod.Guid))
                {
                    return false;
                }

                mod.Name = CleanLobbyMetadataText(mod.Name, 160, mod.Guid);
                mod.Version = CleanLobbyMetadataText(mod.Version, 80, "0.0.0");
                mod.WorkshopUrl = NormalizeWorkshopUrl(mod.WorkshopUrl);
            }

            parsed.Sort((left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.Guid, right.Guid));
            mods = parsed;
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Warning($"Could not read mod metadata for lobby {lobbyId}: {ex.Message}");
            return false;
        }
    }

    private static bool AreLobbyModsCompatible(List<LobbyModMetadataEntry> localMods, List<LobbyModMetadataEntry> remoteMods, out string mismatch)
    {
        Dictionary<string, LobbyModMetadataEntry> remoteByGuid = new Dictionary<string, LobbyModMetadataEntry>(StringComparer.OrdinalIgnoreCase);
        foreach (LobbyModMetadataEntry remote in remoteMods)
        {
            remoteByGuid[remote.Guid] = remote;
        }

        HashSet<string> matched = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (LobbyModMetadataEntry local in localMods)
        {
            if (!remoteByGuid.TryGetValue(local.Guid, out LobbyModMetadataEntry? remote))
            {
                if (!local.Clientside)
                {
                    mismatch = $"local gameplay mod [{local.Name}] is absent on host";
                    return false;
                }

                continue;
            }

            matched.Add(local.Guid);
            if (!string.Equals(local.Version, remote.Version, StringComparison.Ordinal) && (!local.Clientside || !remote.Clientside))
            {
                mismatch = $"gameplay mod [{local.Name}] has version {local.Version}; host has {remote.Version}";
                return false;
            }
        }

        foreach (LobbyModMetadataEntry remote in remoteMods)
        {
            if (!matched.Contains(remote.Guid) && !remote.Clientside)
            {
                mismatch = $"host gameplay mod [{remote.Name}] is not installed locally";
                return false;
            }
        }

        mismatch = string.Empty;
        return true;
    }

    private static bool RejectLobby(CSteamID lobbyId, string reason)
    {
        LogHelper.Warning($"Lobby {lobbyId} rejected due to mod mismatch: {reason}");
        try
        {
            HUD_ConfirmationPopup.ShowOK("missing mods!", static () => { }, true, false);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to display the lobby mod mismatch message.");
        }

        return false;
    }

    /// <summary>
    /// Accepts the common Steam Workshop file-detail forms and returns one URL.
    /// Non-Steam or invalid links are not advertised.
    /// </summary>
    internal static string NormalizeWorkshopUrl(string? value)
    {
        if (!Uri.TryCreate(value?.Trim(), UriKind.Absolute, out Uri? uri))
        {
            return string.Empty;
        }

        bool steamHost = string.Equals(uri.Host, "steamcommunity.com", StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Host, "www.steamcommunity.com", StringComparison.OrdinalIgnoreCase);
        bool supportedScheme = string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
        string path = uri.AbsolutePath.TrimEnd('/');
        bool workshopPath = string.Equals(path, "/sharedfiles/filedetails", StringComparison.OrdinalIgnoreCase) || string.Equals(path, "/workshop/filedetails", StringComparison.OrdinalIgnoreCase);

        if (!steamHost || !supportedScheme || !workshopPath)
        {
            return string.Empty;
        }

        string query = uri.Query.TrimStart('?');
        foreach (string pair in query.Split('&'))
        {
            string[] parts = pair.Split(['='], 2);
            if (parts.Length == 2
                && string.Equals(parts[0], "id", StringComparison.OrdinalIgnoreCase)
                && ulong.TryParse(parts[1], out ulong workshopId)
                && workshopId > 0)
            {
                return $"https://steamcommunity.com/sharedfiles/filedetails/?id={workshopId}";
            }
        }

        return string.Empty;
    }

    private static string CleanLobbyMetadataText(string? value, int maxLength, string fallback = "")
    {
        string cleaned = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (cleaned.Length > maxLength)
        {
            cleaned = cleaned.Substring(0, maxLength);
        }

        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned;
    }

    // -------------------------------------------------------------------------
    // Custom packet registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Gets or creates an R3 observable event stream for a custom packet type, automatically assigning a stable unique network ID for the session.
    /// </summary>
    /// <remarks>
    /// This is the recommended entry point for mods that want to send and receive custom packets. 
    /// The returned <see cref="R3PacketEventHook{T}"/> exposes the assigned ID and an <c>Observable</c> that fires whenever a matching packet arrives.
    /// </remarks>
    /// <typeparam name="T">
    /// A class decorated with <c>[MessagePackObject]</c> that describes the packet payload.
    /// </typeparam>
    /// <returns>
    /// A strongly-typed <see cref="R3PacketEventHook{T}"/> containing the assigned packet ID and an observable stream.
    /// </returns>
    public R3PacketEventHook<T> GetPacketEventFor<T>() where T : class
    {
        Type packetType = typeof(T);
        short packetId;

        lock (_registryLock)
        {
            if (!_typeToIdRegistry.TryGetValue(packetType, out packetId))
            {
                if (_nextAvailablePacketId == short.MaxValue)
                {
                    // This is unrecoverable: every registered packet type from here on
                    // would alias an existing one, causing silent data corruption.
                    LogHelper.Error("Custom network packet ID range exhausted! Cannot register new packet types.");
                    throw new InvalidOperationException("Custom network packet ID range is full.");
                }

                packetId = _nextAvailablePacketId++;
                _typeToIdRegistry[packetType] = packetId;
                LogHelper.Information($"Dynamically registered packet type '{packetType.Name}' with ID: {packetId}");
            }
        }

        return GetPacketEventFor<T>((CustomNetworkPacketType)packetId);
    }

    /// <summary>
    /// Internal overload that creates or retrieves a handler by explicit ID.
    /// Prefer the parameter-less overload to avoid ID collisions.
    /// </summary>
    internal R3PacketEventHook<T> GetPacketEventFor<T>(CustomNetworkPacketType packetId)
    {
        short id = (short)packetId;

        if (_packetHandlers.TryGetValue(id, out IPacketR3Handler handler))
        {
            if (handler is R3PacketEventHook<T> typedHook)
                return typedHook;

            // Another type was already registered under this ID: this would cause
            // deserialization failures at runtime and must be caught early.
            throw new InvalidOperationException(
                $"Packet ID {id} is already registered with a different type ({handler.GetType().GenericTypeArguments[0].Name} vs {typeof(T).Name}). Ensure each packet type uses a unique ID.");
        }

        R3PacketEventHook<T> newHook = new R3PacketEventHook<T>(id);
        _packetHandlers[id] = newHook;
        LogHelper.Information($"Created R3 event stream for packet ID {id} [{typeof(T).Name}]");
        return newHook;
    }

    // -------------------------------------------------------------------------
    // Chore Handling
    // -------------------------------------------------------------------------

    internal static void PackScriptExtenderChore()
    {
        *GameNetworkAPI.Instance.CurrentPayloadSize = 0;

        byte[] payload = GameNetworkAPI.ChorePendingSendPayload ?? throw new InvalidOperationException("No pending Script Extender chore payload is available.");
        GameNetworkAPI.ChorePendingSendPayload = null;

        int payloadSize = payload.Length;
        if (payloadSize < sizeof(short) || payloadSize > GameNetworkAPI.MAX_CHORE_PAYLOAD_BYTES)
        {
            LogHelper.Warning($"Refusing to pack Chore 106 with invalid blob size {payloadSize}.");
            return;
        }

        IntPtr choreManager = (IntPtr)GameGlobalsManager.Instance.ChoreManagerVA;
        BulkChoreDetours.c_game_chore_transfer_field!(choreManager, (IntPtr)(&payloadSize), sizeof(int), 1, 0);
        fixed (byte* pPayload = payload)
        {
            BulkChoreDetours.c_game_chore_transfer_field!(choreManager, (IntPtr)pPayload, payloadSize, 1, 0);
        }
        *GameNetworkAPI.Instance.CurrentPayloadSize = sizeof(int) + payloadSize;
    }

    internal static void UnpackScriptExtenderChore()
    {
        *GameNetworkAPI.Instance.CurrentPayloadSize = 0;

        int payloadSize = 0;
        IntPtr choreManager = (IntPtr)GameGlobalsManager.Instance.ChoreManagerVA;
        BulkChoreDetours.c_game_chore_transfer_field!(choreManager, (IntPtr)(&payloadSize), sizeof(int), 1, 1);

        if (payloadSize < sizeof(short) || payloadSize > GameNetworkAPI.MAX_CHORE_PAYLOAD_BYTES)
        {
            LogHelper.Warning($"Received Chore 106 with invalid blob size {payloadSize}; discarding.");
            return;
        }

        byte[] buffer = new byte[payloadSize];
        fixed (byte* pBuffer = buffer)
        {
            BulkChoreDetours.c_game_chore_transfer_field!(choreManager, (IntPtr)pBuffer, payloadSize, 1, 1);
        }

        *GameNetworkAPI.Instance.CurrentPayloadSize = sizeof(int) + payloadSize;
        GameNetworkAPI.DispatchReceivedScriptExtenderPayload(buffer);
    }

    internal static void MeasureScriptExtenderChore()
    {
        *GameNetworkAPI.Instance.CurrentPayloadSize = 0;

        int recordSize = *GameNetworkAPI.Instance.CurrentIncomingRecordSize;
        if (recordSize < CHORE_RECORD_NATIVE_SIZE + sizeof(int))
        {
            LogHelper.Warning($"Rejecting Chore 106 with undersized native record ({recordSize} bytes).");
            return;
        }

        int blobSize = *GameNetworkAPI.Instance.IncomingPayload;
        *GameNetworkAPI.Instance.CurrentPayloadSize = recordSize - CHORE_RECORD_NATIVE_SIZE;
    }

    /// <summary>
    /// Queues a raw blob for delivery through the lockstep chore system. 
    /// Returns <c>false</c> (without sending) if the transport isn't ready or the blob is too large for a single chore slot, caller should fall back to Steam.
    /// </summary>
    internal static bool SendScriptExtenderChorePayload(byte[] payload)
    {
        if (GameGlobalsManager.Instance.ChoreManagerVA == 0)
        {
            LogHelper.Warning("Chore manager pointer unavailable; refusing chore send");
            return false;
        }

        if (payload.Length > MAX_CHORE_PAYLOAD_BYTES)
        {
            LogHelper.Warning($"Script Extender Chore payload {payload.Length}B exceeds safe cap ({MAX_CHORE_PAYLOAD_BYTES}B); refusing chore send");
            return false;
        }

        lock (EngineInterface.threadLock)
        {
            ChorePendingSendPayload = payload;
            BulkChoreDetours.c_game_queue_chore!((IntPtr)GameGlobalsManager.Instance.ChoreManagerVA, ChoreType.ScriptExtenderChore);
            ChorePendingSendPayload = null;
        }

        return true;
    }

    /// <summary>
    /// Splits the received [packetId][body] blob and hands it to the exact same
    /// dispatch entry point the Steam IL hook uses, so existing R3PacketEventHook{T}
    /// subscribers fire identically regardless of transport.
    /// </summary>
    internal static void DispatchReceivedScriptExtenderPayload(byte[] blob)
    {
        if (blob.Length < 2)
        {
            LogHelper.Warning($"Received malformed SE chore payload (len={blob.Length}), discarding");
            return;
        }

        short packetId = BitConverter.ToInt16(blob, 0);
        byte[] data = new byte[blob.Length - 2];
        Buffer.BlockCopy(blob, 2, data, 0, data.Length);

        LogHelper.Debug($"Dispatching SE chore packet, packetId={packetId}, size={data.Length}B");
        GameNetworkAPI.Instance.HandleRawPacket(packetId, data);
    }

    // -------------------------------------------------------------------------
    // Incoming packet dispatch
    // -------------------------------------------------------------------------

    /// <summary>
    /// Dispatches a raw incoming packet to its registered handler.
    /// Called by the IL hook that intercepts <c>Platform_Multiplayer.ProcessMessage</c>.
    /// </summary>
    /// <param name="packetId">The packet type ID read from the wire.</param>
    /// <param name="data">The raw payload bytes (does not include the 6-byte MPData header).</param>
    /// <param name="sender">
    /// Steam ID of the peer the packet arrived from, where the receive path can establish it.
    /// Callers that have no transport-level sender leave this <c>null</c>; handlers then treat
    /// the packet as coming from an unverified origin.
    /// </param>
    /// <returns>
    /// <c>true</c> if a handler was found and the packet was processed;
    /// <c>false</c> if no handler is registered for this ID (the game will ignore it).
    /// </returns>
    internal bool HandleRawPacket(short packetId, byte[] data, CSteamID? sender = null)
    {
        if (_packetHandlers.TryGetValue(packetId, out IPacketR3Handler handler))
        {
            LogHelper.Debug($"Dispatching raw packet ID={packetId}, size={data.Length}B, sender={(sender.HasValue ? sender.Value.ToString() : "unknown")}");
            handler.HandleRaw(data, sender);
            return true;
        }

        LogHelper.Debug($"No handler registered for packet ID={packetId}, ignoring");
        return false;
    }


    // -------------------------------------------------------------------------
    // Serialization helpers
    // -------------------------------------------------------------------------

    /// <summary>Serializes a MessagePack object to a byte array.</summary>
    /// <typeparam name="T">The type to serialize.</typeparam>
    /// <param name="packet">The object instance to serialize.</param>
    /// <returns>The MessagePack-encoded byte array.</returns>
    public static byte[] Serialize<T>(T packet)
    {
        return MessagePackSerializer.Serialize(packet);
    }

    /// <summary>Deserializes a MessagePack byte array into the specified type.</summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <param name="bytes">The bytes to deserialize.</param>
    /// <returns>The deserialized object, or <c>null</c> if deserialization fails.</returns>
    public static T? Deserialize<T>(byte[] bytes)
    {
       return MessagePackSerializer.Deserialize<T>(bytes);
    }

    // -------------------------------------------------------------------------
    // Chat / lobby message handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Handles an incoming in-game chat message. If the message begins with
    /// <see cref="COMMAND_PREFIX"/>, it is parsed as a command and raised on
    /// <see cref="NetworkR3EventHooks.OnReceiveInGameChatCommand"/>; otherwise it is
    /// raised on <see cref="NetworkR3EventHooks.OnReceiveInGameChatMessage"/>.
    /// </summary>
    public void HandleInGameChatMessage(string fromPlayerName, int fromPlayerId, string message, int duration)
    {
        LogHelper.Information($"fromPlayerName=[{fromPlayerName}], fromPlayerId={fromPlayerId}, message={message}, duration={duration}");

        if (string.IsNullOrEmpty(message))
        {
            LogHelper.Verbose("ignoring empty message");
            return;
        }

        if (message[0] == COMMAND_PREFIX)
        {
            string commandBody = message[1..].Trim();

            // Regex pattern to match text between double quotes
            string pattern = "\"([^\"]*)\"";

            // Extract matches (arguments)
            MatchCollection matches = Regex.Matches(commandBody, pattern);
            string[] args = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                args[i] = matches[i].Groups[1].Value;
            }
            
            string commandName = commandBody.Split(' ', '\"')[0];
            LogHelper.Verbose($"parsed command [{commandName}] with [{args.Length}] args");
            NetworkR3EventHooks.OnReceiveInGameChatCommand.Raise(new ReceiveInGameChatCommandEventArgs(EventHookPhase.Post, commandName, args, fromPlayerName, fromPlayerId));
            return;
        }
        NetworkR3EventHooks.OnReceiveInGameChatMessage.Raise(new ReceiveInGameChatMessageEventArgs(EventHookPhase.Post, fromPlayerName, fromPlayerId, message, duration));
    }

    /// <summary>
    /// Handles an outgoing in-game chat message, raising
    /// <see cref="NetworkR3EventHooks.OnSendInGameChatMessage"/> for subscribers.
    /// </summary>
    public void HandleSendInGameChatMessage(List<int> recipients, string message)
    {
        LogHelper.Information($"Message=[{message}], receivers={string.Join(", ", recipients)}");

        if (string.IsNullOrEmpty(message))
        {
            LogHelper.Verbose("ignoring empty message");
            return;
        }

        NetworkR3EventHooks.OnSendInGameChatMessage.Raise(new SendInGameChatMessageEventArgs(EventHookPhase.Post, recipients, message));
    }

    /// <summary>
    /// Handles a custom-info push to a specific lobby member (typically triggered when
    /// a new player joins). Raises <see cref="NetworkR3EventHooks.OnSendCustomInfoToLobbyMember"/>
    /// and then performs the mod-settings initial sync for the joining player.
    /// </summary>
    public void HandleSendCustomInfoToMember(Platform_Multiplayer.MPLobbyMember member)
    {
        LogHelper.Information($"member=[{member.name}]");

        NetworkR3EventHooks.OnSendCustomInfoToLobbyMember.Raise(new OnSendCustomInfoToLobbyMemberEventArgs(EventHookPhase.Post, member));

        GameXAMLManagerAPI.Instance.SyncSettingsToNewPlayer(member);
    }

    /// <summary>
    /// Handles an incoming lobby chat message. Commands (prefixed with <see cref="COMMAND_PREFIX"/>) are raised on
    /// <see cref="NetworkR3EventHooks.OnReceiveLobbyChatCommand"/>; plain messages
    /// on <see cref="NetworkR3EventHooks.OnReceiveLobbyChatMessage"/>.
    /// </summary>
    public void HandleLobbyChatMessage(string message, CSteamID Id)
    {
        LogHelper.Information($"Message=[{message}], steamId={Id}");

        if (string.IsNullOrEmpty(message))
        {
            LogHelper.Verbose("ignoring empty message");
            return;
        }

        if (message[0] == COMMAND_PREFIX)
        {
            // Regex pattern to match text between double quotes
            string pattern = "\"([^\"]*)\"";

            // Extract matches
            MatchCollection matches = Regex.Matches(message, pattern);
            string[] args = new string[matches.Count];
            for (int i = 0; i < matches.Count; i++)
            {
                args[i] = matches[i].Groups[1].Value;
            }

            LogHelper.Verbose($"parsed command with [{args.Length}] args");
            NetworkR3EventHooks.OnReceiveLobbyChatCommand.Raise(new ReceiveLobbyChatCommandEventArgs(EventHookPhase.Post, message, args, Id));
            return;
        }
        NetworkR3EventHooks.OnReceiveLobbyChatMessage.Raise(new ReceiveLobbyChatMessageEventArgs(EventHookPhase.Post, message, Id));
    }

    /// <summary>
    /// Handles an outgoing lobby chat message, raising
    /// <see cref="NetworkR3EventHooks.OnSendLobbyChatMessage"/> for subscribers.
    /// </summary>
    public void HandleSendLobbyChatMessage(string message)
    {
        LogHelper.Information($"Message=[{message}]");

        if (string.IsNullOrEmpty(message))
        {
            LogHelper.Verbose("ignoring empty message");
            return;
        }

        NetworkR3EventHooks.OnSendLobbyChatMessage.Raise(new SendLobbyChatMessageEventArgs(EventHookPhase.Post, message));
    }

    // -------------------------------------------------------------------------
    // Packet sending
    // -------------------------------------------------------------------------

    /// <summary>
    /// Serializes <paramref name="packet"/> and sends it to all players, choosing between the chore/lockstep transport and the direct Steam transport.
    /// </summary>
    /// <remarks>
    /// The chore transport rides the same tick-scheduled, ordered pipeline as native game state changes, so it's safe to use from non-deterministic call sites (e.g. UI button clicks) where raw Steam messaging could desync the sim. 
    /// It has a hard ~1.2KB payload cap; oversized packets automatically fall back to Steam.
    /// </remarks>
    /// <typeparam name="T">The MessagePack-serializable packet type.</typeparam>
    /// <param name="packet">The packet object to send.</param>
    /// <param name="packetId">The network ID that identifies this packet type on the wire.</param>
    /// <param name="viaChore">
    /// When <c>true</c>, sends through the lockstep chore system instead of directly over Steam.
    /// Falls back to Steam automatically if the chore transport is unavailable or the
    /// serialized payload is too large.
    /// </param>
    /// <param name="instantMessage">Only applies to the Steam fallback path.</param>
    public static void SendPacketToAllEx2<T>(T packet, short packetId, bool viaChore = false, bool instantMessage = false)
    {
        if (viaChore)
        {
            byte[] body = Serialize(packet);
            byte[] blob = new byte[2 + body.Length];
            BitConverter.GetBytes(packetId).CopyTo(blob, 0);
            Buffer.BlockCopy(body, 0, blob, 2, body.Length);

            if (SendScriptExtenderChorePayload(blob))
            {
                LogHelper.Debug($"Sent packet ID {packetId} via chore transport ({blob.Length}B)");
                return;
            }

            LogHelper.Warning($"Chore transport unavailable or payload too large for packet ID {packetId}; falling back to Steam");
        }

        SendPacketToAll(packet, packetId, instantMessage);
    }

    /// <summary>
    /// Sends a pre-constructed <see cref="Platform_Multiplayer.MPData"/> packet to all
    /// players in the current game session. (Except the sending player!)
    /// </summary>
    /// <param name="packet">The packet to send.</param>
    /// <param name="instantMessage">
    /// When <c>true</c>, bypasses the game's normal packet queue and sends immediately.
    /// </param>
    public static void SendPacketToAllEx(Platform_Multiplayer.MPData packet, bool instantMessage = false)
    {
        if (!IsNetworkedEnvironment())
        {
            LogHelper.Warning("called outside a networked environment: packet dropped");
            return;
        }
        Platform_Multiplayer.instance.SendPacketToAll(packet, instantMessage);
    }

    /// <summary>
    /// Serializes <paramref name="packet"/> and sends it to all players in the current game session.  (Except the sending player!)
    /// </summary>
    /// <typeparam name="T">The MessagePack-serializable packet type.</typeparam>
    /// <param name="packet">The packet object to send.</param>
    /// <param name="packetId">The network ID that identifies this packet type on the wire.</param>
    /// <param name="instantMessage">
    /// When <c>true</c>, bypasses the game's normal packet queue and sends immediately.
    /// </param>
    public static void SendPacketToAll<T>(T packet, short packetId, bool instantMessage = false)
    {
        byte[] bytes = Serialize(packet);
        SendPacketToAllEx(new Platform_Multiplayer.MPData()
        {
            data = bytes,
            dataLength = bytes.Length,
            dataOffset = 0,
            packetType = packetId
        }, instantMessage);
    }

    /// <summary>
    /// Sends a pre-constructed <see cref="Platform_Multiplayer.MPData"/> packet to a
    /// specific player by their game player ID.
    /// </summary>
    /// <param name="playerId">The target player's in-game ID (1–8).</param>
    /// <param name="packet">The packet to send.</param>
    public static void SendPacketToPlayerIdEx(int playerId, Platform_Multiplayer.MPData packet)
    {
        if (!IsNetworkedEnvironment())
        {
            LogHelper.Warning($"called outside a networked environment: packet to player {playerId} dropped");
            return;
        }
        Platform_Multiplayer.instance.SendPacketToPlayerID(playerId, packet);
    }

    /// <summary>
    /// Serializes <paramref name="packet"/> and sends it to a specific player by game player ID.
    /// </summary>
    /// <typeparam name="T">The MessagePack-serializable packet type.</typeparam>
    /// <param name="playerId">The target player's in-game ID (1–8).</param>
    /// <param name="packet">The packet object to send.</param>
    /// <param name="packetId">The network ID that identifies this packet type on the wire.</param>
    public static void SendPacketToPlayerId<T>(int playerId, T packet, short packetId)
    {
        byte[] bytes = Serialize(packet);
        SendPacketToPlayerIdEx(playerId, new Platform_Multiplayer.MPData()
        {
            data = bytes,
            dataLength = bytes.Length,
            dataOffset = 0,
            packetType = packetId
        });
    }

    /// <summary>
    /// Sends a packet to all other players in a lobby-aware manner.
    /// <list type="bullet">
    ///   <item>
    ///     <b>In-game</b>: delegates to <see cref="Platform_Multiplayer.SendPacketToAll"/>,
    ///     which uses the established <c>gameMembers</c> connection list.
    ///   </item>
    ///   <item>
    ///     <b>Lobby</b>: iterates <c>activeLobby.members</c> and sends directly via
    ///     <c>SteamNetworkingMessages</c> on channel 2 (the channel the game polls for
    ///     <c>MPData</c> packets).
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="packet">The packet to broadcast.</param>
    public static void SendPacketToAllLobby(Platform_Multiplayer.MPData packet)
    {
        Platform_Multiplayer mp = Platform_Multiplayer.instance;

        if (mp == null)
        {
            LogHelper.Warning("Platform_Multiplayer instance is null. Packet dropped.");
            return;
        }

        // In-game path: gameMembers list is populated after game start
        if (mp.gameMembers != null)
        {
            LogHelper.Debug($"Using in-game path (gameMembers count={mp.gameMembers.Count})");
            mp.SendPacketToAll(packet, false);
            return;
        }

        // Lobby path: send via Steam networking directly on channel 2
        if (mp.activeLobby?.members == null)
        {
            LogHelper.Warning("No active lobby or member list is null. Packet dropped.");
            return;
        }

        byte[] rawBytes = packet.ToBytes();
        int sent = 0;
        foreach (Platform_Multiplayer.MPLobbyMember member in mp.activeLobby.members)
        {
            if (member.IsSelf() || member.SkirmishMember)
                continue;

            SteamNetworkingIdentity identity = default;
            identity.SetSteamID(member.id);

            fixed (byte* ptr = rawBytes)
            {
                // 2=MPData channel
                EResult result = SteamNetworkingMessages.SendMessageToUser(ref identity, (IntPtr)ptr, (uint)rawBytes.Length, (int)(SteamNetworkingSend.Reliable | SteamNetworkingSend.AutoRestartBrokenSession), 2);

                if (result != EResult.k_EResultOK)
                    LogHelper.Warning($"SendMessageToUser failed for [{member.name}] (steamId={member.id}) - EResult={result}");
                else
                    sent++;
            }
        }

        LogHelper.Debug($"Sent to [{sent}] lobby member(s), packetType={packet.packetType}, size={rawBytes.Length}B");
    }

    /// <summary>
    /// Sends a packet to a specific lobby member identified by their <see cref="CSteamID"/>.
    /// <list type="bullet">
    ///   <item>
    ///     <b>In-game</b>: resolves the Steam ID to a <c>gameMembers</c> entry and uses
    ///     <see cref="SendPacketToPlayerIdEx"/>.
    ///   </item>
    ///   <item>
    ///     <b>Lobby</b>: sends directly via <c>SteamNetworkingMessages</c> on channel 2,
    ///     with a pinned GC handle to prevent the GC from moving the buffer mid-send.
    ///   </item>
    /// </list>
    /// </summary>
    /// <param name="target">The Steam ID of the recipient.</param>
    /// <param name="packet">The pre-constructed MPData packet to send.</param>
    public static void SendPacketToSteamId(CSteamID target, Platform_Multiplayer.MPData packet)
    {
        Platform_Multiplayer mp = Platform_Multiplayer.instance;

        if (mp == null)
        {
            LogHelper.Warning($"Platform_Multiplayer instance is null — packet to [{target}] dropped");
            return;
        }

        // In-game path: resolve to a game member and use the normal send route
        Platform_Multiplayer.MPGameMember? gameMember = mp.gameMembers?.Find(m => m.steamID == target.m_SteamID);

        if (gameMember != null)
        {
            LogHelper.Debug($"In-game path, resolving [{target}] to playerId={gameMember.playerID}");
            SendPacketToPlayerIdEx(gameMember.playerID, packet);
            return;
        }

        // Lobby path: send directly via Steam networking
        LogHelper.Debug($"Lobby path, sending directly to {target}");

        byte[] rawBytes = packet.ToBytes();
        SteamNetworkingIdentity identity = default;
        identity.SetSteamID(target);

        GCHandle handle = GCHandle.Alloc(rawBytes, GCHandleType.Pinned);
        try
        {
            EResult result = SteamNetworkingMessages.SendMessageToUser(ref identity, handle.AddrOfPinnedObject(), (uint)rawBytes.Length, (int)(SteamNetworkingSend.Reliable | SteamNetworkingSend.AutoRestartBrokenSession), 2);

            if (result != EResult.k_EResultOK)
            {
                LogHelper.Warning($"SendMessageToUser to [{target}] failed: EResult={result}");
            }
        }
        finally
        {
            handle.Free();
        }
    }

    /// <summary>
    /// Returns all players in the current game session, or <c>null</c> if not in-game.
    /// </summary>
    public static List<Platform_Multiplayer.MPGameMember>? GetPlayers()
    {
        if (!IsNetworkedEnvironment())
            return null;

        return Platform_Multiplayer.instance.gameMembers;
    }

    /// <summary>
    /// Returns the <see cref="Platform_Multiplayer.MPGameMember"/> for a given player ID,
    /// or <c>null</c> if the player is not found or not in a networked game.
    /// </summary>
    /// <param name="playerId">The in-game player ID (1–8) to look up.</param>
    public static Platform_Multiplayer.MPGameMember? GetPlayerById(int playerId)
    {
        if (!IsNetworkedEnvironment())
            return null;

        return Platform_Multiplayer.instance.getPlayer(playerId);
    }

    /// <summary>
    /// Returns <c>true</c> if the local client is currently in an active networked session
    /// (either in the lobby or after game start). Concidentally, this should always be true even in skirmishes.
    /// Shouldnt be a problem, however.
    /// </summary>
    public static bool IsNetworkedEnvironment()
    {
        Platform_Multiplayer? instance = Platform_Multiplayer.instance;
        if (instance == null) 
            return false;

        // We are networked if we are in a Lobby OR if the Game has started (gameMembers exists)
        return instance.activeLobby != null || instance.gameMembers != null;
    }

    /// <summary>
    /// Returns <c>true</c> if the local client is currently in an active multiplayer game session.
    /// </summary>
    /// <returns></returns>
    public static bool IsMultiplayerGame()
    {
        return Platform_Multiplayer.MPGameActive || (Director.instance != null && Director.instance.MultiplayerGame);
    }

    /// <summary>
    /// Returns <c>true</c> if the local client is currently inside an active Steam lobby
    /// (pre-game waiting room). Returns <c>false</c> and logs a warning if not.
    /// </summary>
    public static bool IsLobbyEnvironment()
    {
        Platform_Multiplayer? instance = Platform_Multiplayer.instance;
        if (instance == null || instance.activeLobby == null)
        {
            LogHelper.Debug($"Usage in non-networked lobby environment!");
            return false;
        }
        return true;
    }

    /// <summary>
    /// Mutes or unmutes the in-game chat for a specific player.
    /// </summary>
    /// <param name="playerId">The ID of the player to mute/unmute.</param>
    /// <param name="muted">The new mute status.</param>
    public static void SetChatMute(int playerId, bool muted)
    {
        if (!IsNetworkedEnvironment())
            return;

        Platform_Multiplayer.instance.SetChatMute(playerId, muted);
    }

    /// <summary>
    /// Checks if a specific player is currently chat-muted.
    /// </summary>
    /// <param name="playerId">The ID of the player to check.</param>
    /// <returns><c>true</c> if the player is muted; otherwise, <c>false</c>.</returns>
    public static bool IsChatMute(int playerId)
    {
        if (!IsNetworkedEnvironment())
            return false;

        return Platform_Multiplayer.instance.IsChatMute(playerId);
    }

    /// <summary>
    /// Checks if the current player is the host.
    /// Will always return true in singleplayer.
    /// </summary>
    /// <returns><c>true</c> if the player is the host; otherwise, <c>false</c>.</returns>
    public static bool IsLocalHost()
    {
        // If not networked (Singleplayer), we are the host.
        if (!IsNetworkedEnvironment())
            return true;

        // In the Lobby stage
        if (Platform_Multiplayer.instance.activeLobby != null)
        {
            return Platform_Multiplayer.instance.activeLobby.isHost;
        }

        // In the Game stage
        return Platform_Multiplayer.instance.IsHost;
    }

    /// <summary>
    /// Returns the Steam ID of the current lobby's owner (the host), or <c>null</c> when there is
    /// no active lobby.
    /// </summary>
    public static CSteamID? GetHostSteamId()
    {
        Platform_Multiplayer.MPLobby? lobby = Platform_Multiplayer.instance?.activeLobby;
        if (lobby == null)
            return null;

        CSteamID owner = SteamMatchmaking.GetLobbyOwner(lobby.id);
        return owner.IsValid() ? owner : null;
    }

    /// <summary>
    /// Resolves a Steam ID to its final player slot (1-8), or -1 if the slot cannot be established.
    /// A -1 result means not resolvable.
    /// </summary>
    public static int GetPlayerIdForSteamId(CSteamID steamId)
    {
        Platform_Multiplayer? mp = Platform_Multiplayer.instance;
        if (mp == null)
        {
            LogHelper.Debug($"No Platform_Multiplayer instance: cannot resolve [{steamId}]");
            return -1;
        }

        // In-game: the roster carries the slot each player was actually assigned at map start.
        if (mp.gameMembers != null)
        {
            Platform_Multiplayer.MPGameMember? member = mp.gameMembers.Find(m => m.steamID == steamId.m_SteamID);

            if (member != null && member.playerID > 0)
            {
                LogHelper.Debug($"Resolved [{steamId}] from game roster: playerId={member.playerID}");
                return member.playerID;
            }

            LogHelper.Debug($"SteamID [{steamId}] is not in the active game roster");
            return -1;
        }

        // Lobby: the host-published slot table.
        Platform_Multiplayer.MPLobby? lobby = mp.activeLobby;
        if (lobby == null)
        {
            LogHelper.Debug($"No active lobby: cannot resolve [{steamId}]");
            return -1;
        }

        int slot = lobby.getThisPlayerFromSteamID(steamId.m_SteamID);
        if (slot > 0)
        {
            LogHelper.Debug($"Resolved [{steamId}] from lobby slot table: playerId={slot}");
            return slot;
        }

        LogHelper.Debug($"SteamID [{steamId}] has no slot in the lobby table (not a participant, or the table has not arrived yet)");
        return -1;
    }

    /// <summary>
    /// Returns <c>true</c> once an authoritative slot mapping exists, or rather: the game roster has been built or the host slot table has been received.
    /// While this is <c>false</c>, a -1 from <see cref="GetPlayerIdForSteamId"/> means only that the mapping has not arrived yet. 
    /// Callsites that would otherwise reject a packet should defer it instead.
    /// </summary>
    public static bool IsPlayerSlotMappingAvailable()
    {
        Platform_Multiplayer? mp = Platform_Multiplayer.instance;
        if (mp == null)
            return false;

        if (mp.gameMembers != null)
            return true;

        ulong[]? mapping = mp.activeLobby?.this_player_to_SteamID_mapping;
        if (mapping == null)
            return false;

        for (int i = 0; i < mapping.Length; i++)
        {
            if (mapping[i] != 0)
                return true;
        }

        return false;
    }


    /// <summary>
    /// Gets the local player id (unity-side)
    /// Do not use early.
    /// </summary>
    /// <returns>Returns the local player id; Otherwise -1</returns>
    public static int GetLocalPlayerId()
    {
        if (IsNetworkedEnvironment())
        {
            CSteamID localSteamId = SteamUser.GetSteamID();
            int localId = GetPlayerIdForSteamId(localSteamId);

            if (localId < 0)
                LogHelper.Warning($"Local SteamID [{localSteamId}] has no assigned slot yet: returning -1");

            return localId;
        }

        // In-game: trust EditorDirector
        int edId = EditorDirector.instance != null ? EditorDirector.instance.gameLocalPlayerID : -1;
        if (edId <= 0)
            LogHelper.Warning($"EditorDirector returned invalid ID {edId} outside lobby phase");
        else
            LogHelper.Debug($"In-game: EditorDirector returned {edId}");

        return edId;
    }


    // -------------------------------------------------------------------------
    // Misc player controls
    // -------------------------------------------------------------------------

    /// <summary>
    /// Forces the local player to leave the current game session.
    /// </summary>
    public static void LeaveGame()
    {
        if (!IsNetworkedEnvironment())
            return;

        Platform_Multiplayer.instance.LeaveGame();
    }

    /// <summary>
    /// Gets the number of players currently active in the game (does not include kicked players).
    /// </summary>
    /// <returns>The number of active players, or -1 if not in a networked game.</returns>
    public static int GetNumActivePlayers()
    {
        if (!IsNetworkedEnvironment())
            return -1;

        return Platform_Multiplayer.instance.GetNumActivePlayers();
    }

    /// <summary>
    /// Kicks a player from the game. This can only be performed by the host.
    /// </summary>
    /// <param name="playerId">The ID of the player to kick.</param>
    public static void KickPlayerFromGame(int playerId)
    {
        if (!IsNetworkedEnvironment())
            return;

        Platform_Multiplayer.instance.kickPlayerFromGame(playerId);
    }

    /// <summary>
    /// Sends an in-game chat message to a specific list of recipients.
    /// </summary>
    /// <param name="recipients">A list of player IDs who should receive the message.</param>
    /// <param name="message">The message content.</param>
    public static void SendIngameChat(List<int> recipients, string message)
    {
        if (!IsNetworkedEnvironment())
            return;

        Platform_Multiplayer.instance.SendIngameChat(recipients, message);
    }

    /// <summary>
    /// Sends a pre-defined in-game "insult" or taunt to a specific list of recipients.
    /// </summary>
    /// <param name="recipients">A list of player IDs who should receive the insult.</param>
    /// <param name="insult">The ID of the insult to send.</param>
    public static void SendIngameChatInsult(List<int> recipients, int insult)
    {
        if (!IsNetworkedEnvironment())
            return;

        Platform_Multiplayer.instance.SendIngameChatInsult(recipients, insult);
    }

#pragma warning restore 0618
}
