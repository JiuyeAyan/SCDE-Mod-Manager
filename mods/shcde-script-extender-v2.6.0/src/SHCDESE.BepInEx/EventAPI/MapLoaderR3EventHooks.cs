using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to map loading and unloading events.
/// </summary>
/// <remarks>
/// This class centralizes events related to the game's map lifecycle, such as loading a new map,
/// starting a game session, loading a saved game, and unloading the current map.
/// </remarks>
public static class MapLoaderR3EventHooks
{
    /// <summary>
    /// Fired when the current map is being unloaded and cleared from memory.
    /// </summary>
    [LuaApiExport("OnUnloadMap")]
    public static readonly R3EventHook<MapUnloadEventArgs> OnUnloadMap = new();

    /// <summary>
    /// Fired when the game session begins after a map has been loaded.
    /// </summary>
    [LuaApiExport("OnStartMap")]
    public static readonly R3EventHook<MapStartEventArgs> OnStartMap = new();

    /// <summary>
    /// Fired when a map file (e.g., custom map, campaign mission) is being loaded to be played.
    /// </summary>
    [LuaApiExport("OnLoadMap")]
    public static readonly R3EventHook<MapLoadEventArgs> OnLoadMap = new();

    /// <summary>
    /// Fired when a saved game file (.sav) is being loaded.
    /// </summary>
    [LuaApiExport("OnLoadSave")]
    public static readonly R3EventHook<LoadSaveGameEventArgs> OnLoadSave = new();
}