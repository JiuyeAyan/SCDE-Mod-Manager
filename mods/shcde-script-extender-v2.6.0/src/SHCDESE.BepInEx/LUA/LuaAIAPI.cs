using SHCDESE.API;
using SHCDESE.Extensions;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameAIManagerAPI functionality to the Lua scripting environment.
/// </summary>
public static class LuaAIAPI
{
    /// <summary>
    /// Registers ai-related functions with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameAIManagerAPI.Instance);
    }

}
