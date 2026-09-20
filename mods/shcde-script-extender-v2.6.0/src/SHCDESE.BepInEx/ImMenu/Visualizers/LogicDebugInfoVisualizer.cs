using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Interop;
using System;
using UnityEngine;

namespace SHCDESE.ImMenu.Visualizers;

/// <summary>
/// Separate Dear ImGui visualizer that probes tiles using EngineInterface.GetLayerDebug(x, y)
/// and renders a selectable field from <see cref="EngineInterface.LogicDebugInfo"/> as a heatmap
/// on the Unity Tilemap, completely independent of <see cref="DebugGridVisualizer"/>.
/// This function is dealing with the direct return of the native engine function
/// __int64 __fastcall DLL_GetLayerDebug(int x, int y, int *retData)
/// </summary>
public unsafe class LogicDebugInfoVisualizer : MonoBehaviour
{
    public static LogicDebugInfoVisualizer Instance { get; private set; }

    // State 
    public bool IsActive = false;
    public LogicDebugField SelectedField = LogicDebugField.logic_layer;

    // Heatmap settings
    public bool AutoScale = true;
    public float MinVal = 0f;
    public float MaxVal = 10f;
    public float Alpha = 0.75f;
    public bool AutoRefresh = false;

    // Cache
    private Color?[] _colorCache;
    private const int MAP_SIZE = 800;
    private const int MAX_TILES = MAP_SIZE * MAP_SIZE;

    // Hover probe
    private Vector2Int _hoverCoords = Vector2Int.zero;
    private int _hoverTileId = -1;
    private EngineInterface.LogicDebugInfo _hoverInfo;

    // All field names 
    public enum LogicDebugField
    {
        gfx_layer,
        gfx_layer_file,
        gfx_layer_id,
        alpha_gfx_layer,
        construction_gfx_layer,
        pillar_gfx_layer,
        pillar_gfx_layer_file,
        pillar_gfx_layer_id,
        wall_gfx_layer,
        wall_gfx_layer_file,
        wall_gfx_layer_id,
        floating_layer,
        random_layer,
        logic_layer,
        logic2_layer,
        changed_layer,
        organism_layer,
        structure_layer,
        structure_was_layer,
        chimp_layer,
        fly_layer,
        height_layer,
        default_height_layer,
        wall_owner_layer,
        luminesence_layer,
        show_hi_layer,
        misc_display_layer,
        damage_layer,
        macro_layer,
        path_connection_layer,
        path_linkage_layer,
        occupancy_layer,
        certain_path_layer,
        walk_layer,
        ai_zone_layer,
        ai_info_layer,
        ai_danger_layer,
        ai_proximity_layer,
        town_dz_spread_id,
        town_null_connects,
        town_dz_spread_count,
        town_stone_value,
        town_structure,
        town_oasis,
        town_farm,
        town_iron,
        problem_build,
        aiv_block_zone,
        delay_layer,
        aiv_block_layer,
        mapOfset
    }

    // Unity lifecycle
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _colorCache = new Color?[MAX_TILES];
    }

    private void Update()
    {
        if (!IsActive) return;

        ProbeCursor();

        if (AutoRefresh && Time.frameCount % 2000 == 0)
            RefreshData();
    }

    public void RenderUI()
    {
        ImGui.Checkbox("Enable Logic Layer Visualizer", ref IsActive);

        if (!IsActive) return;

        ImGui.Separator();

        // Field selector
        if (ImGui.BeginCombo("Target Field", SelectedField.ToString()))
        {
            foreach (LogicDebugField field in Enum.GetValues(typeof(LogicDebugField)))
            {
                if (ImGui.Selectable(field.ToString(), SelectedField == field))
                {
                    SelectedField = field;
                    RefreshData();
                }
            }
            ImGui.EndCombo();
        }

        ImGui.Spacing();

        // Left group: settings
        ImGui.BeginGroup();
        ImGui.TextColored(new System.Numerics.Vector4(0.4f, 0.8f, 1f, 1f), "Heatmap Settings");

        ImGui.Checkbox("Auto Scale", ref AutoScale);
        if (!AutoScale)
        {
            ImGui.DragFloat("Min", ref MinVal);
            ImGui.DragFloat("Max", ref MaxVal);
        }
        else
        {
            ImGui.TextDisabled($"Auto Range: {MinVal:F2} – {MaxVal:F2}");
            if (ImGui.Button("Reset Range")) { MinVal = 0; MaxVal = 100; }
        }

        ImGui.SliderFloat("Alpha", ref Alpha, 0f, 1f);
        ImGui.Checkbox("Live Update (every 2000 frames)", ref AutoRefresh);
        if (!AutoRefresh && ImGui.Button("Force Refresh Now"))
            RefreshData();

        ImGui.EndGroup();

        // Right group: cursor probe
        ImGui.BeginGroup();
        ImGui.TextColored(new System.Numerics.Vector4(1f, 1f, 0f, 1f), "Cursor Probe");
        ImGui.Text($"Tile X,Y  : {_hoverCoords.x}, {_hoverCoords.y}");
        ImGui.Text($"Tile ID   : {_hoverTileId}");
        ImGui.Separator();
        RenderHoverInfoTable();
        ImGui.EndGroup();
    }

    /// <summary>Renders all LogicDebugInfo fields for the hovered tile in a compact two-column table.</summary>
    private void RenderHoverInfoTable()
    {
        if (_hoverTileId < 0)
        {
            ImGui.TextDisabled("(hover over a tile)");
            return;
        }

        ImGuiTableFlags flags = ImGuiTableFlags.SizingFixedFit
                              | ImGuiTableFlags.BordersInnerV
                              | ImGuiTableFlags.ScrollY;

        if (!ImGui.BeginTable("LogicDebugInfoTable", 2, flags, new System.Numerics.Vector2(380f, 320f)))
            return;

        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableSetupColumn("Field", ImGuiTableColumnFlags.WidthFixed, 190f);
        ImGui.TableSetupColumn("Value", ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableHeadersRow();

        // Highlight the currently selected field
        RenderRow("gfx_layer", _hoverInfo.gfx_layer, LogicDebugField.gfx_layer);
        RenderRow("gfx_layer_file", _hoverInfo.gfx_layer_file, LogicDebugField.gfx_layer_file);
        RenderRow("gfx_layer_id", _hoverInfo.gfx_layer_id, LogicDebugField.gfx_layer_id);
        RenderRow("alpha_gfx_layer", _hoverInfo.alpha_gfx_layer, LogicDebugField.alpha_gfx_layer);
        RenderRow("construction_gfx_layer", _hoverInfo.construction_gfx_layer, LogicDebugField.construction_gfx_layer);
        RenderRow("pillar_gfx_layer", _hoverInfo.pillar_gfx_layer, LogicDebugField.pillar_gfx_layer);
        RenderRow("pillar_gfx_layer_file", _hoverInfo.pillar_gfx_layer_file, LogicDebugField.pillar_gfx_layer_file);
        RenderRow("pillar_gfx_layer_id", _hoverInfo.pillar_gfx_layer_id, LogicDebugField.pillar_gfx_layer_id);
        RenderRow("wall_gfx_layer", _hoverInfo.wall_gfx_layer, LogicDebugField.wall_gfx_layer);
        RenderRow("wall_gfx_layer_file", _hoverInfo.wall_gfx_layer_file, LogicDebugField.wall_gfx_layer_file);
        RenderRow("wall_gfx_layer_id", _hoverInfo.wall_gfx_layer_id, LogicDebugField.wall_gfx_layer_id);
        RenderRow("floating_layer", _hoverInfo.floating_layer, LogicDebugField.floating_layer);
        RenderRow("random_layer", _hoverInfo.random_layer, LogicDebugField.random_layer);
        RenderRow("logic_layer", _hoverInfo.logic_layer, LogicDebugField.logic_layer);
        RenderRow("logic2_layer", _hoverInfo.logic2_layer, LogicDebugField.logic2_layer);
        RenderRow("changed_layer", _hoverInfo.changed_layer, LogicDebugField.changed_layer);
        RenderRow("organism_layer", _hoverInfo.organism_layer, LogicDebugField.organism_layer);
        RenderRow("structure_layer", _hoverInfo.structure_layer, LogicDebugField.structure_layer);
        RenderRow("structure_was_layer", _hoverInfo.structure_was_layer, LogicDebugField.structure_was_layer);
        RenderRow("chimp_layer", _hoverInfo.chimp_layer, LogicDebugField.chimp_layer);
        RenderRow("fly_layer", _hoverInfo.fly_layer, LogicDebugField.fly_layer);
        RenderRow("height_layer", _hoverInfo.height_layer, LogicDebugField.height_layer);
        RenderRow("default_height_layer", _hoverInfo.default_height_layer, LogicDebugField.default_height_layer);
        RenderRow("wall_owner_layer", _hoverInfo.wall_owner_layer, LogicDebugField.wall_owner_layer);
        RenderRow("luminesence_layer", _hoverInfo.luminesence_layer, LogicDebugField.luminesence_layer);
        RenderRow("show_hi_layer", _hoverInfo.show_hi_layer, LogicDebugField.show_hi_layer);
        RenderRow("misc_display_layer", _hoverInfo.misc_display_layer, LogicDebugField.misc_display_layer);
        RenderRow("damage_layer", _hoverInfo.damage_layer, LogicDebugField.damage_layer);
        RenderRow("macro_layer", _hoverInfo.macro_layer, LogicDebugField.macro_layer);
        RenderRow("path_connection_layer", _hoverInfo.path_connection_layer, LogicDebugField.path_connection_layer);
        RenderRow("path_linkage_layer", _hoverInfo.path_linkage_layer, LogicDebugField.path_linkage_layer);
        RenderRow("occupancy_layer", _hoverInfo.occupancy_layer, LogicDebugField.occupancy_layer);
        RenderRow("certain_path_layer", _hoverInfo.certain_path_layer, LogicDebugField.certain_path_layer);
        RenderRow("walk_layer", _hoverInfo.walk_layer, LogicDebugField.walk_layer);
        RenderRow("ai_zone_layer", _hoverInfo.ai_zone_layer, LogicDebugField.ai_zone_layer);
        RenderRow("ai_info_layer", _hoverInfo.ai_info_layer, LogicDebugField.ai_info_layer);
        RenderRow("ai_danger_layer", _hoverInfo.ai_danger_layer, LogicDebugField.ai_danger_layer);
        RenderRow("ai_proximity_layer", _hoverInfo.ai_proximity_layer, LogicDebugField.ai_proximity_layer);
        RenderRow("town_dz_spread_id", _hoverInfo.town_dz_spread_id, LogicDebugField.town_dz_spread_id);
        RenderRow("town_null_connects", _hoverInfo.town_null_connects, LogicDebugField.town_null_connects);
        RenderRow("town_dz_spread_count", _hoverInfo.town_dz_spread_count, LogicDebugField.town_dz_spread_count);
        RenderRow("town_stone_value", _hoverInfo.town_stone_value, LogicDebugField.town_stone_value);
        RenderRow("town_structure", _hoverInfo.town_structure, LogicDebugField.town_structure);
        RenderRow("town_oasis", _hoverInfo.town_oasis, LogicDebugField.town_oasis);
        RenderRow("town_farm", _hoverInfo.town_farm, LogicDebugField.town_farm);
        RenderRow("town_iron", _hoverInfo.town_iron, LogicDebugField.town_iron);
        RenderRow("problem_build", _hoverInfo.problem_build, LogicDebugField.problem_build);
        RenderRow("aiv_block_zone", _hoverInfo.aiv_block_zone, LogicDebugField.aiv_block_zone);
        RenderRow("delay_layer", _hoverInfo.delay_layer, LogicDebugField.delay_layer);
        RenderRow("aiv_block_layer", _hoverInfo.aiv_block_layer, LogicDebugField.aiv_block_layer);
        RenderRow("mapOfset", _hoverInfo.mapOfset, LogicDebugField.mapOfset);

        ImGui.EndTable();
    }

    private void RenderRow(string label, int value, LogicDebugField field)
    {
        bool isSelected = SelectedField == field;

        ImGui.TableNextRow();

        if (isSelected)
            ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, ImGui.GetColorU32(new System.Numerics.Vector4(0.3f, 0.5f, 0.1f, 0.5f)));

        ImGui.TableNextColumn();
        if (isSelected)
            ImGui.TextColored(new System.Numerics.Vector4(0.5f, 1f, 0.3f, 1f), label);
        else
            ImGui.Text(label);

        ImGui.TableNextColumn();
        ImGui.Text($"{value}  (0x{value:X8})");
    }

    // Cursor probe
    private void ProbeCursor()
    {
        if (GamePlayerManagerAPI.Instance.CursorManager == null) return;

        int tid = (int)GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileId;
        UnmanagedVector2<ushort> pos = GameTileManagerAPI.Instance.GetTileVectorFromId(tid);
        int tx = pos.X;
        int ty = pos.Y;

        _hoverCoords = new Vector2Int(tx, ty);
        _hoverTileId = tid;

        if (tid >= 0 && tid < MAX_TILES)
        {
            try { _hoverInfo = EngineInterface.GetLayerDebug(tx, ty); }
            catch { }
        }
    }

    // Data refresh
    public void RefreshData()
    {
        Array.Clear(_colorCache, 0, _colorCache.Length);

        if (AutoScale)
            RecalculateRange();

        PopulateColorCache();

        // Trigger Unity Update
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (TilemapManager.instance != null)
            {
                TilemapManager.instance.triggerTMFullRefresh();
            }
        });
    }

    private void RecalculateRange()
    {
        float min = float.MaxValue;
        float max = float.MinValue;

        for (int y = 0; y < MAP_SIZE; y++)
        {
            for (int x = 0; x < MAP_SIZE; x++)
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (tileId < 0 || tileId >= MAX_TILES) continue;

                try
                {
                    float v = GetFieldValue(EngineInterface.GetLayerDebug(x, y), SelectedField);
                    if (v < min) min = v;
                    if (v > max) max = v;
                }
                catch { }
            }
        }

        if (min < max) { MinVal = min; MaxVal = max; }
    }

    private void PopulateColorCache()
    {
        for (int y = 0; y < MAP_SIZE; y++)
        {
            for (int x = 0; x < MAP_SIZE; x++)
            {
                int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
                if (tileId < 0 || tileId >= MAX_TILES) continue;

                try
                {
                    float v = GetFieldValue(EngineInterface.GetLayerDebug(x, y), SelectedField);
                    _colorCache[tileId] = GetHeatMapColor(v);
                }
                catch { }
            }
        }
    }

    public bool TryGetColorOverride(int x, int y, out Color color)
    {
        color = Color.white;
        if (!IsActive) return false;

        int tileId = GameTileManagerAPI.Instance.GetTileId(x, y);
        if (tileId >= 0 && tileId < MAX_TILES)
        {
            Color? c = _colorCache[tileId];
            if (c.HasValue) { color = c.Value; return true; }
        }
        return false;
    }

    // Helpers 
    private static float GetFieldValue(EngineInterface.LogicDebugInfo info, LogicDebugField field)
    {
        return field switch
        {
            LogicDebugField.gfx_layer => info.gfx_layer,
            LogicDebugField.gfx_layer_file => info.gfx_layer_file,
            LogicDebugField.gfx_layer_id => info.gfx_layer_id,
            LogicDebugField.alpha_gfx_layer => info.alpha_gfx_layer,
            LogicDebugField.construction_gfx_layer => info.construction_gfx_layer,
            LogicDebugField.pillar_gfx_layer => info.pillar_gfx_layer,
            LogicDebugField.pillar_gfx_layer_file => info.pillar_gfx_layer_file,
            LogicDebugField.pillar_gfx_layer_id => info.pillar_gfx_layer_id,
            LogicDebugField.wall_gfx_layer => info.wall_gfx_layer,
            LogicDebugField.wall_gfx_layer_file => info.wall_gfx_layer_file,
            LogicDebugField.wall_gfx_layer_id => info.wall_gfx_layer_id,
            LogicDebugField.floating_layer => info.floating_layer,
            LogicDebugField.random_layer => info.random_layer,
            LogicDebugField.logic_layer => info.logic_layer,
            LogicDebugField.logic2_layer => info.logic2_layer,
            LogicDebugField.changed_layer => info.changed_layer,
            LogicDebugField.organism_layer => info.organism_layer,
            LogicDebugField.structure_layer => info.structure_layer,
            LogicDebugField.structure_was_layer => info.structure_was_layer,
            LogicDebugField.chimp_layer => info.chimp_layer,
            LogicDebugField.fly_layer => info.fly_layer,
            LogicDebugField.height_layer => info.height_layer,
            LogicDebugField.default_height_layer => info.default_height_layer,
            LogicDebugField.wall_owner_layer => info.wall_owner_layer,
            LogicDebugField.luminesence_layer => info.luminesence_layer,
            LogicDebugField.show_hi_layer => info.show_hi_layer,
            LogicDebugField.misc_display_layer => info.misc_display_layer,
            LogicDebugField.damage_layer => info.damage_layer,
            LogicDebugField.macro_layer => info.macro_layer,
            LogicDebugField.path_connection_layer => info.path_connection_layer,
            LogicDebugField.path_linkage_layer => info.path_linkage_layer,
            LogicDebugField.occupancy_layer => info.occupancy_layer,
            LogicDebugField.certain_path_layer => info.certain_path_layer,
            LogicDebugField.walk_layer => info.walk_layer,
            LogicDebugField.ai_zone_layer => info.ai_zone_layer,
            LogicDebugField.ai_info_layer => info.ai_info_layer,
            LogicDebugField.ai_danger_layer => info.ai_danger_layer,
            LogicDebugField.ai_proximity_layer => info.ai_proximity_layer,
            LogicDebugField.town_dz_spread_id => info.town_dz_spread_id,
            LogicDebugField.town_null_connects => info.town_null_connects,
            LogicDebugField.town_dz_spread_count => info.town_dz_spread_count,
            LogicDebugField.town_stone_value => info.town_stone_value,
            LogicDebugField.town_structure => info.town_structure,
            LogicDebugField.town_oasis => info.town_oasis,
            LogicDebugField.town_farm => info.town_farm,
            LogicDebugField.town_iron => info.town_iron,
            LogicDebugField.problem_build => info.problem_build,
            LogicDebugField.aiv_block_zone => info.aiv_block_zone,
            LogicDebugField.delay_layer => info.delay_layer,
            LogicDebugField.aiv_block_layer => info.aiv_block_layer,
            LogicDebugField.mapOfset => info.mapOfset,
            _ => 0f
        };
    }

    private Color GetHeatMapColor(float value)
    {
        float t = Mathf.InverseLerp(MinVal, MaxVal, value);
        // Blue → Green → Red  (same palette as UnityTileGridVisualizer)
        return Color.HSVToRGB((1f - t) * 0.66f, 1f, 1f).ToAlpha(Alpha);
    }
}