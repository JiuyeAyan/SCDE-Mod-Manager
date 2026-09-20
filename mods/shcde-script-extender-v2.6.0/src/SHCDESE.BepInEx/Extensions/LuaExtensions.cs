using NLua;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Reflection;

namespace SHCDESE.Extensions;

public static class LuaExtensions
{
    // -------------------------------------------------------------------------------------------
    // Instance method registration
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Scans an object instance and registers all of its methods (and properties) decorated
    /// with <see cref="LuaApiExportAttribute"/>.
    /// <para>
    /// If the instance's type carries a <see cref="LuaApiNamespaceAttribute"/> the exported
    /// Lua name will be <c>&lt;Prefix&gt;_&lt;MemberName&gt;</c>; otherwise the name from
    /// the attribute is used verbatim.
    /// </para>
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="targetInstance">The object whose members will be registered.</param>
    /// <param name="bindingFlags">Reflection flags used during the member search.</param>
    public static void RegisterExportedMethods(
        this NLua.Lua lua,
        object targetInstance,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic)
    {
        if (targetInstance == null)
        {
            LogHelper.Error("Cannot register exported methods: the provided target instance is null.");
            return;
        }

        Type type = targetInstance.GetType();

        foreach (MethodInfo method in type.GetMethods(bindingFlags))
        {
            LuaApiExportAttribute? attr = method.GetCustomAttribute<LuaApiExportAttribute>();
            if (attr == null) continue;

            string luaName = attr.ResolveName(method.DeclaringType);
            lua.SafeRegisterFunction(luaName, targetInstance, method);
        }

        foreach (PropertyInfo property in type.GetProperties(bindingFlags))
        {
            LuaApiExportAttribute? attr = property.GetCustomAttribute<LuaApiExportAttribute>();
            if (attr == null) continue;

            string baseName = attr.ResolveName(property.DeclaringType);

            MethodInfo? getter = property.GetGetMethod(nonPublic: true);
            if (getter == null) continue;

            object? value = getter.Invoke(targetInstance, null);
            if (value == null) continue;

            // Assign the object directly: NLua exposes all its public methods to Lua automatically.
            lua[baseName] = value;
        }
    }

    // -------------------------------------------------------------------------------------------
    // Static method registration
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Scans a <see cref="Type"/> and registers all of its static methods (and
    /// properties) decorated with <see cref="LuaApiExportAttribute"/>.
    /// <para>
    /// If the type carries a <see cref="LuaApiNamespaceAttribute"/> the exported Lua name
    /// will be <c>&lt;Prefix&gt;_&lt;MemberName&gt;</c>; otherwise the attribute name is
    /// used verbatim.
    /// </para>
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="type">The class whose static members will be registered.</param>
    /// <param name="bindingFlags">Reflection flags used during the member search.</param>
    public static void RegisterExportedStaticMethods(this NLua.Lua lua, Type type, BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
    {
        if (type == null)
        {
            LogHelper.Error("Cannot register exported static methods: the provided type is null.");
            return;
        }

        foreach (MethodInfo method in type.GetMethods(bindingFlags))
        {
            LuaApiExportAttribute? attr = method.GetCustomAttribute<LuaApiExportAttribute>();
            if (attr == null) continue;

            string luaName = attr.ResolveName(method.DeclaringType);
            lua.SafeRegisterFunction(luaName, method);
        }

        foreach (PropertyInfo property in type.GetProperties(bindingFlags))
        {
            LuaApiExportAttribute? attr = property.GetCustomAttribute<LuaApiExportAttribute>();
            if (attr == null) continue;

            string baseName = attr.ResolveName(property.DeclaringType);

            MethodInfo? getter = property.GetGetMethod(nonPublic: true);
            if (getter == null) continue;

            object? value = getter.Invoke(null, null);
            if (value == null) continue;

            // Assign the object directly — NLua exposes all its public methods to Lua automatically.
            lua[baseName] = value;
        }
    }

    // -------------------------------------------------------------------------------------------
    // ManagedValue auto-registration
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Registers a managed-value object under a fixed Lua prefix by exposing its
    /// <c>Push</c>, <c>Pop</c>, <c>Set</c>, and <c>GetValue</c> methods automatically.
    /// <para>
    /// The resulting Lua globals will be:
    /// <list type="bullet">
    ///   <item><c>&lt;luaPrefix&gt;_Push(value)</c></item>
    ///   <item><c>&lt;luaPrefix&gt;_Pop()</c></item>
    ///   <item><c>&lt;luaPrefix&gt;_Set(value)</c></item>
    ///   <item><c>&lt;luaPrefix&gt;_GetValue()</c></item>
    /// </list>
    /// Any methods not found on <paramref name="managedValue"/> are skipped with a warning.
    /// </para>
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="luaPrefix">
    /// The prefix used when building the Lua function names, e.g. <c>"WoodCost"</c>.
    /// </param>
    /// <param name="managedValue">
    /// The managed-value object to register. Must expose at least one of the expected methods.
    /// </param>
    /// <param name="bindingFlags">Reflection flags for method lookup.</param>
    public static void RegisterManagedValue(
        this NLua.Lua lua,
        string luaPrefix,
        object managedValue,
        BindingFlags bindingFlags = BindingFlags.Public | BindingFlags.Instance)
    {
        if (string.IsNullOrWhiteSpace(luaPrefix))
        {
            LogHelper.Error("RegisterManagedValue: luaPrefix must not be null or whitespace.");
            return;
        }

        if (managedValue == null)
        {
            LogHelper.Error($"RegisterManagedValue [{luaPrefix}]: managedValue is null.");
            return;
        }

        Type type = managedValue.GetType();

        // The canonical set of method names we look for on a managed-value wrapper.
        ReadOnlySpan<string> methodNames = ["Push", "Pop", "SetValue", "GetValue"];

        bool registeredAny = false;

        foreach (string methodName in methodNames)
        {
            // GetMethod returns null when the method doesn't exist; we skip silently so
            // callers are not forced to implement the full set.
            MethodInfo? method = type.GetMethod(methodName, bindingFlags);
            if (method == null)
            {
                LogHelper.Debug($"RegisterManagedValue [{luaPrefix}]: method '{methodName}' not found on {type.Name} — skipping.");
                continue;
            }

            string luaName = $"{luaPrefix}_{methodName}";
            lua.SafeRegisterFunction(luaName, managedValue, method);
            registeredAny = true;
        }

        if (!registeredAny)
        {
            LogHelper.Warning(
                $"RegisterManagedValue [{luaPrefix}]: no Push/Pop/Set/GetValue methods found on {type.Name}. " +
                "Verify the type implements the expected managed-value interface.");
        }
    }

    /// <summary>
    /// Convenience overload that derives the Lua prefix from the class-level
    /// <see cref="LuaApiNamespaceAttribute"/> on <paramref name="managedValue"/>'s type.
    /// <para>
    /// Throws <see cref="InvalidOperationException"/> if the type does not carry
    /// <see cref="LuaApiNamespaceAttribute"/>.
    /// </para>
    /// </summary>
    /// <param name="lua">The Lua state.</param>
    /// <param name="managedValue">The managed-value object to register.</param>
    public static void RegisterManagedValue(this NLua.Lua lua, object managedValue)
    {
        if (managedValue == null)
        {
            LogHelper.Error("RegisterManagedValue: managedValue is null.");
            return;
        }

        Type type = managedValue.GetType();
        LuaApiNamespaceAttribute? ns = (LuaApiNamespaceAttribute?)Attribute.GetCustomAttribute(type, typeof(LuaApiNamespaceAttribute));

        if (ns == null)
        {
            throw new InvalidOperationException(
                $"RegisterManagedValue (no-prefix overload): type '{type.FullName}' does not carry " +
                $"[{nameof(LuaApiNamespaceAttribute)}]. Either add the attribute or use the overload that accepts an explicit prefix.");
        }

        lua.RegisterManagedValue(ns.Prefix, managedValue);
    }

    // -------------------------------------------------------------------------------------------
    // Delegate-based convenience
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Registers a function for Lua using a delegate, automatically extracting the Lua
    /// function name from the <see cref="LuaApiExportAttribute"/> (combined with any
    /// class-level <see cref="LuaApiNamespaceAttribute"/>).
    /// </summary>
    public static void SafeRegisterFunction(this NLua.Lua lua, Delegate methodDelegate)
    {
        if (methodDelegate == null)
        {
            LogHelper.Error("Cannot register function: the provided delegate is null!");
            return;
        }

        MethodInfo methodInfo = methodDelegate.Method;
        LuaApiExportAttribute? attr = methodInfo.GetCustomAttribute<LuaApiExportAttribute>();

        if (attr == null)
        {
            LogHelper.Error(
                $"Cannot register function [{methodInfo.Name}]: it is missing the [{nameof(LuaApiExportAttribute)}] attribute.");
            return;
        }

        string luaName = attr.ResolveName(methodInfo.DeclaringType);
        object? target = methodDelegate.Target;

        if (methodInfo.IsStatic)
            lua.SafeRegisterFunction(luaName, methodInfo);
        else
            lua.SafeRegisterFunction(luaName, target!, methodInfo);
    }

    // -------------------------------------------------------------------------------------------
    // Core NLua wrappers
    // -------------------------------------------------------------------------------------------

    /// <summary>
    /// Safely wraps <see cref="NLua.Lua.RegisterFunction(string, object, MethodBase)"/> with
    /// null-checks and diagnostic logging.
    /// </summary>
    public static void SafeRegisterFunction(this NLua.Lua lua, string path, object target, MethodBase method)
    {
        if (target == null)
        {
            LogHelper.Error($"Cannot register [{path}]: target object instance not found!");
            return;
        }

        if (method == null)
        {
            LogHelper.Error($"Cannot register [{path}]: managed method not found!");
            return;
        }

        LogHelper.Debug($"Registering lua-function [{path}] for {method.Name} (instance)");
        lua.RegisterFunction(path, target, method);
    }

    /// <summary>
    /// Safely wraps <see cref="NLua.Lua.RegisterFunction(string, object, MethodBase)"/> for
    /// static methods (no target instance).
    /// </summary>
    public static void SafeRegisterFunction(this NLua.Lua lua, string path, MethodBase method)
    {
        if (method == null)
        {
            LogHelper.Error($"Cannot register [{path}]: managed method not found!");
            return;
        }

        LogHelper.Debug($"Registering lua-function [{path}] for {method.Name} (static)");
        lua.RegisterFunction(path, null, method);
    }
}