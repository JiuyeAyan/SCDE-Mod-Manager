using NLua;
using SHCDESE.API;
using SHCDESE.Extensions;
using System.Reflection;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameTranslateAPI functionality to the Lua scripting environment.
/// </summary>
public unsafe static class LuaTranslateAPI
{
    /// <summary>
    /// Registers all translation-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameTranslateAPI.Instance);

    }
}
