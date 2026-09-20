using MonoMod.RuntimeDetour;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.Logging;
using System.Collections.Generic;
namespace SHCDESE.Lua.EventSystem;

/// <summary>
/// Manages hooks that are exposed to the Lua environment.
/// </summary>
public class LuaHookManager
{
    private readonly Dictionary<string, ILUAR3Hook> _luaHooks = new Dictionary<string, ILUAR3Hook>();
    private readonly Dictionary<string, ILUAReactiveProperty> _luaProperties = new Dictionary<string, ILUAReactiveProperty>();

    /// <summary>
    /// Registers a C# Hook{T} and makes it available to Lua under a specific name.
    /// </summary>
    public void RegisterHook<T>(string luaName, R3EventHook<T> csharpHook) where T : EventHookBase
    {
        if (!_luaHooks.ContainsKey(luaName))
        {
            _luaHooks[luaName] = new LUAR3Hook<T>(csharpHook);
        }
    }

    /// <summary>
    /// Registers a C# ReactiveProperty{T} and makes it available to Lua under a specific name.
    /// </summary>
    public void RegisterProperty<T>(string luaName, ReactiveProperty<T> reactiveProperty)
    {
        if (!_luaProperties.ContainsKey(luaName))
        {
            _luaProperties[luaName] = new LUAReactiveProperty<T>(reactiveProperty);
        }
    }

    /// <summary>
    /// This is the method that will be called FROM Lua to get a hook object.
    /// </summary>
    public ILUAR3Hook? GetHook(string name)
    {
        _luaHooks.TryGetValue(name, out ILUAR3Hook? hook);

        if (hook == null)
        {
            LogHelper.Warning($"Hook by name [{name}] not found!");
        }

        return hook;
    }

    /// <summary>
    /// Gets a reactive property object that can be subscribed to from Lua.
    /// </summary>
    public ILUAReactiveProperty? GetProperty(string name)
    {
        _luaProperties.TryGetValue(name, out ILUAReactiveProperty? prop);

        if (prop == null)
        {
            LogHelper.Warning($"Property by name [{name}] not found!");
        }

        return prop;
    }
}
