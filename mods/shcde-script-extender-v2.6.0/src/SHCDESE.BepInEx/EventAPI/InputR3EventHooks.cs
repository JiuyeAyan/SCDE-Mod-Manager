using SHCDESE.EventAPI.Input;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to events related to UnityEngine Legacy Input.
/// </summary>
public static class InputR3EventHooks
{
    /// <summary>
    /// Fired when a key or mouse button is pressed down this frame.
    /// </summary>
    [LuaApiExport("OnKeyDown")]
    public static readonly R3EventHook<UnityInputEventArgs> OnKeyDown = new();

    /// <summary>
    /// Fired when a key or mouse button is released this frame.
    /// </summary>
    [LuaApiExport("OnKeyUp")]
    public static readonly R3EventHook<UnityInputEventArgs> OnKeyUp = new();

    /// <summary>
    /// Fired while a key or mouse button is being held down.
    /// </summary>
    [LuaApiExport("OnKey")]
    public static readonly R3EventHook<UnityInputEventArgs> OnKey = new();
}
