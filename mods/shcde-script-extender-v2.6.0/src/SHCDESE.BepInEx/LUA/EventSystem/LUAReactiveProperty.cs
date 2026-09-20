using NLua;
using R3;
using Serilog;
using System;
namespace SHCDESE.Lua.EventSystem;

/// <summary>
/// A generic wrapper that exposes a C# ReactiveProperty(T) to the Lua environment.
/// </summary>
public class LUAReactiveProperty<T> : ILUAReactiveProperty
{
    private readonly ReactiveProperty<T> _reactiveProperty;

    internal LUAReactiveProperty(ReactiveProperty<T> reactiveProperty)
    {
        _reactiveProperty = reactiveProperty;
    }

    /// <summary>
    /// Gets the current value of the property.
    /// </summary>
    public object GetValue()
    {
        return _reactiveProperty.Value;
    }

    /// <summary>
    /// Subscribes a Lua function to the property's value changes.
    /// </summary>
    public LUAR3Subscription Subscribe(LuaFunction function)
    {
        if (function == null)
        {
            Log.Warning($"LUAReactiveProperty<{typeof(T).Name}> - Subscribe: LuaFunction is null!");
            return null;
        }

        // Subscribe to the C# R3 stream. The action calls the Lua function, passing the new value.
        IDisposable disposable = _reactiveProperty.Subscribe(newValue =>
        {
            try
            {
                // The new value is passed directly as an argument to the Lua function.
                function.Call(newValue);
            }
            catch (Exception ex)
            {
                Log.Error($"LUAReactiveProperty<{typeof(T).Name}> - Error executing Lua subscription callback: {ex.Message}");
            }
        });

        return new LUAR3Subscription(disposable);
    }
}