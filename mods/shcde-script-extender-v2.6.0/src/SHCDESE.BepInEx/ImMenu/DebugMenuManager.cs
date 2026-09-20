using SHCDESE.DebugMenu.ImMenu;
using SHCDESE.ImMenu.MapEditor;
using System;
using UUIMGUI.API;

namespace SHCDESE.DebugMenu;

public unsafe class DebugMenuManager
{
    private static readonly Lazy<DebugMenuManager> lazy = new Lazy<DebugMenuManager>(() => new DebugMenuManager());

    public static DebugMenuManager Instance { get { return lazy.Value; } }

    private DebugMenuManager()
    {
        _mapRegionSelector = new MapRegionSelector();
        _massTileHeightEditor = new MassTileHeightEditor(_mapRegionSelector);
        _tileInspectorTool = new TileInspectorTool();
        _mirrorTool = new MirrorTool();
        _tilePropertyMutatorTool = new TilePropertyMutatorTool();
    }

    internal MapRegionSelector _mapRegionSelector;
    internal MassTileHeightEditor _massTileHeightEditor;
    internal TileInspectorTool _tileInspectorTool;
    internal MirrorTool _mirrorTool;
    internal TilePropertyMutatorTool _tilePropertyMutatorTool;

    public void Initialize()
    {
        ImGUIAPI.AddMenuCallback(&UserMenuRenderer.PresentCallback);
        ImGUIAPI.AddAlwaysCallback(&UserMenuRenderer.PresentCallbackLive);
    }
}