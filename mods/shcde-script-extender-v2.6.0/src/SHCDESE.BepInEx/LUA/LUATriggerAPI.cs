using SHCDESE.API;
using SHCDESE.Extensions;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes the GameTriggerAPI functionality to the Lua scripting environment.
/// </summary>
public static class LuaTriggerAPI
{
    /// <summary>
    /// Registers all trigger-related functions and helper methods with the specified Lua instance.
    /// </summary>
    /// <param name="lua">The Lua state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameTriggerManager.Instance);

    }
}
