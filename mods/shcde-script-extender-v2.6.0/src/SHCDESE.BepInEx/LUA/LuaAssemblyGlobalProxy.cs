using Microsoft.Extensions.Logging;
using SHCDESE.Logging;
using System;
using System.Reflection;
using System.Runtime.ExceptionServices;

namespace SHCDESE.LUA;

/// <summary>
/// A type-erased Lua-facing proxy for a <see cref="ManagedAssemblyImmediate{T}"/> or
/// <see cref="ManagedAssemblyDisplacement{Task}"/>.
///
/// NLua passes all Lua numbers as <c>double</c>, which means calling
/// <c>gSomeCap:Push(4)</c> from Lua would fail with "Invalid arguments" when the
/// underlying generic method expects a <c>byte</c>, <c>int</c>, etc.
///
/// This proxy exposes only plain <c>double</c> parameters to NLua, converts them to
/// the correct underlying type at runtime via <see cref="Convert"/>, and forwards
/// the call to the real managed wrapper.
/// </summary>
public sealed class LuaAssemblyGlobalProxy
{
    private readonly object _inner;         // the ManagedAssemblyImmediate<T> or Displacement<T>
    private readonly Type _valueType;       // the T in IAssemblyGetSet<T>
    private readonly ILogger? _logger;

    // Cached reflected methods: resolved once per proxy instance
    private readonly System.Reflection.MethodInfo _getValue;
    private readonly System.Reflection.MethodInfo _setValue;
    private readonly System.Reflection.MethodInfo _push;
    private readonly System.Reflection.MethodInfo _pop;
    private readonly System.Reflection.MethodInfo _restoreDefaults;

    public LuaAssemblyGlobalProxy(object managedAssemblyItem, Type valueType, ILogger? logger = null)
    {
        _inner = managedAssemblyItem ?? throw new ArgumentNullException(nameof(managedAssemblyItem));
        _valueType = valueType ?? throw new ArgumentNullException(nameof(valueType));
        _logger = logger;

        Type itemType = _inner.GetType();
        _getValue = itemType.GetMethod("GetValue") ?? throw new InvalidOperationException($"GetValue not found on {itemType}");
        _setValue = itemType.GetMethod("SetValue") ?? throw new InvalidOperationException($"SetValue not found on {itemType}");
        _push = itemType.GetMethod("Push") ?? throw new InvalidOperationException($"Push not found on {itemType}");
        _pop = itemType.GetMethod("Pop") ?? throw new InvalidOperationException($"Pop not found on {itemType}");
        _restoreDefaults = itemType.GetMethod("RestoreOriginal") ?? throw new InvalidOperationException($"RestoreOriginal not found on {itemType}");
    }

    private object? Invoke(MethodInfo method, object?[]? args)
    {
        try
        {
            return method.Invoke(_inner, args);
        }
        catch (TargetInvocationException ex) when (ex.InnerException != null)
        {
            LogHelper.Error(ex.InnerException, $"{method.Name} failed for {_inner.GetType().FullName}");

            ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            throw;
        }
    }

    /// <summary>
    /// Returns the current value as a <c>double</c> so Lua can use it in arithmetic directly.
    /// </summary>
    public double GetValue()
    {
        object? result = Invoke(_getValue, null);
        return result == null ? 0.0 : Convert.ToDouble(result);
    }

    /// <summary>
    /// Sets the value. NLua always passes numbers as <c>double</c>; we convert to the real type here.
    /// </summary>
    public void SetValue(double value)
    {
        object converted = ConvertToValueType(value);
        Invoke(_setValue, new[] { converted });
    }

    /// <summary>
    /// Pushes the current value onto the undo stack and sets the new value.
    /// </summary>
    public void Push(double value)
    {
        object converted = ConvertToValueType(value);
        Invoke(_push, new[] { converted });
    }

    /// <summary>
    /// Pops the most recently pushed value and restores it.
    /// </summary>
    public double Pop()
    {
        object? result = Invoke(_pop, null);
        return result == null ? 0.0 : Convert.ToDouble(result);
    }

    /// <summary>
    /// Restores the original value that was read from memory at scan time.
    /// </summary>
    public void RestoreDefaults()
    {
        Invoke(_restoreDefaults, null);
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    private object ConvertToValueType(double value)
    {
        try
        {
            return Convert.ChangeType(value, _valueType);
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, $"LuaAssemblyGlobalProxy: Could not convert [{value}] to [{_valueType.Name}], falling back to default.");
            return Activator.CreateInstance(_valueType)!;
        }
    }
}
