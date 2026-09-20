using SHCDESE.Lua.DocsGen;
using System;

namespace SHCDESE.LUA.DocsGen;

/// <summary>
/// When placed on a <b>class or struct</b>, defines the namespace prefix that will be
/// prepended to every method-level <see cref="LuaApiExportAttribute"/> name inside that type.
/// <para>
/// Example: a class decorated with <c>[LuaApiNamespace("Building")]</c> and a method decorated
/// with <c>[LuaApiExport("GetWoodCost")]</c> will be registered in Lua as
/// <c>Building_GetWoodCost</c>.
/// </para>
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class LuaApiNamespaceAttribute : Attribute
{
    /// <summary>
    /// The prefix that is prepended to each exported member name, separated by an underscore.
    /// </summary>
    public string Prefix { get; }

    /// <param name="prefix">
    /// The namespace prefix (e.g. <c>"Building"</c>).  Must not be null or whitespace.
    /// </param>
    public LuaApiNamespaceAttribute(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix must not be null or whitespace.", nameof(prefix));

        Prefix = prefix;
    }
}