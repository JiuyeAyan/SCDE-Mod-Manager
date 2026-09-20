using SHCDESE.EventAPI.Lua;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to various lua-related events in the game.
/// </summary>
public static class LuaR3EventHooks
{
    /// <summary>
    /// Fired a lua state is create anew.
    /// </summary>
    [LuaApiExport("OnCreateState")]
    public static readonly R3EventHook<CreateStateEventArgs> OnCreateState = new();
}
