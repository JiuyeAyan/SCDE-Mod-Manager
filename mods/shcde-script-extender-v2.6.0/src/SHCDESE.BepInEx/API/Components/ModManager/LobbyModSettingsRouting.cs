using SHCDESE.API.Components.Network;
using System.Reflection;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Decides how a lobby mod settings property is routed.
/// <para>
/// Synchronisation and persistence are independent axes:
/// <list type="bullet">
///   <item><see cref="SyncHostOnlyAttribute"/>: host to clients; stores the locally owned host value.</item>
///   <item><see cref="SyncPerPlayerAttribute"/>: each player's own value; stores the local player's.</item>
///   <item><see cref="PersistLocalAttribute"/>: never sent; stores the local value.</item>
///   <item><see cref="DoNotPersistAttribute"/>: cancels storage, leaving synchronisation as declared.</item>
///   <item>No attribute: neither sent nor stored, i.e. presentation state.</item>
/// </list>
/// </para>
/// </summary>
internal static class LobbyModSettingsRouting
{
    /// <summary>The host owns this value and broadcasts it to every client.</summary>
    public static bool IsHostOnly(this PropertyInfo prop) => prop.GetCustomAttribute<SyncHostOnlyAttribute>() != null;

    /// <summary>Every player owns their own copy of this value.</summary>
    public static bool IsPerPlayer(this PropertyInfo prop) => prop.GetCustomAttribute<SyncPerPlayerAttribute>() != null;

    /// <summary>The value takes part in the network protocol.</summary>
    public static bool IsSynced(this PropertyInfo prop) => prop.IsHostOnly() || prop.IsPerPlayer();

    /// <summary>The value belongs in this player's settings file.</summary>
    public static bool IsPersisted(this PropertyInfo prop) =>
        (prop.IsSynced() || prop.GetCustomAttribute<PersistLocalAttribute>() != null)
        && prop.GetCustomAttribute<DoNotPersistAttribute>() == null;

    /// <summary>The value is neither sent nor stored, so the settings system ignores it entirely.</summary>
    public static bool IsUIOnly(this PropertyInfo prop) => !prop.IsSynced() && !prop.IsPersisted();
}