using NLua;
using SHCDESE.API;
using SHCDESE.Extensions;

namespace SHCDESE.Lua;

/// <summary>
/// Provides a static API bridge that exposes the <see cref="GameMetadataManagerAPI"/>
/// functionality to the Lua scripting environment.
/// </summary>
public static class LuaMetadataAPI
{
    /// <summary>
    /// Registers all metadata-related C# functions as global functions within the specified Lua state.
    /// </summary>
    /// <param name="lua">The target <see cref="NLua.Lua"/> state to register the functions into.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedMethods(GameMetadataManagerAPI.Instance);

    }
}