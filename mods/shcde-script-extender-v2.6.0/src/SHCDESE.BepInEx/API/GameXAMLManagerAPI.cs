using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using MessagePack;
using R3;
using SHCDESE.API.Components.Assets;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.API.Components.Noesis;
using SHCDESE.API.Components.Noesis.XAML;
using SHCDESE.EventAPI;
using SHCDESE.Logging;
using SHCDESE.NoesisUtil;
using SHCDESE.UI;
using SHCDESE.ViewModels;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Xml;
using System.Xml.Linq;

namespace SHCDESE.API;

/// <summary>
/// Provides high-level APIs for Noesis/XAML integration and multiplayer mod settings sync.
/// <para>
/// Responsibilities:
/// <list type="bullet">
///   <item>Loading and binding mod XAML views to their ViewModels.</item>
///   <item>Injecting mod ViewModels into game-owned Noesis UI trees.</item>
///   <item>Applying XAML patch files contributed by mods.</item>
///   <item>
///     Synchronising mod settings across lobby members via <see cref="LobbyModSettingSyncPacket"/>,
///     honouring <see cref="SyncPerPlayerAttribute"/> and <see cref="SyncHostOnlyAttribute"/>.
///   </item>
/// </list>
/// </para>
/// </summary>
public sealed class GameXAMLManagerAPI
{
    // -------------------------------------------------------------------------
    // Singleton
    // -------------------------------------------------------------------------

    private static readonly Lazy<GameXAMLManagerAPI> _lazy = new(() => new GameXAMLManagerAPI());

    /// <summary>Gets the singleton instance of <see cref="GameXAMLManagerAPI"/>.</summary>
    public static GameXAMLManagerAPI Instance => _lazy.Value;

    // -------------------------------------------------------------------------
    // Constants
    // -------------------------------------------------------------------------

    internal const string DEFAULT_XAML_PATCH_RELATIVE_PATH = "Patches";

    // -------------------------------------------------------------------------
    // Private state
    // -------------------------------------------------------------------------

    /// <summary>
    /// Maps Noesis element names (x:Name) to the ViewModel that should be bound to them.
    /// Populated via <see cref="RegisterBinding"/> and applied whenever a new Noesis
    /// view finishes loading.
    /// </summary>
    private Dictionary<string, object> _bindingRegistry = new Dictionary<string, object>();

    /// <summary>All mod settings registrations, exposed to the hub ViewModel for tab display.</summary>
    private ObservableCollection<LobbyModSettingsEntry> _registeredModSettings = new();

    /// <summary>
    /// Public read-only view of the registered mod settings list.
    /// Bound to the mod-settings hub UI so it automatically reflects new registrations.
    /// </summary>
    public ObservableCollection<LobbyModSettingsEntry> RegisteredModSettings => _registeredModSettings;

    /// <summary>
    /// Set to <c>true</c> while <see cref="ReceiveSettingsUpdate"/> is applying an
    /// incoming network update, preventing that update from triggering a redundant
    /// outgoing broadcast.
    /// </summary>
    private bool _isProcessingNetworkSync = false;

    /// <summary>
    /// The dynamically assigned network packet ID for <see cref="LobbyModSettingSyncPacket"/>.
    /// Populated by <see cref="SetupNetworking"/>; zero until then.
    /// </summary>
    private short _syncPacketId;

    /// <summary>
    /// Maps mod name -> its dedicated storage instance.
    /// Populated in RegisterModSettings; one entry per registered mod.
    /// </summary>
    private readonly Dictionary<string, LobbyModSettingsStorage> _storageMap = [];

    /// <summary>
    /// Per-player updates that arrived before an authoritative slot mapping existed.
    /// Held until the mapping is published rather than written to a guessed slot.
    /// Superseded entries (same mod, property and sender) are replaced, so this holds
    /// at most the latest value per sender and stays small (hopefully)
    /// </summary>
    private readonly List<DeferredPerPlayerUpdate> _deferredPerPlayerUpdates = [];
    private const int MAX_DEFERRED_PER_PLAYER_UPDATES = 64;
    private bool _isFlushingDeferredUpdates = false;

    private sealed class DeferredPerPlayerUpdate
    {
        public LobbyModSettingSyncPacket Packet = null!;
        public CSteamID Sender;
    }


    // -------------------------------------------------------------------------
    // Initialization
    // -------------------------------------------------------------------------

    private GameXAMLManagerAPI()
    {

    }

    /// <summary>
    /// Wires up the XAML asset processing pipeline. Called once during plugin startup,
    /// before the game loads any XAML assets.
    /// </summary>
    internal void Setup()
    {
        GameAssetManagerAPI.Instance.OnTextFileAssetProcess += OnTextFileAssetProcess;
    }

    /// <summary>
    /// Registers the <see cref="LobbyModSettingSyncPacket"/> with the network layer and
    /// subscribes to its R3 observable. Must be called after <see cref="GameNetworkAPI.InitializeSubscribers"/> has run.
    /// </summary>
    internal void SetupNetworking()
    {
        // Register for custom packets using our GameNetworkAPI
        R3PacketEventHook<LobbyModSettingSyncPacket> hook = GameNetworkAPI.Instance.GetPacketEventFor<LobbyModSettingSyncPacket>();
        _syncPacketId = hook.GetPacketId();
        LogHelper.Information($"Mod-settings sync packet registered with ID={_syncPacketId}");

        hook.GetBaseHook().Observable.Subscribe(packet =>
        {
            ReceiveSettingsUpdate(packet.Packet, packet.SenderSteamId);
        });
    }

    // -------------------------------------------------------------------------
    // Mod settings registration
    // -------------------------------------------------------------------------

    /// <summary>
    /// Registers a multiplayer lobby settings panel for a mod.
    /// <para>
    /// This method loads the XAML view, binds the ViewModel to it, adds the entry to
    /// the hub's tab list, and hooks <see cref="INotifyPropertyChanged.PropertyChanged"/>
    /// to automatically broadcast attributed property changes to other players.
    /// </para>
    /// </summary>
    /// <param name="plugin">The BepInEx plugin instance that owns these settings.</param>
    /// <param name="modName">
    /// A unique display name for the mod. Also used as the key in sync packets —
    /// must match exactly on all clients.
    /// </param>
    /// <param name="viewModel">
    /// The ViewModel instance. Should implement <see cref="INotifyPropertyChanged"/>
    /// and extend <see cref="LobbyModSettingsBaseViewModel"/>. Properties decorated with
    /// <see cref="SyncPerPlayerAttribute"/> or <see cref="SyncHostOnlyAttribute"/> are
    /// synchronised and persisted automatically; see <see cref="LobbyModSettingsRouting"/>
    /// for persisting a value without synchronising it, or the reverse.
    /// </param>
    /// <param name="xamlSourceFile">
    /// Mod-relative path to the XAML file describing the settings UI
    /// (e.g. <c>"MyModUI/ModSettings.xaml"</c>).
    /// </param>
    public void RegisterLobbyModSettings(BaseUnityPlugin plugin, string modName, object viewModel, string xamlSourceFile)
    {
        try
        {
            if (!GameAssetManagerAPI.Instance.GetModifiedFilePath(xamlSourceFile, out string absoluteXamlSourceFile))
            {
                LogHelper.Error($"[{modName}]: Could not find xaml source file: [{xamlSourceFile}]");
                return;
            }

            // Restore saved values before the view binds so the UI
            // starts up showing the player's previous choices.
            LobbyModSettingsStorage storage = new LobbyModSettingsStorage(plugin.Info.Location, modName);
            _storageMap[modName] = storage;
            storage.Load(viewModel);

            // Noesis stuff
            Noesis.FrameworkElement view = (Noesis.FrameworkElement)Noesis.GUI.LoadXaml(new FileStream(absoluteXamlSourceFile, FileMode.Open), xamlSourceFile);
            view.DataContext = viewModel;

            // Keeps ComboBox drop-downs from opening and instantly closing again
            // when they sit low enough in the panel that the popup would be repositioned.
            ComboBoxDropDownFix.Attach(view);

            _registeredModSettings.Add(new LobbyModSettingsEntry(plugin, modName, viewModel, view));
            LogHelper.Information($"Registered mod [{modName}]");

            if (viewModel is INotifyPropertyChanged notify)
            {
                notify.PropertyChanged += (s, e) => {
                    LogHelper.Debug($"[{modName}] PropertyChanged: [{e.PropertyName}], networkSyncInProgress={_isProcessingNetworkSync}");

                    // Suppress broadcasts that originate from our own incoming sync to avoid an echo loop.
                    if (_isProcessingNetworkSync)
                        return;

                    // A ViewModel raising a revert notification is snapping its UI back to the
                    // authoritative value after a rejected edit. Nothing changed, so there is
                    // nothing to broadcast and nothing to persist.
                    if (s is LobbyModSettingsBaseViewModel revertingVm && revertingVm.IsSuppressingSync)
                    {
                        LogHelper.Debug($"[{modName}]: [{e.PropertyName}] is a UI revert, ignoring");
                        return;
                    }

                    // Classify the property before anything downstream treats this notification as
                    // a settings change. A ViewModel raises PropertyChanged for its own presentation
                    // state too - enablement flags, visibility, computed labels - which is routed
                    // nowhere. Those have to drop out here, or each one is reported as a failed sync
                    // attempt and triggers a full settings snapshot to disk.
                    if (s == null)
                    {
                        LogHelper.Warning($"[{modName}]: PropertyChanged for [{e.PropertyName}] raised without a sender, ignoring");
                        return;
                    }

                    if (!TryGetSettingProperty(s, e.PropertyName, out PropertyInfo? prop))
                    {
                        LogHelper.Debug($"[{modName}]: [{e.PropertyName}] is UI-only, ignoring");
                        return;
                    }

                    // Authorisation is checked here rather than only inside BroadcastSettingChange,
                    // because an unauthorised change must not be written to disk either. Otherwise a
                    // client's rejected host-only value would still be persisted locally and restored
                    // on next launch, silently diverging from the host.
                    if (!IsLocallyAuthoritative(prop))
                    {
                        LogHelper.Warning($"[{modName}]: unauthorised local change to host-only property [{e.PropertyName}]; not broadcast, not persisted");
                        return;
                    }

                    // Synchronisation and persistence are independent, so each is asked for
                    // separately: a local setting is stored without ever reaching the network,
                    // and a transient synced setting is sent without ever reaching the disk.
                    if (prop.IsSynced())
                        BroadcastSettingChange(modName, s, prop);

                    if (prop.IsPersisted())
                        storage.Save(s);
                };
            }
            else
            {
                LogHelper.Warning($"[{modName}]: ViewModel does not implement INotifyPropertyChanged — property changes will not be synced");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error($"[{modName}] Exception during registration: {ex.ToString()}");
        }
    }

    // -------------------------------------------------------------------------
    // Outgoing sync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Resolves <paramref name="propertyName"/> on <paramref name="viewModel"/> and reports whether
    /// that property is a setting at all.
    /// <para>
    /// It is one if <see cref="LobbyModSettingsRouting"/> routes it anywhere, to the network or to
    /// disk. Everything else is presentation state: undecorated properties, and names that do not
    /// resolve to a public instance property. Both are rejected here, before any networking or
    /// storage decision is made.
    /// </para>
    /// </summary>
    /// <param name="viewModel">The ViewModel that raised the change notification.</param>
    /// <param name="propertyName">
    /// The notified property name. An empty name is the "every property changed" convention and
    /// names no single setting to route, so it is not treated as a settings change.
    /// </param>
    /// <param name="prop">The resolved property, or <c>null</c> when this returns <c>false</c>.</param>
    /// <returns><c>true</c> if the property resolved and is synchronised, persisted, or both.</returns>
    private static bool TryGetSettingProperty(object viewModel, string? propertyName, [NotNullWhen(true)] out PropertyInfo? prop)
    {
        prop = null;

        if (string.IsNullOrEmpty(propertyName))
            return false;

        PropertyInfo? resolved = viewModel.GetType().GetProperty(propertyName);
        if (resolved == null || resolved.IsUIOnly())
            return false;

        prop = resolved;
        return true;
    }

    /// <summary>
    /// Determines whether the local player is allowed to originate a change to <paramref name="prop"/>.
    /// <para>
    /// Returns <c>false</c> only for <see cref="SyncHostOnlyAttribute"/> properties changed by a
    /// non-host inside a networked session. Per-player properties are always locally authoritative,
    /// as is everything in singleplayer.
    /// </para>
    /// <para>
    /// Callers pass an already-resolved setting property; presentation state and unresolvable names
    /// are filtered out by <see cref="TryGetSettingProperty"/> before authorisation is considered.
    /// </para>
    /// </summary>
    private static bool IsLocallyAuthoritative(PropertyInfo prop)
    {
        if (!prop.IsHostOnly())
            return true;

        if (!GameNetworkAPI.IsNetworkedEnvironment())
            return true;

        return GameNetworkAPI.IsLocalHost();
    }

    /// <summary>
    /// Evaluates whether a changed property should be broadcast to other players,
    /// then serialises and sends it if so.
    /// <para>
    /// Routing rules:
    /// <list type="bullet">
    ///   <item><see cref="SyncPerPlayerAttribute"/>: any player may broadcast their own value.</item>
    ///   <item><see cref="SyncHostOnlyAttribute"/>: only the host may broadcast.</item>
    ///   <item>No attribute: not synced; such properties never reach this method.</item>
    /// </list>
    /// </para>
    /// <para>
    /// Sync attribution is settled before any networking state is consulted, so a property that is
    /// not synchronised in the first place is never reported as an attempted sync.
    /// </para>
    /// </summary>
    private void BroadcastSettingChange(string modName, object viewModel, PropertyInfo prop)
    {
        string propertyName = prop.Name;

        // Whether the property is synced at all is a property of the ViewModel, not of the current
        // session, so it is decided first. Callers already filter these out; this keeps the method
        // correct in isolation.
        if (!prop.IsSynced())
        {
            LogHelper.Debug($"[{modName}]: [{propertyName}] is not synchronised, skipping");
            return;
        }

        bool isHostOnly = prop.IsHostOnly();

        // Guard: not in a multiplayer session. Normal whenever the player is in singleplayer or the
        // main menu, so a synced property changing here is expected rather than a issue.
        if (!GameNetworkAPI.IsNetworkedEnvironment())
        {
            LogHelper.Debug($"[{modName}]: [{propertyName}] not broadcast, no networked session");
            return;
        }

        // Guard: networked, but the packet type has not been registered yet.
        if (_syncPacketId == 0)
        {
            LogHelper.Warning($"[{modName}] Attempting to sync property [{propertyName}] before sync packed id is assigned");
            return;
        }

        if (isHostOnly && !GameNetworkAPI.IsLocalHost())
        {
            // A client managed to trigger a host-only property change.
            LogHelper.Warning($"[{modName}]: client attempted to broadcast host-only property [{propertyName}]");
            return;
        }

        int localPlayerId = GameNetworkAPI.GetLocalPlayerId();
        LogHelper.Debug($"[{modName}]: broadcasting [{propertyName}] (hostOnly={isHostOnly}) from playerId={localPlayerId}");

        if (!isHostOnly && localPlayerId < 0)
        {
            LogHelper.Warning($"[{modName}]: no player slot assigned yet, skipping broadcast of per-player property [{propertyName}]");
            return;
        }

        try
        {
            LobbyModSettingSyncPacket packet = new LobbyModSettingSyncPacket
            {
                ModName = modName,
                PropertyName = propertyName,
                SerializedValue = MessagePackSerializer.Serialize(prop.GetValue(viewModel)),
                TypeName = prop.PropertyType.AssemblyQualifiedName,
                SourcePlayerId = localPlayerId
            };

            byte[] bytes = MessagePackSerializer.Serialize(packet);
            Platform_Multiplayer.MPData mpData = new Platform_Multiplayer.MPData
            {
                packetType = _syncPacketId,
                data = bytes,
                dataLength = bytes.Length
            };

            GameNetworkAPI.SendPacketToAllLobby(mpData);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"[{modName}]: failed to serialize or send [{propertyName}]");
        }
    }

    // -------------------------------------------------------------------------
    // Incoming sync
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies an incoming <see cref="LobbyModSettingSyncPacket"/> to the appropriate
    /// ViewModel, then triggers any necessary Noesis binding refreshes.
    /// <para>
    /// While this method runs, <see cref="_isProcessingNetworkSync"/> is set to prevent
    /// the resulting <see cref="INotifyPropertyChanged.PropertyChanged"/> events from
    /// triggering a redundant outgoing broadcast.
    /// </para>
    /// </summary>
    private void ReceiveSettingsUpdate(LobbyModSettingSyncPacket packet, CSteamID? sender)
    {
        LogHelper.Debug($"Mod=[{packet.ModName}], property=[{packet.PropertyName}], sourcePlayer={packet.SourcePlayerId}, sender={(sender.HasValue ? sender.Value.ToString() : "unknown")}");

        LobbyModSettingsEntry entry = _registeredModSettings.FirstOrDefault(x => x.Name == packet.ModName);
        if (entry == null)
        {
            LogHelper.Warning($"No registered mod found with name [{packet.ModName}]. Packet ignored.");
            return;
        }

        object vm = entry.ViewModel;
        PropertyInfo prop = vm.GetType().GetProperty(packet.PropertyName);
        if (prop == null)
        {
            LogHelper.Warning($"[{packet.ModName}]: Property [{packet.PropertyName}] not found on ViewModel [{vm.GetType().Name}]. Packet ignored.");
            return;
        }

        if (!prop.IsSynced())
        {
            // We received a packet for a property that is not part of the protocol.
            // This shouldn't happen if both sides are running the same mod version.
            LogHelper.Warning($"[{packet.ModName}]: Property [{packet.PropertyName}] is not synchronised. Packet ignored. Version mismatch?");
            return;
        }

        // Resolve the TypeName back to a .NET type for deserialization
        Type? targetType = Type.GetType(packet.TypeName);
        if (targetType == null)
        {
            LogHelper.Error($"[{packet.ModName}]: cannot resolve type [{packet.TypeName}] for property [{packet.PropertyName}]. Packet ignored.");
            return;
        }

        object? val;
        try
        {
            val = MessagePackSerializer.Deserialize(targetType, packet.SerializedValue);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"[{packet.ModName}]: deserialization failed for property [{packet.PropertyName}]");
            return;
        }

        if (val == null)
        {
            LogHelper.Error($"[{packet.ModName}]: deserialized value is null for property [{packet.PropertyName}]. Packet ignored.");
            return;
        }

        _isProcessingNetworkSync = true;
        try
        {
            if (prop.IsPerPlayer())
                ApplyPerPlayerUpdate(vm, prop, packet, val, sender);
            else
                ApplyHostOnlyUpdate(vm, prop, packet, val, sender);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"[{packet.ModName}]: exception while applying update for [{packet.PropertyName}]");
        }
        finally
        {
            _isProcessingNetworkSync = false;
        }
    }


    /// <summary>
    /// Applies an incoming per-player setting update.
    /// <para>
    /// Convention: a per-player property named <c>Foo</c> must have a companion array
    /// property named <c>FooData</c> (indexed 1–8 by player ID). The scalar <c>Foo</c>
    /// property is the local player's view into that array; all other indices are
    /// maintained for read-only display by other systems.
    /// </para>
    /// </summary>
    private static void ApplyPerPlayerUpdate(object vm, PropertyInfo prop, LobbyModSettingSyncPacket packet, object val, CSteamID? sender)
    {
        if (!sender.HasValue)
        {
            LogHelper.Warning($"Cannot verify origin of per-player property [{packet.PropertyName}] (sender unknown). Packet ignored.");
            return;
        }

        int senderPlayerId = GameNetworkAPI.GetPlayerIdForSteamId(sender.Value);
        if (senderPlayerId < 0)
        {
            if (!GameNetworkAPI.IsPlayerSlotMappingAvailable())
            {
                Instance.DeferPerPlayerUpdate(packet, sender.Value);
                return;
            }

            LogHelper.Warning($"Per-player property [{packet.PropertyName}] arrived from [{sender.Value}], which holds no player slot. Packet ignored.");
        }

        if (senderPlayerId != packet.SourcePlayerId)
        {
            LogHelper.Warning($"Per-player property [{packet.PropertyName}] claimed playerId={packet.SourcePlayerId} but was sent by [{sender.Value}] (playerId={senderPlayerId}). Packet ignored.");
            return;
        }

        string arrayPropName = packet.PropertyName + "Data";
        PropertyInfo? storageProp = vm.GetType().GetProperty(arrayPropName);

        if (storageProp == null)
        {
            LogHelper.Warning($"Companion array property [{arrayPropName}] not found on ViewModel [{vm.GetType().Name}]. Update dropped.");
            return;
        }

        if (storageProp.GetValue(vm) is not Array array)
        {
            LogHelper.Warning($"[{arrayPropName}] is not an Array. Update dropped.");
            return;
        }

        if (packet.SourcePlayerId < 0 || packet.SourcePlayerId >= array.Length)
        {
            LogHelper.Warning($"sourcePlayerId={packet.SourcePlayerId} is out of range for [{arrayPropName}] (length={array.Length}). Update dropped.");
            return;
        }

        array.SetValue(val, packet.SourcePlayerId);
        LogHelper.Debug($"[{arrayPropName}[{packet.SourcePlayerId}]] = {val}");

        if (vm is LobbyModSettingsBaseViewModel baseVm)
        {
            // Always refresh the backing array so any UI bound to the data updates
            baseVm.System_TriggerUpdate(arrayPropName);

            // Only refresh the scalar property on the machine that owns this slot,
            // so the local checkbox reflects the confirmed value without going stale.
            if (packet.SourcePlayerId == GameNetworkAPI.GetLocalPlayerId())
                baseVm.System_TriggerUpdate(packet.PropertyName);
        }
    }

    /// <summary>
    /// Holds a per-player update that cannot yet be attributed to a slot.
    /// Any earlier update for the same mod, property and sender is superseded, so only the latest value per sender survives.
    /// </summary>
    private void DeferPerPlayerUpdate(LobbyModSettingSyncPacket packet, CSteamID sender)
    {
        _deferredPerPlayerUpdates.RemoveAll(d =>
            d.Sender == sender &&
            d.Packet.ModName == packet.ModName &&
            d.Packet.PropertyName == packet.PropertyName);

        if (_deferredPerPlayerUpdates.Count >= MAX_DEFERRED_PER_PLAYER_UPDATES)
        {
            LogHelper.Warning($"Deferred per-player update queue is full ({MAX_DEFERRED_PER_PLAYER_UPDATES}). Dropping oldest entry. Please report this!");
            _deferredPerPlayerUpdates.RemoveAt(0);
        }

        _deferredPerPlayerUpdates.Add(
            new DeferredPerPlayerUpdate
            {
                Packet = packet,
                Sender = sender
            });
        LogHelper.Debug($"Deferred per-player property [{packet.PropertyName}] from [{sender}]: no slot mapping yet (queue={_deferredPerPlayerUpdates.Count})");
    }

    /// <summary>
    /// Re-applies any deferred per-player updates once an authoritative slot mapping exists.
    /// </summary>
    internal void FlushDeferredPerPlayerUpdates()
    {
        if (_isFlushingDeferredUpdates || _deferredPerPlayerUpdates.Count == 0)
            return;

        if (!GameNetworkAPI.IsPlayerSlotMappingAvailable())
            return;

        DeferredPerPlayerUpdate[] pending = [.. _deferredPerPlayerUpdates];
        _deferredPerPlayerUpdates.Clear();

        LogHelper.Debug($"Slot mapping available: replaying {pending.Length} deferred per-player update(s)");

        _isFlushingDeferredUpdates = true;
        try
        {
            foreach (DeferredPerPlayerUpdate deferred in pending)
            {
                ReceiveSettingsUpdate(deferred.Packet, deferred.Sender);
            }
        }
        finally
        {
            _isFlushingDeferredUpdates = false;
        }
    }

    /// <summary>
    /// Applies an incoming host-only setting update.
    /// Clients accept and apply the value; the host ignores it (it should never arrive).
    /// </summary>
    private static void ApplyHostOnlyUpdate(object vm, PropertyInfo prop, LobbyModSettingSyncPacket packet, object val, CSteamID? sender)
    {
        if (GameNetworkAPI.IsLocalHost())
        {
            LogHelper.Warning($"Host received its own host-only packet for [{packet.PropertyName}]: ignored");
            return;
        }

        // Authorise on transport identity, never on packet contents. SourcePlayerId lives inside
        // the payload and a modified client can set it to anything, so it is only ever logged.
        CSteamID? host = GameNetworkAPI.GetHostSteamId();

        if (!sender.HasValue || host == null)
        {
            LogHelper.Warning($"Cannot verify origin of host-only property [{packet.PropertyName}] (sender or lobby owner unknown). Packet ignored.");
            return;
        }

        if (sender.Value != host.Value)
        {
            LogHelper.Warning($"Host-only property [{packet.PropertyName}] arrived from [{sender.Value}], which is not the lobby owner [{host.Value}]. Packet ignored.");
            return;
        }

        // The value is verified host state, so open an authorised-write window before touching
        // the setter. Clients reject host-only writes by default, and applying an inbound update
        // goes through that same setter: without this scope the client would refuse the host's
        // own broadcast and revert its UI.
        LobbyModSettingsBaseViewModel? baseVm = vm as LobbyModSettingsBaseViewModel;

        baseVm?.BeginAuthorisedUpdate();
        try
        {
            prop.SetValue(vm, val);
        }
        finally
        {
            baseVm?.EndAuthorisedUpdate();
        }

        LogHelper.Debug($"[{packet.PropertyName}] = {val}");

        baseVm?.System_TriggerUpdate(packet.PropertyName);
    }

    // -------------------------------------------------------------------------
    // Initial state sync for joining players
    // -------------------------------------------------------------------------

    /// <summary>
    /// Pushes the current state of all attributed mod-settings properties to a player
    /// who has just joined the lobby. Only runs on the host.
    /// <para>
    /// Both <see cref="SyncPerPlayerAttribute"/> and <see cref="SyncHostOnlyAttribute"/>
    /// properties are sent. Everything else is skipped, including local-only settings.
    /// </para>
    /// </summary>
    /// <param name="member">The lobby member who just joined.</param>
    public void SyncSettingsToNewPlayer(Platform_Multiplayer.MPLobbyMember member)
    {
        if (!GameNetworkAPI.IsLocalHost())
        {
            LogHelper.Debug("Hot host, skipping");
            return;
        }

        LogHelper.Information($"Syncing all mod settings to [{member.Name}]");

        foreach (LobbyModSettingsEntry mod in _registeredModSettings)
        {
            PropertyInfo[] props = mod.ViewModel.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (PropertyInfo prop in props)
            {
                if (!prop.CanRead)
                    continue;

                if (!prop.IsSynced())
                {
                    LogHelper.Debug($"[{mod.Name}]: Skipping unsynchronised property [{prop.Name}]");
                    continue;
                }

                try
                {
                    object? val = prop.GetValue(mod.ViewModel);
                    if (val == null)
                    {
                        LogHelper.Warning($"[{mod.Name}]: Property [{prop.Name}] returned null. Skipped");
                        continue;
                    }

                    LobbyModSettingSyncPacket packet = new LobbyModSettingSyncPacket
                    {
                        ModName = mod.Name,
                        PropertyName = prop.Name,
                        SerializedValue = MessagePackSerializer.Serialize(val),
                        TypeName = val.GetType().AssemblyQualifiedName,
                        SourcePlayerId = GameNetworkAPI.GetLocalPlayerId()
                    };

                    byte[] bytes = MessagePackSerializer.Serialize(packet);
                    Platform_Multiplayer.MPData mpData = new Platform_Multiplayer.MPData
                    {
                        packetType = _syncPacketId,
                        data = bytes,
                        dataLength = bytes.Length
                    };

                    GameNetworkAPI.SendPacketToSteamId(member.id, mpData);
                    LogHelper.Debug($"[{mod.Name}]: Sent [{prop.Name}] to [{member.Name}]");
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, $"[{mod.Name}]: Failed to sync property [{prop.Name}] to [{member.Name}]");
                }
            }
        }
    }

    private void OnTextFileAssetProcess(string relativePath, ref string text)
    {
        // Only patch XAML files
        if (relativePath.EndsWith(".xaml", StringComparison.InvariantCultureIgnoreCase))
        {
            LogHelper.Debug($"Processing: [{relativePath}]");
            ApplyPatchForPath(relativePath, ref text);
        }
    }

    private List<XDocument> GetPatchesForPath(string relativePath)
    {
        List<XDocument> patches = [];

        foreach (IndexedModResource patchResource in GameAssetManagerAPI.Instance.GetPatchesForPath(relativePath))
        {
            NameTable nt = new();
            XmlNamespaceManager nsManager = XamlPatcher.CreateNamespaceManager(nt);
            XmlParserContext parserContext = new XmlParserContext(null, nsManager, null, XmlSpace.Default);

            try
            {
                LogHelper.Information($"Found patch for [{relativePath}] in mod [{patchResource.ModGuid}]");
                using Stream stream = patchResource.OpenRead();
                using XmlReader reader = XmlReader.Create(stream, new XmlReaderSettings(), parserContext);
                patches.Add(XDocument.Load(reader));
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Failed to load patch resource: {patchResource.Path}");
            }
        }
        return patches;
    }

    private void ApplyPatchForPath(string relativePath, ref string text)
    {
        List<XDocument> patches = GetPatchesForPath(relativePath);
        if (patches.Count == 0)
            return;

        LogHelper.Information($"Applying {patches.Count} patch files to {relativePath}");

        // Build operations list from all patch files
        List<XmlPatchOperation> allOperations = new List<XmlPatchOperation>();

        foreach (XDocument patchDoc in patches)
        {
            XElement root = patchDoc.Root;
            foreach (XElement opNode in root.Elements("Operation"))
            {
                XmlPatchOperation op = new XmlPatchOperation();

                if (Enum.TryParse(opNode.Attribute("Type")?.Value, out XmlPatchType type))
                    op.Type = type;
                else
                    continue;

                op.XPath = opNode.Attribute("XPath")?.Value;
                op.AttributeName = opNode.Attribute("AttributeName")?.Value;
                op.Value = opNode.Attribute("Value")?.Value;

                //XElement contentNode = opNode.Element("Content");
                //if (contentNode != null && contentNode.FirstNode != null)
                //{
                //    op.Content = contentNode.FirstNode.ToString();
                //}
                XElement contentNode = opNode.Element("Content");
                if (contentNode != null)
                {
                    XElement realContent = contentNode.Elements().FirstOrDefault();

                    if (realContent != null)
                    {
                        op.Content = realContent.ToString();
                    }
                    else
                    {
                        if (!string.IsNullOrWhiteSpace(contentNode.Value))
                        {
                            op.Content = contentNode.Value.Trim();
                        }
                    }
                }
                allOperations.Add(op);
            }
        }

        // Apply to the string reference
        if (allOperations.Count > 0)
        {
            text = XamlPatcher.ApplyPatches(text, allOperations);

            // Debug dump
            //File.WriteAllText("last_patched_debug.xml", text);
        }
    }

    /// <summary>
    /// "Set and Forget" API.
    /// Registers a ViewModel to be automatically attached to ANY element with this name,
    /// whenever it is loaded by Noesis.
    /// </summary>
    public void RegisterBinding(string elementName, object viewModel)
    {
        LogHelper.Information($"Registering auto-binding: [{elementName}]");

        if (_bindingRegistry.ContainsKey(elementName))
            _bindingRegistry[elementName] = viewModel;
        else
            _bindingRegistry.Add(elementName, viewModel);

        // Optional: Scan existing views immediately in case we registered late
        ScanAllActiveViews();
    }

    /// <summary>
    /// Called by our Hooks whenever a UI component finishes loading.
    /// </summary>
    internal void InjectBindings(object rootElement)
    {
        if (rootElement is not Noesis.FrameworkElement rootFe)
            return;

        // Iterate over all registered bindings
        foreach (KeyValuePair<string, object> kvp in _bindingRegistry)
        {
            string targetName = kvp.Key;
            object viewModel = kvp.Value;

            // Use FindName
            Noesis.FrameworkElement? found = rootFe.FindName(targetName) as Noesis.FrameworkElement;

            // Fallback to Visual Tree Search
            if (found == null)
            {
                found = FindElementByName(rootFe, targetName);
            }

            if (found != null)
            {
                // Only bind if not already bound
                if (found.DataContext != viewModel)
                {
                    LogHelper.Information($"ViewModel Linked [{targetName}] in [{rootElement.GetType().Name}]");
                    ApplyBinding(found, viewModel);

                    // Force it to stay set if the game tries to overwrite it
                    found.DataContextChanged += (s, e) =>
                    {
                        if (e.NewValue != viewModel)
                        {
                            LogHelper.Warning($"Detected DataContext overwrite on [{targetName}]. Re-applying mod ViewModel.");
                            ApplyBinding(found, viewModel);
                        }
                    };

                    // found.InvalidateProperty(FrameworkElement.DataContextProperty);
                }

                // Some controls expose lifecycle events rather than bindable commands.
                // So we notify interested ViewModels after DataContext is in place so they can attach once and safely detach if Noesis recreates the view.
                if (viewModel is INoesisElementBindingAware bindingAware)
                {
                    try
                    {
                        bindingAware.OnNoesisElementBound(found);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.Error(ex, $"Failed to notify ViewModel for [{targetName}]");
                    }
                }
            }
            else
            {
                LogHelper.Verbose($"Linking: [{targetName}] not found in [{rootElement.GetType().Name}]");
            }
        }
    }

    private void ApplyBinding(Noesis.FrameworkElement element, object vm)
    {
        if (element.DataContext != vm)
        {
            LogHelper.Information($"ViewModel Linked [{element.Name}]");
            element.DataContext = vm;
        }
    }

    private void ScanAllActiveViews()
    {
        NoesisView[] views = UnityEngine.Object.FindObjectsOfType<NoesisView>();
        foreach (NoesisView v in views)
            InjectBindings(v.Content);
    }

    // ---------------------------------------------------------------------------------------
    // UI HELPER METHODS FOR MODS USING NOESIS
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Searches all active Noesis Views for an element with the specific name.
    /// </summary>
    public Noesis.FrameworkElement? FindGlobalElement(string name)
    {
        NoesisView[] views = UnityEngine.Object.FindObjectsOfType<NoesisView>();
        foreach (NoesisView view in views)
        {
            if (view == null || view.Content == null)
                continue;

            Noesis.FrameworkElement? found = FindElementByName(view.Content, name);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Recursively searches the Noesis visual tree for a FrameworkElement with a specific name.
    /// Safe to call even if the tree is deep.
    /// </summary>
    /// <param name="root">The root object to start searching from.</param>
    /// <param name="name">The x:Name of the element to find.</param>
    /// <param name="depth">Internal depth counter to prevent stack overflows.</param>
    /// <returns>The found element, or null.</returns>
    public Noesis.FrameworkElement? FindElementByName(Noesis.DependencyObject root, string name, int depth = 0)
    {
        if (root == null || depth > 50)
            return null;

        int count = Noesis.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            Noesis.DependencyObject child = Noesis.VisualTreeHelper.GetChild(root, i);

            if (child is Noesis.FrameworkElement fe && fe.Name == name)
            {
                return fe;
            }

            Noesis.FrameworkElement? found = FindElementByName(child, name, depth + 1);
            if (found != null)
                return found;
        }
        return null;
    }

    /// <summary>
    /// Scans all active NoesisViews in the scene, looks for an injected element by name,
    /// and if found, assigns the provided ViewModel to its DataContext.
    /// </summary>
    /// <param name="elementName">The x:Name defined in the XAML patch.</param>
    /// <param name="viewModel">The C# object to bind to the element.</param>
    /// <returns>The found FrameworkElement if successful, otherwise null.</returns>
    public Noesis.FrameworkElement? TryHookElementDataContext(string elementName, object viewModel)
    {
        NoesisView[] views = UnityEngine.Object.FindObjectsOfType<NoesisView>();

        foreach (NoesisView view in views)
        {
            if (view == null || view.Content == null)
                continue;

            try
            {
                Noesis.FrameworkElement? foundElement = FindElementByName(view.Content, elementName);

                if (foundElement != null)
                {
                    LogHelper.Information($"Found [{elementName}] in view [{view.gameObject.name}]. Hooking DataContext.");
                    foundElement.DataContext = viewModel;
                    return foundElement;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error searching view [{view.name}]");
            }
        }

        return null;
    }

    /// <summary>
    /// Use this to dump a visual tree of any noesis parent.
    /// Can get spammy, use with caution.
    /// </summary>
    /// <param name="parent">The parent to dump</param>
    /// <param name="indent"></param>
    public static void DebugDumpVisualTree(Noesis.DependencyObject parent, int indent = 0)
    {
        if (parent == null)
            return;

        int count = Noesis.VisualTreeHelper.GetChildrenCount(parent);
        string prefix = new string('-', indent);

        string type = parent.GetType().Name;
        string name = (parent as Noesis.FrameworkElement)?.Name ?? "Unnamed";
        LogHelper.Information($"{prefix} {type} : {name}");

        for (int i = 0; i < count; i++)
        {
            Noesis.DependencyObject child = Noesis.VisualTreeHelper.GetChild(parent, i);
            DebugDumpVisualTree(child, indent + 1);
        }
    }

    /// <summary>
    /// Clears the binding registry.
    /// </summary>
    internal void Unload()
    {
        _bindingRegistry.Clear();
    }
}
