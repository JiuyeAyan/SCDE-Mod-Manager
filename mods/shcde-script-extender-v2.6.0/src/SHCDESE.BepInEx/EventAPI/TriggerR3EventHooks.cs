using SHCDESE.EventAPI.Trigger;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to events related to triggers.
/// </summary>
public static class TriggerR3EventHooks
{
    /// <summary>
    /// Fired when a unit enters an area.
    /// </summary>
    [LuaApiExport("OnAreaEntered")]
    public static readonly R3EventHook<TriggerEventArgs> OnAreaEntered = new();

    /// <summary>
    /// Fired when a unit leaves an area.
    /// </summary>
    [LuaApiExport("OnAreaExited")]
    public static readonly R3EventHook<TriggerEventArgs> OnAreaExited = new();
}
