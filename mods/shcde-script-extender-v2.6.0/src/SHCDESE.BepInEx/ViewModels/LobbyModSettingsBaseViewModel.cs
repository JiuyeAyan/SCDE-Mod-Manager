using RedBird.X64.Assembly.Stateful;
using SHCDESE.API;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.Logging;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace SHCDESE.ViewModels;

public abstract class LobbyModSettingsBaseViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Cache of (ViewModel type, property name) -> "is decorated with [SyncHostOnly]".
    /// Attribute lookups happen on every setter call, so the reflection result is memoised.
    /// </summary>
    private static readonly ConcurrentDictionary<(Type, string), bool> _hostOnlyCache = new();

    /// <summary>
    /// <c>true</c> while this ViewModel is raising a notification purely to snap bound UI
    /// back to the authoritative value after a rejected edit.
    /// <para>
    /// <see cref="GameXAMLManagerAPI"/> checks this before broadcasting or persisting, so a
    /// revert is never mistaken for a real user edit.
    /// </para>
    /// </summary>
    internal bool IsSuppressingSync { get; private set; }

    /// <summary>
    /// <c>true</c> while an already-authorised update is being written into this ViewModel by
    /// the sync system, i.e. a value that arrived from the host and was verified against the
    /// lobby owner before being applied.
    /// <para>
    /// <see cref="CanEdit"/> permits writes inside this window. Without it, an incoming host
    /// broadcast would be rejected on every client, because applying it goes through the very
    /// same property setter a client is normally forbidden from using.
    /// </para>
    /// </summary>
    internal bool IsApplyingAuthorisedUpdate { get; private set; }

    /// <summary>
    /// Opens an authorised-write window on this ViewModel. Call only after the update's origin
    /// has been verified, and always pair with <see cref="EndAuthorisedUpdate"/> in a
    /// <c>finally</c> block.
    /// </summary>
    internal void BeginAuthorisedUpdate() => IsApplyingAuthorisedUpdate = true;

    /// <summary>Closes the window opened by <see cref="BeginAuthorisedUpdate"/>.</summary>
    internal void EndAuthorisedUpdate() => IsApplyingAuthorisedUpdate = false;

    protected void OnPropertyChanged(string name)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }

    public void System_TriggerUpdate(string name) => OnPropertyChanged(name);

    /// <summary>
    /// <c>true</c> when the local player owns host-only settings (host in multiplayer,
    /// or always in singleplayer). Bind <c>IsEnabled</c> of host-only controls to this.
    /// </summary>
    /// <remarks>
    /// This is a computed property: it will NOT update on its own when lobby state changes.
    /// Call <see cref="System_RefreshHostState"/> whenever host status may have changed.
    /// </remarks>
    public bool IsHost => GameNetworkAPI.IsLocalHost();

    /// <summary>Forces any UI bound to <see cref="IsHost"/> to re-evaluate.</summary>
    public void System_RefreshHostState() => OnPropertyChanged(nameof(IsHost));

    // -------------------------------------------------------------------------
    // Write authorisation
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns <c>true</c> if the named property on this ViewModel carries
    /// <see cref="SyncHostOnlyAttribute"/>.
    /// </summary>
    protected bool IsHostOnlyProperty(string propertyName)
    {
        if (string.IsNullOrEmpty(propertyName))
            return false;

        return _hostOnlyCache.GetOrAdd((GetType(), propertyName), key =>
        {
            PropertyInfo prop = key.Item1.GetProperty(key.Item2);
            return prop != null && prop.IsHostOnly();
        });
    }

    /// <summary>
    /// Gate that every synced property setter must call BEFORE mutating its backing store.
    /// <para>
    /// Returns <c>false</c> when the local player is a client and the property is marked
    /// <see cref="SyncHostOnlyAttribute"/>. In that case the pending edit must be discarded,
    /// and a revert notification is raised so two-way-bound controls (checkboxes, text boxes)
    /// snap back to the host's authoritative value instead of showing a phantom local change.
    /// </para>
    /// </summary>
    /// <param name="propertyName">
    /// Filled in automatically from the calling property. Only pass explicitly when calling
    /// from a helper rather than directly from the setter.
    /// </param>
    protected bool CanEdit([CallerMemberName] string propertyName = null)
    {
        if (string.IsNullOrEmpty(propertyName))
            return true;

        // The sync system is writing a value it has already verified came from the host.
        // This is the authoritative path, not a local user edit, so it is always allowed.
        if (IsApplyingAuthorisedUpdate)
            return true;

        // Not host-restricted: anyone may edit.
        if (!IsHostOnlyProperty(propertyName))
            return true;

        // Singleplayer / main menu: IsLocalHost() is true anyway, but short-circuit
        // so we never touch networking state we don't need.
        if (!GameNetworkAPI.IsNetworkedEnvironment())
            return true;

        if (GameNetworkAPI.IsLocalHost())
            return true;

        LogHelper.Warning($"[{GetType().Name}] rejected client edit of host-only property [{propertyName}]; reverting UI");
        NotifyRevert(propertyName);
        return false;
    }

    /// <summary>
    /// Raises <see cref="PropertyChanged"/> with <see cref="IsSuppressingSync"/> set, so bound
    /// controls re-read the unchanged getter while the sync pipeline ignores the notification.
    /// </summary>
    private void NotifyRevert(string propertyName)
    {
        IsSuppressingSync = true;
        try
        {
            OnPropertyChanged(propertyName);
        }
        finally
        {
            IsSuppressingSync = false;
        }
    }

    /// <summary>
    /// Convenience setter for synced properties with a simple backing field.
    /// Applies the <see cref="CanEdit"/> gate, skips no-op writes, assigns, and notifies.
    /// </summary>
    /// <returns><c>true</c> if the value was actually changed.</returns>
    protected bool SetSynced<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (!CanEdit(propertyName))
            return false;

        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Formats an integer for a string property bound to a XAML text control.
    /// </summary>
    /// <remarks>
    /// The invariant representation is safe to persist and synchronize between players using
    /// different operating-system cultures. Pair this with <c>SetHostInteger</c>.
    /// </remarks>
    protected static string FormatInteger(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Formats a nullable unsigned 16-bit integer for a string property bound to a XAML text
    /// control. An unavailable value is represented as <c>0</c>.
    /// </summary>
    protected static string FormatInteger(ushort? value) => (value ?? 0).ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// Convenience setter for a host-owned integer exposed to XAML as a string.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The calling property should be decorated with <see cref="SyncHostOnlyAttribute"/>. 
    /// This method applies the normal host-authorisation gate, parses invariant integer text, skips no-op writes, assigns the backing field, and raises <see cref="PropertyChanged"/>.
    /// </para>
    /// <para>
    /// Invalid text is rejected and the bound control is refreshed from its unchanged getter.
    /// The refresh is marked as a UI revert, so it is neither synchronized nor persisted.
    /// </para>
    /// <code>
    /// private int _limit = 5;
    ///
    /// [SyncHostOnly]
    /// public string Limit
    /// {
    ///     get =&gt; FormatInteger(_limit);
    ///     set =&gt; SetHostInteger(ref _limit, value);
    /// }
    /// </code>
    /// </remarks>
    /// <returns><c>true</c> if the parsed value was actually changed.</returns>
    protected bool SetHostInteger(ref int field, string value, [CallerMemberName] string propertyName = null) => SetHostInteger(ref field, value, int.MinValue, int.MaxValue, propertyName);

    /// <summary>
    /// Convenience setter for a range-constrained host-owned integer exposed to XAML as a string.
    /// </summary>
    /// <remarks>
    /// The range is inclusive. 
    /// Invalid or out-of-range text is rejected and the bound control is refreshed from its unchanged getter without synchronizing or persisting the rejected value.
    /// </remarks>
    /// <param name="field">The integer backing field.</param>
    /// <param name="value">Text supplied by the XAML binding.</param>
    /// <param name="minimumValue">The smallest accepted value, inclusive.</param>
    /// <param name="maximumValue">The largest accepted value, inclusive.</param>
    /// <param name="propertyName">
    /// Filled in automatically from the calling property. Pass explicitly when calling from another helper.
    /// </param>
    /// <returns><c>true</c> if the parsed value was actually changed.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="minimumValue"/> is greater than <paramref name="maximumValue"/>.
    /// </exception>
    protected bool SetHostInteger(ref int field, string value, int minimumValue, int maximumValue, [CallerMemberName] string propertyName = null)
    {
        if (minimumValue > maximumValue)
            throw new ArgumentOutOfRangeException(nameof(minimumValue), minimumValue, "Minimum value cannot exceed maximum value.");

        if (!CanEdit(propertyName))
            return false;

        if (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
            || parsed < minimumValue
            || parsed > maximumValue)
        {
            LogHelper.Debug($"[{GetType().Name}] rejected invalid integer [{value}] for [{propertyName}]; expected {minimumValue}..{maximumValue}");
            NotifyRevert(propertyName);
            return false;
        }

        if (field == parsed)
        {
            // Normalize equivalent input such as whitespace, a leading plus, or leading zeroes.
            if (!string.Equals(value, FormatInteger(field), StringComparison.Ordinal))
                NotifyRevert(propertyName);

            return false;
        }

        field = parsed;
        OnPropertyChanged(propertyName);
        return true;
    }

    /// <summary>
    /// Convenience setter for a host-owned unsigned 16-bit assembly immediate exposed to XAML as a string.
    /// </summary>
    /// <remarks>
    /// The calling property should be decorated with <see cref="SyncHostOnlyAttribute"/>. 
    /// Missing patches and invalid text are rejected by refreshing the bound control from its unchanged getter; the refresh is neither synchronized nor persisted.
    /// </remarks>
    /// <param name="patch">The assembly immediate to read and update.</param>
    /// <param name="value">Text supplied by the XAML binding.</param>
    /// <param name="propertyName">Filled in automatically from the calling property.</param>
    /// <returns><c>true</c> if the immediate value was actually changed.</returns>
    protected bool SetHostManagedImmediate<T>(ManagedAssemblyImmediate<T>? patch, string value, [CallerMemberName] string propertyName = null) where T : unmanaged
    {
        Type type = typeof(T);
        bool supported =
            type == typeof(sbyte) ||
            type == typeof(byte) ||
            type == typeof(short) ||
            type == typeof(ushort) ||
            type == typeof(int) ||
            type == typeof(uint) ||
            type == typeof(long) ||
            type == typeof(ulong);

        if (!supported)
        {
            throw new NotSupportedException($"Managed immediate type [{type.FullName}] must be an 8-, 16-, 32-, or 64-bit integral type.");
        }

        if (!CanEdit(propertyName))
            return false;

        if (patch == null)
        {
            NotifyRevert(propertyName);
            return false;
        }

        T parsed;
        try
        {
            parsed = (T)Convert.ChangeType(value, type, CultureInfo.InvariantCulture);
        }
        catch (Exception ex) when (ex is FormatException || ex is InvalidCastException || ex is OverflowException)
        {
            NotifyRevert(propertyName);
            return false;
        }

        T current = patch.GetValue();
        if (EqualityComparer<T>.Default.Equals(current, parsed))
        {
            string normalized = ((IFormattable)(object)current).ToString(null, CultureInfo.InvariantCulture);

            if (!string.Equals(value, normalized, StringComparison.Ordinal))
                NotifyRevert(propertyName);

            return false;
        }

        try
        {
            patch.SetValue(parsed);
        }
        catch (Exception ex) when (ex is ArgumentException || ex is OverflowException)
        {
            NotifyRevert(propertyName);
            return false;
        }

        OnPropertyChanged(propertyName);
        return true;
    }
}