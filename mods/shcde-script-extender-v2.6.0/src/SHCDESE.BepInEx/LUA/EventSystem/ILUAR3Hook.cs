using NLua;

namespace SHCDESE.Lua.EventSystem;

public interface ILUAR3Hook
{
    /// <summary>
    /// Subscribes a Lua function to the hook.
    /// </summary>
    /// <param name="function">The Lua function to be called.</param>
    /// <returns>A subscription object with an Unsubscribe method.</returns>
    LUAR3Subscription Subscribe(LuaFunction function);
}
