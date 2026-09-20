using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using System.Runtime.CompilerServices;
using UnityEngine;

namespace SHCDESE.ImMenu.Visualizers;

/// <summary>
/// Visualizes raw memory grids from the GameTileManager on the Unity Tilemap.
/// </summary>
public unsafe class DebugGridVisualizer : MonoBehaviour
{
    public static DebugGridVisualizer Instance { get; private set; }

    // --- Configuration ---
    public bool IsActive = false;
    public UnknownGridTarget SelectedGrid = UnknownGridTarget.None;

    // Heatmap Settings
    public bool AutoScale = true;
    public float MinVal = 0;
    public float MaxVal = 10;
    public float Alpha = 0.75f;
    public bool AutoRefresh = false;

    // --- Cache ---
    private Color?[] _colorCache;
    private const int MAX_TILES = 800 * 800;

    // --- Debug Info ---
    private float _hoverValue = 0;
    private string _hoverAddress = "0x0000000000000000"; // Cache string to avoid allocs
    private int _hoverTileId = -1;
    private Vector2Int _hoverCoords = Vector2Int.zero;

    public enum UnknownGridTarget
    {
        None,
        // --- Previously "UnknownGrid" entries, now resolved ---
        PillarGFX_Int32,          // was Grid1_Int32     | pillar_gfx_layer   (0x4EC680, dynamic)
        UnknownGrid_0xA20D40_Byte,// was Grid2_Byte      | (TODO, offset 0xA20D40)
        ShowHi_Byte,              // was Grid3_Byte      | show_hi_layer       (0xEB7A20)
        MiscDisplay_UInt16,       // was Grid4_UInt16    | misc_display_layer  (0xF05F40)
        PathConnection_UInt16,    // was Grid5_UInt16    | path_connection_layer(0x108D8E0)
        PathEdgeMask_Byte,
        AIZone_Byte,              // was Grid6_Byte      | ai_zone_layer       (0x13001E0)
        Occupancy_Byte,           // was Grid7_Byte      | occupancy_layer     (0x1178840, CompactPlayerBitMask)
        Delay_Byte,               // was Grid8_Byte      | delay_layer         (0x1CCBEF0)
        // --- Named grids ---
        Height,
        PropertyFlags,            // logic_layer
        BuildingId,               // structure_layer
        OwnerId,                  // wall_owner_layer
        // --- GFX grids ---
        GFX_Int32,                // gfx_layer           (0x140900)
        AlphaGFX_Int32,           // alpha_gfx_layer     (0x279D80)
        ConstructionGFX_Int32,    // construction_layer  (0x3B3200)
        WallGFX_Int32,            // wall_gfx_layer      (0x625B00)
        // --- Other named grids ---
        UnknownGrid_0x75EF80_Int16,   // (TODO, Int16)
        RandomNoise_UInt16,            // random_layer       (0x7FB5C0)
        StructureWas_Byte,             // structure_was_layer(0xBA86E0)
        FlyLayer_Int16,                // fly_layer          (0xC93640, projectile/deco IDs)
        UnknownGrid_0xD30080_Byte,     // (TODO, byte)
        Luminescence_Byte,             // luminesence_layer  (0xE69500)
        MacroLayer_Int16,              // macro_layer        (0xFF0EA0)
        UnknownGrid_0x134E700_Byte,    // (TODO, byte)
        AIDanger_Byte,                 // ai_danger_layer    (0x139CC20, death heatmap)
        UnknownGrid_0x13EB140_Byte,    // (TODO, byte, pathmarker decay: Arab units)
        AIVBlockLayer_Byte,            // aiv_block_layer    (0x1C7C0C0, CompactPlayerBitMask)
        GatePath_Byte,                 // gate_path_layer    (0x1D1A730)
        UnknownGrid_0x1D68F70_Int32,   // (TODO, Int32)
        MoatWorkTaskIndexGrid_UInt16  // (TODO, UInt16)
    }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _colorCache = new Color?[MAX_TILES];
    }

    public void RenderUI()
    {
        ImGui.Checkbox("Enable Visualizer", ref IsActive);

        if (!IsActive) return;

        ImGui.Separator();

        // Grid Selector
        if (ImGui.BeginCombo("Target Grid", SelectedGrid.ToString()))
        {
            foreach (UnknownGridTarget grid in Enum.GetValues(typeof(UnknownGridTarget)))
            {
                if (ImGui.Selectable(grid.ToString(), SelectedGrid == grid))
                {
                    SelectedGrid = grid;
                    RefreshData(); // Immediate update on change
                }
            }
            ImGui.EndCombo();
        }

        // Settings
        ImGui.BeginGroup();
        ImGui.Text("Settings");
        ImGui.Checkbox("Auto Scale", ref AutoScale);

        if (!AutoScale)
        {
            ImGui.DragFloat("Min", ref MinVal);
            ImGui.DragFloat("Max", ref MaxVal);
        }
        else
        {
            ImGui.TextDisabled($"Auto Range: {MinVal:F2} - {MaxVal:F2}");
            // Add a reset button in case auto-scale gets stuck on garbage data
            if (ImGui.Button("Reset Range")) { MinVal = 0; MaxVal = 100; }
        }

        ImGui.SliderFloat("Alpha", ref Alpha, 0.0f, 1.0f);

        ImGui.Checkbox("Live Update (30fps)", ref AutoRefresh);
        if (!AutoRefresh && ImGui.Button("Force Refresh Now"))
        {
            RefreshData();
        }
        ImGui.EndGroup();

        // Debug Info
        ImGui.BeginGroup();
        ImGui.TextColored(new System.Numerics.Vector4(1, 1, 0, 1), "Cursor Probe");
        ImGui.Text($"Tile X,Y: {_hoverCoords.x}, {_hoverCoords.y}");
        ImGui.Text($"Tile ID : {_hoverTileId}");
        ImGui.Text($"Raw Value: {_hoverValue} / {((int)_hoverValue).ToString("X8")}");
        ImGui.Text($"Address  : {_hoverAddress}");
        ImGui.Text($"Color : {(_hoverTileId != -1 && _hoverTileId < MAX_TILES ? _colorCache[_hoverTileId]?.ToString() : "None")}");
        ImGui.EndGroup();
    }

    private void Update()
    {
        if (!IsActive) return;

        // Probe the tile under mouse for debug info
        ProbeCursor();

        if (AutoRefresh)
        {
            // Throttle to every 2000 frames to save FPS
            if (Time.frameCount % 2000 == 0)
            {
                RefreshData();
            }
        }
    }

    private void ProbeCursor()
    {
        if (GamePlayerManagerAPI.Instance.CursorManager != null)
        {
            int tid = (int)GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileId;
            UnmanagedVector2<ushort> pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tid);
            int tx = pos.X;
            int ty = pos.Y;

            _hoverCoords = new Vector2Int(tx, ty);
            _hoverTileId = tid;
            GameTileManagerView view = GameTileManagerAPI.Instance.TileManager;

            // Grab value directly from memory for accuracy
            if (view != null && tid >= 0 && tid < MAX_TILES)
            {
                _hoverValue = GetValueForTileId(view, tid, SelectedGrid);
                IntPtr ptr = GetAddressForTileId(view, _hoverTileId, SelectedGrid);
                _hoverAddress = ptr == IntPtr.Zero ? "N/A" : $"0x{ptr.ToInt64():X16}";
            }
        }
    }

    private void RefreshData()
    {
        if (GameTileManagerAPI.Instance.TileManager == null) return;
        GameTileManagerView view = GameTileManagerAPI.Instance.TileManager;

        // Clear Cache
        Array.Clear(_colorCache, 0, _colorCache.Length);

        // Calculate Min/Max (if Auto)
        // We iterate 0..MapSize to find valid ranges. 
        if (AutoScale)
        {
            float min = float.MaxValue;
            float max = float.MinValue;
            PopulateMinMax(view, ref min, ref max);

            // Safety clamp to prevent div/0
            if (Math.Abs(max - min) < 0.0001f) max = min + 1.0f;

            MinVal = min;
            MaxVal = max;
        }

        // Fill Color Cache
        PopulateColorCache(view);

        // Trigger Unity Update
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (TilemapManager.instance != null)
            {
                TilemapManager.instance.triggerTMFullRefresh();
            }
        });
    }

    // Helper to avoid switch statement overhead inside tight loops by picking a delegate or logic block
    // Here we just use a helper that switches on the Span type for the specific loop
    private void PopulateMinMax(GameTileManagerView view, ref float min, ref float max)
    {
        int limit = MAX_TILES;
        if (SelectedGrid == UnknownGridTarget.PillarGFX_Int32)
        {
            Span<int> span = view.PillarGFXGrid;
            for (int i = 0; i < limit; i++) { float v = span[i]; if (v < min) min = v; if (v > max) max = v; }
        }
        else if (SelectedGrid == UnknownGridTarget.UnknownGrid_0xA20D40_Byte)
        {
            Span<byte> span = view.UnknownGrid2;
            for (int i = 0; i < limit; i++) { float v = span[i]; if (v < min) min = v; if (v > max) max = v; }
        }
        else if (SelectedGrid == UnknownGridTarget.Height)
        {
            Span<byte> span = view.HeightGrid;
            for (int i = 0; i < limit; i++) { float v = span[i]; if (v < min) min = v; if (v > max) max = v; }
        }
        else
        {
            for (int i = 0; i < limit; i++)
            {
                float v = GetValueForTileId(view, i, SelectedGrid);
                if (v < min) min = v;
                if (v > max) max = v;
            }
        }
    }

    private void PopulateColorCache(GameTileManagerView view)
    {
        int width = 800;
        int height = 800;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);

                if (tileId >= 0 && tileId < MAX_TILES)
                {
                    float val = GetValueForTileId(view, tileId, SelectedGrid);
                    _colorCache[tileId] = GetHeatMapColor(val);
                }
            }
        }
    }
    private IntPtr GetAddressForTileId(GameTileManagerView view, int id, UnknownGridTarget target)
    {
        if (target == UnknownGridTarget.None)
            return IntPtr.Zero;

        try
        {
            switch (target)
            {
                // Int32 spans
                case UnknownGridTarget.PillarGFX_Int32:
                    return (IntPtr)Unsafe.AsPointer(ref view.PillarGFXGrid[id]);
                case UnknownGridTarget.PropertyFlags:
                    return (IntPtr)Unsafe.AsPointer(ref view.LogicGrid[id]);
                case UnknownGridTarget.GFX_Int32:
                    return (IntPtr)Unsafe.AsPointer(ref view.GFXGrid[id]);
                case UnknownGridTarget.AlphaGFX_Int32:
                    return (IntPtr)Unsafe.AsPointer(ref view.AlphaGFXGrid[id]);
                case UnknownGridTarget.ConstructionGFX_Int32:
                    return (IntPtr)Unsafe.AsPointer(ref view.ConstructionGrid[id]);
                case UnknownGridTarget.WallGFX_Int32:
                    return (IntPtr)Unsafe.AsPointer(ref view.WallGFXGrid[id]);
                case UnknownGridTarget.UnknownGrid_0x1D68F70_Int32:
                    return (IntPtr)Unsafe.AsPointer(ref view.UnknownGrid_0x1D68F70[id]);

                // Byte spans
                case UnknownGridTarget.UnknownGrid_0xA20D40_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.UnknownGrid2[id]);
                case UnknownGridTarget.ShowHi_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.ShowHiGrid[id]);
                case UnknownGridTarget.AIZone_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.AIZoneGrid[id]);
                case UnknownGridTarget.Occupancy_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.OccupancyGrid[id]);
                case UnknownGridTarget.Delay_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.DelayGrid[id]);
                case UnknownGridTarget.Height:
                    return (IntPtr)Unsafe.AsPointer(ref view.HeightGrid[id]);
                case UnknownGridTarget.OwnerId:
                    return (IntPtr)Unsafe.AsPointer(ref view.WallOwnerGrid[id]);
                case UnknownGridTarget.StructureWas_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.StructureWasGrid[id]);
                case UnknownGridTarget.UnknownGrid_0xD30080_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.UnknownGrid_0xD30080[id]);
                case UnknownGridTarget.Luminescence_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.LuminesenceGrid[id]);
                case UnknownGridTarget.UnknownGrid_0x134E700_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.UnknownGrid_0x134E700[id]);
                case UnknownGridTarget.AIDanger_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.AIDangerGrid[id]);
                case UnknownGridTarget.UnknownGrid_0x13EB140_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.UnknownGrid_0x13EB140[id]);
                case UnknownGridTarget.AIVBlockLayer_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.AIVBlockGrid[id]);
                case UnknownGridTarget.GatePath_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.GatePathGrid[id]);
                case UnknownGridTarget.PathEdgeMask_Byte:
                    return (IntPtr)Unsafe.AsPointer(ref view.PathEdgeMaskGrid[id]);

                // UInt16 / Int16 spans
                case UnknownGridTarget.MiscDisplay_UInt16:
                    return (IntPtr)Unsafe.AsPointer(ref view.MiscDisplayGrid[id]);
                case UnknownGridTarget.PathConnection_UInt16:
                    return (IntPtr)Unsafe.AsPointer(ref view.PathConnectionGrid[id]);
                case UnknownGridTarget.BuildingId:
                    return (IntPtr)Unsafe.AsPointer(ref view.StructureGrid[id]);
                case UnknownGridTarget.UnknownGrid_0x75EF80_Int16:
                    return (IntPtr)Unsafe.AsPointer(ref view.UnknownGrid_0x75EF80[id]);
                case UnknownGridTarget.FlyLayer_Int16:
                    return (IntPtr)Unsafe.AsPointer(ref view.FlyGrid[id]);
                case UnknownGridTarget.MacroLayer_Int16:
                    return (IntPtr)Unsafe.AsPointer(ref view.MacroGrid[id]);
                case UnknownGridTarget.RandomNoise_UInt16:
                    return (IntPtr)Unsafe.AsPointer(ref view.TileRandomNoiseGrid[id]);
                case UnknownGridTarget.MoatWorkTaskIndexGrid_UInt16:
                    return (IntPtr)Unsafe.AsPointer(ref view.MoatWorkTaskIndexGrid[id]);

                default:
                    return IntPtr.Zero;
            }
        }
        catch
        {
            // Map reinit / bounds race protection
            return IntPtr.Zero;
        }
    }

    private float GetValueForTileId(GameTileManagerView view, int id, UnknownGridTarget target)
    {
        switch (target)
        {
            case UnknownGridTarget.PillarGFX_Int32: return view.PillarGFXGrid[id];
            case UnknownGridTarget.UnknownGrid_0xA20D40_Byte: return view.UnknownGrid2[id];
            case UnknownGridTarget.ShowHi_Byte: return view.ShowHiGrid[id];
            case UnknownGridTarget.MiscDisplay_UInt16: return view.MiscDisplayGrid[id];
            case UnknownGridTarget.PathConnection_UInt16: return view.PathConnectionGrid[id];
            case UnknownGridTarget.PathEdgeMask_Byte: return view.PathEdgeMaskGrid[id];
            case UnknownGridTarget.AIZone_Byte: return view.AIZoneGrid[id];
            case UnknownGridTarget.Occupancy_Byte: return (float)view.OccupancyGrid[id];
            case UnknownGridTarget.Delay_Byte: return view.DelayGrid[id];
            case UnknownGridTarget.Height: return view.HeightGrid[id];
            case UnknownGridTarget.PropertyFlags: return view.LogicGrid[id];
            case UnknownGridTarget.BuildingId: return view.StructureGrid[id];
            case UnknownGridTarget.OwnerId: return view.WallOwnerGrid[id];
            case UnknownGridTarget.GFX_Int32: return view.GFXGrid[id];
            case UnknownGridTarget.AlphaGFX_Int32: return view.AlphaGFXGrid[id];
            case UnknownGridTarget.ConstructionGFX_Int32: return view.ConstructionGrid[id];
            case UnknownGridTarget.WallGFX_Int32: return view.WallGFXGrid[id];
            case UnknownGridTarget.UnknownGrid_0x75EF80_Int16: return view.UnknownGrid_0x75EF80[id];
            case UnknownGridTarget.RandomNoise_UInt16: return view.TileRandomNoiseGrid[id];
            case UnknownGridTarget.StructureWas_Byte: return view.StructureWasGrid[id];
            case UnknownGridTarget.FlyLayer_Int16: return view.FlyGrid[id];
            case UnknownGridTarget.UnknownGrid_0xD30080_Byte: return view.UnknownGrid_0xD30080[id];
            case UnknownGridTarget.Luminescence_Byte: return view.LuminesenceGrid[id];
            case UnknownGridTarget.MacroLayer_Int16: return view.MacroGrid[id];
            case UnknownGridTarget.UnknownGrid_0x134E700_Byte: return view.UnknownGrid_0x134E700[id];
            case UnknownGridTarget.AIDanger_Byte: return view.AIDangerGrid[id];
            case UnknownGridTarget.UnknownGrid_0x13EB140_Byte: return view.UnknownGrid_0x13EB140[id];
            case UnknownGridTarget.AIVBlockLayer_Byte: return (float)view.AIVBlockGrid[id];
            case UnknownGridTarget.GatePath_Byte: return view.GatePathGrid[id];
            case UnknownGridTarget.UnknownGrid_0x1D68F70_Int32: return view.UnknownGrid_0x1D68F70[id];
            case UnknownGridTarget.MoatWorkTaskIndexGrid_UInt16: return view.MoatWorkTaskIndexGrid[id];
            default: return 0;
        }
    }

    private Color GetHeatMapColor(float value)
    {
        float t = Mathf.InverseLerp(MinVal, MaxVal, value);

        // 0 -> Blue, 0.5 -> Green, 1.0 -> Red
        return Color.HSVToRGB((1.0f - t) * 0.66f, 1.0f, 1.0f).ToAlpha(Alpha);
    }

    public bool TryGetColorOverride(int x, int y, out Color color)
    {
        color = Color.white;
        if (!IsActive) return false;

        // We must use the exact same ID lookup here
        int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);

        if (tileId >= 0 && tileId < MAX_TILES)
        {
            Color? c = _colorCache[tileId];
            if (c.HasValue)
            {
                color = c.Value;
                return true;
            }
        }
        return false;
    }
}

// Extension for alpha
public static class ColorExt
{
    public static Color ToAlpha(this Color c, float a) { c.a = a; return c; }
}