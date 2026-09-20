using NLua;
using System;

namespace SHCDESE.Lua;

public static class LuaUtil
{
    /// <summary>
    /// Injects an enum into the Lua global scope as a table
    /// with all its names mapped to values.
    /// Example: RegisterEnum(lua, typeof(eChimps), "eChimps")
    /// allows Lua code to use eChimps.SOME_VALUE
    /// </summary>
    public static void RegisterEnum(NLua.Lua lua, Type enumType, string globalName)
    {
        if (!enumType.IsEnum)
            throw new ArgumentException($"{enumType.FullName} is not an enum type");

        lua.NewTable(globalName);
        LuaTable table = lua.GetTable(globalName);
        foreach (string name in Enum.GetNames(enumType))
        {
            object value = Enum.Parse(enumType, name);
            table[name] = value;
        }

        // put the table into global scope
        lua[globalName] = table;
    }
}
