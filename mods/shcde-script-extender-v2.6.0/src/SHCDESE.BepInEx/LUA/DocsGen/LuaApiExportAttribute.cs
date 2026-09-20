using SHCDESE.LUA.DocsGen;
using System;

namespace SHCDESE.Lua.DocsGen;

/// <summary>
/// Marks a method, property, or field to be exported to the Lua API and included in
/// documentation generation.
/// <para>
/// If the declaring type carries a <see cref="LuaApiNamespaceAttribute"/>, the final Lua
/// function name will be <c>&lt;Prefix&gt;_&lt;LuaFunctionName&gt;</c>.  Otherwise the
/// <see cref="LuaFunctionName"/> is used verbatim, preserving full backwards compatibility.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Field | AttributeTargets.Property)]
public sealed class LuaApiExportAttribute : Attribute
{
    /// <summary>
    /// The local name of the function as declared on the member.
    /// Use <see cref="ResolveName"/> to obtain the final, fully-qualified Lua name.
    /// </summary>
    public string LuaFunctionName { get; }

    /// <param name="luaFunctionName">
    /// The local Lua name for this member (e.g. <c>"GetWoodCost"</c>).
    /// </param>
    public LuaApiExportAttribute(string luaFunctionName)
    {
        LuaFunctionName = luaFunctionName;
    }

    /// <summary>
    /// Resolves the final Lua-visible name for this attribute by combining the optional
    /// class-level <see cref="LuaApiNamespaceAttribute"/> prefix with
    /// <see cref="LuaFunctionName"/>.
    /// </summary>
    /// <param name="declaringType">
    /// The <see cref="Type"/> that owns the annotated member.  Pass <c>null</c> to skip
    /// prefix resolution (the raw <see cref="LuaFunctionName"/> is returned).
    /// </param>
    /// <returns>
    /// <c>"&lt;Prefix&gt;_&lt;LuaFunctionName&gt;"</c> when a namespace attribute is
    /// present; otherwise <see cref="LuaFunctionName"/> unchanged.
    /// </returns>
    public string ResolveName(Type? declaringType)
    {
        if (declaringType == null)
            return LuaFunctionName;

        LuaApiNamespaceAttribute? ns = declaringType.GetCustomAttributes(typeof(LuaApiNamespaceAttribute), inherit: false) as LuaApiNamespaceAttribute[] is { Length: > 0 } arr
            ? arr[0]
            : null;

        // Slightly more readable alternative without the pattern above:
        if (ns == null)
        {
            ns = (LuaApiNamespaceAttribute?)Attribute.GetCustomAttribute(declaringType, typeof(LuaApiNamespaceAttribute));
        }

        return ns != null ? $"{ns.Prefix}_{LuaFunctionName}" : LuaFunctionName;
    }
}