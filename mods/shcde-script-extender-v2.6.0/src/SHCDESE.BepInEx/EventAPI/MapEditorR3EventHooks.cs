using SHCDESE.EventAPI.MapEditor;
using SHCDESE.Lua.DocsGen;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to map editor actions.
/// </summary>
/// <remarks>
/// This class centralizes events related to direct map manipulation within the editor,
/// such as terrain painting, height adjustment and object placement.
/// </remarks>
public static class MapEditorR3EventHooks
{
    /// <summary>
    /// Fired when vegetation (e.g., trees, bushes) is placed on the map.
    /// </summary>
    [LuaApiExport("OnVegetationPlace")]
    public static readonly R3EventHook<VegetationPlaceEventArgs> OnVegetationPlace = new();

    /// <summary>
    /// Fired when a terrain brush is used to paint a tile's type.
    /// </summary>
    [LuaApiExport("OnSetTileType")]
    public static readonly R3EventHook<BrushSetTileTypeEventArgs> OnSetTileType = new();

    /// <summary>
    /// Fired when a terrain brush is used to paint a tile's type to nothing.
    /// </summary>
    [LuaApiExport("OnSetTileTypeToNone")]
    public static readonly R3EventHook<BrushSetTileTypeToNoneEventArgs> OnSetTileTypeToNone = new();

    /// <summary>
    /// Fired when a terrain brush is used to modify the height of the landscape to a preset
    /// </summary>
    [LuaApiExport("OnSetTileHeightToPreset")]
    public static readonly R3EventHook<BrushSetTileHeightToPresetEventArgs> OnSetTileHeightToPreset = new();

    /// <summary>
    /// Fired when a terrain brush is used to modify the height of the landscape to a specified number
    /// </summary>
    [LuaApiExport("OnSetTileHeightToNumber")]
    public static readonly R3EventHook<BrushSetTileHeightToNumberEventArgs> OnSetTileHeightToNumber = new();

    /// <summary>
    /// Fired when a terrain brush is used to even the height of the landscape.
    /// </summary>
    [LuaApiExport("OnEvenTileHeight")]
    public static readonly R3EventHook<BrushEvenTileHeightEventArgs> OnEvenTileHeight = new();

    /// <summary>
    /// Fired when a terrain brush is used to modify the height of the landscape (raise).
    /// </summary>
    [LuaApiExport("OnRaiseTileHeight")]
    public static readonly R3EventHook<BrushRaiseTileHeightEventArgs> OnRaiseTileHeight = new();

    /// <summary>
    /// Fired when a terrain brush is used to delete entities in an area
    /// </summary>
    [LuaApiExport("OnDelete")]
    public static readonly R3EventHook<BrushDeleteEventArgs> OnDelete = new();

    /// <summary>
    /// Fired when a unit is placed on the map from the editor menu.
    /// </summary>
    [LuaApiExport("OnUnitPlace")]
    public static readonly R3EventHook<UnitPlaceEventArgs> OnUnitPlace = new();

    /// <summary>
    /// Fired when a animal is placed on the map from the editor menu.
    /// </summary>
    [LuaApiExport("OnAnimalPlace")]
    public static readonly R3EventHook<AnimalPlaceEventArgs> OnAnimalPlace = new();
}