using NLua;
using System;

namespace SHCDESE.Lua.EventSystem;

/// <summary>
/// A non-generic interface to allow storing different LUAReactiveProperty{T} types in a dictionary.
/// </summary>
public interface ILUAReactiveProperty
{
    /// <summary>
    /// Subscribes a Lua function to the reactive property's value changes.
    /// </summary>
    /// <param name="function">The Lua function to be called with the new value.</param>
    /// <returns>A subscription object with an Unsubscribe method.</returns>
    LUAR3Subscription Subscribe(LuaFunction function);

    /// <summary>
    /// Gets the current value of the property.
    /// </summary>
    object GetValue();
}
