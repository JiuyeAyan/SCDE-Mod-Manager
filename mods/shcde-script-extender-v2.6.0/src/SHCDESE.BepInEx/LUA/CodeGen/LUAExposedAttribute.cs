using System;

namespace SHCDESE.Lua.CodeGen;

/// <summary>
/// Specifys which fields are exposed to LUA (Apply on structs only)
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class LuaExposedAttribute : Attribute
{

}