using System;

namespace SHCDESE.Lua.EventSystem;

/// <summary>
/// A simple wrapper around an IDisposable subscription that can be safely
/// passed to and used by Lua code to unsubscribe from a hook.
/// </summary>
public class LUAR3Subscription
{
    private IDisposable? _subscription;

    internal LUAR3Subscription(IDisposable subscription)
    {
        _subscription = subscription;
    }

    /// <summary>
    /// Disposes of the subscription, unsubscribing the Lua function.
    /// This method will be callable from Lua.
    /// </summary>
    public void Unsubscribe()
    {
        _subscription?.Dispose();
        _subscription = null;
    }
}
