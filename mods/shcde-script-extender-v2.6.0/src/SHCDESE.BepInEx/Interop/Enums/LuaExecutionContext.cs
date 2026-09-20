namespace SHCDESE.Interop.Enums;

/// <summary>
/// Represents the execution context for Lua scripts, determining which file sources are accessible.
/// </summary>
public enum LuaExecutionContext
{
    /// <summary>Map archive context - can only access files within the map's zip archive.</summary>
    MapArchive,

    /// <summary>Lord AI context - can access the lord's directory and registered asset mods.</summary>
    LordAI,

    /// <summary>Asset mod context - can access the mod's directory and other registered asset mods.</summary>
    AssetMod
}