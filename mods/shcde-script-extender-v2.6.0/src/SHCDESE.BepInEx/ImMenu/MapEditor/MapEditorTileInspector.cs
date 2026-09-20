using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Logging;
using UUIMGUI.Core;

namespace SHCDESE.ImMenu.MapEditor;

/// <summary>
/// A tool for selecting a single tile on the map to inspect and edit all of its properties.
/// Changes are staged and only applied when the user clicks a "Save" button.
/// </summary>
public class TileInspectorTool
{
    private int? _selectedTileId;

    // -----------------------------------------------------------------------
    // Staging area for all editable tile properties.
    // All fields are 'int' for ImGui InputInt compatibility.
    // Clamp ranges match the underlying memory type from GameTileManagerView.
    //
    // Type reference (from GameTileManagerView):
    //   byte    -> clamp [0, 255]
    //   UInt16  -> clamp [0, 65535]
    //   Int32   -> full int range (no clamp)
    // -----------------------------------------------------------------------
    private struct StagedTileProperties
    {
        // --- byte grids ---
        public int Height;               // HeightGrid           (byte)  aka height_layer
        public int DefaultHeight;        // DefaultHeightGrid    (byte)  aka default_height_layer
        public int DamageState;          // DamageGrid           (byte)  aka damage_layer
        public int WallOwner;            // WallOwnerGrid        (byte)  aka wall_owner_layer
        public int TileType;             // Logic2Grid           (byte)  aka logic2_layer
        public int UnknownGrid_0xA20D40; // UnknownGrid2         (byte)  offset 0xA20D40 (TODO)
        public int ShowHi;               // ShowHiGrid           (byte)  aka show_hi_layer
        public int AIZone;               // AIZoneGrid           (byte)  aka ai_zone_layer
        public int OccupancyMask;        // OccupancyGrid        (byte)  aka occupancy_layer  (CompactPlayerBitMask)
        public int Delay;                // DelayGrid            (byte)  aka delay_layer
        public int Luminescence;         // LuminesenceGrid      (byte)  aka luminesence_layer
        public int StructureWas;         // StructureWasGrid     (byte)  aka structure_was_layer
        public int AIDanger;             // AIDangerGrid         (byte)  aka ai_danger_layer
        public int AIVBlock;             // AIVBlockGrid         (byte)  aka aiv_block_layer   (CompactPlayerBitMask)
        public int GatePath;             // GatePathGrid         (byte)  aka gate_path_layer
        public int UnknownGrid_0xD30080; // UnknownGrid_0xD30080 (byte)  (TODO)
        public int UnknownGrid_0x134E700;// UnknownGrid_0x134E700(byte)  (TODO)
        public int UnknownGrid_0x13EB140;// UnknownGrid_0x13EB140(byte)  (TODO: unit pathmarker decay)

        // --- UInt16 grids ---
        public int OrganismId;           // OrganismGrid         (UInt16) aka organism_layer
        public int StructureId;          // StructureGrid        (UInt16) aka structure_layer
        public int UnitId;               // TileUnitIdGrid       (UInt16)
        public int MiscDisplay;          // MiscDisplayGrid      (UInt16) aka misc_display_layer
        public int PathConnection;       // PathConnectionGrid   (UInt16) aka path_connection_layer
        public int RandomNoise;          // TileRandomNoiseGrid  (UInt16) aka random_layer
        public int UnknownGrid_0x75EF80; // UnknownGrid_0x75EF80 (Int16)  (TODO)
        public int MoatWorkTaskIndexGrid;// UnknownGrid_0x1EA23F0(UInt16) (TODO)

        // --- Int16 grids ---
        public int MacroLayer;           // MacroGrid            (Int16)  aka macro_layer
        public int FlyLayer;             // FlyGrid              (Int16)  aka fly_layer

        // --- Int32 grids ---
        public int LogicFlags;           // LogicGrid            (Int32)  aka logic_layer (TilePropertyFlag)
        public int PillarGFX;            // PillarGFXGrid        (Int32)  aka pillar_gfx_layer (dynamic)
        public int GFX;                  // GFXGrid              (Int32)  aka gfx_layer
        public int AlphaGFX;             // AlphaGFXGrid         (Int32)  aka alpha_gfx_layer
        public int ConstructionGFX;      // ConstructionGrid     (Int32)  aka construction_gfx_layer
        public int WallGFX;              // WallGFXGrid          (Int32)  aka wall_gfx_layer
        public int UnknownGrid_0x1D68F70;// UnknownGrid_0x1D68F70(Int32)  (TODO)
    }

    private StagedTileProperties _staged;

    public TileInspectorTool() { }

    private bool EnableFreeBuild = false;
    public unsafe void Render()
    {
        ImGui.Begin(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_TITLE"));

        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_ENABLE_FREEBUILD"), ref EnableFreeBuild);
        if (EnableFreeBuild)
        {
            GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride = true;
            GameTileManagerAPI.Instance.TileManager.PlacementBlockedOverrideValue = false;
        }
        else
        {
            GameTileManagerAPI.Instance.TileManager.UsePlacementBlockedOverride = false;
        }

        // --- Selection Input ---
        ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_HELP"));
        if (ImGui.IsKeyDown(ImGuiKey.LeftAlt) && ImGui.IsMouseClicked(ImGuiMouseButton.Right))
        {
            int? hoveredTileId = (GamePlayerManagerAPI.Instance.CursorManager != null)
                ? (int)GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileId
                : null;

            if (hoveredTileId.HasValue)
                SelectTile(hoveredTileId.Value);
        }

        ImGui.Separator();

        if (!_selectedTileId.HasValue)
        {
            ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_NO_SEL"));
            ImGui.End();
            return;
        }

        int tileId = _selectedTileId.Value;
        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_SEL_ID"), tileId));
        ImGui.Spacing();

        // -----------------------------------------------------------------------
        // byte properties  [0, 255]
        // -----------------------------------------------------------------------
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_SEC_BYTE"));

        ByteField("SE_EDITOR_INSPECTOR_HEIGHT", ref _staged.Height);
        ByteField("SE_EDITOR_INSPECTOR_DEF_HEIGHT", ref _staged.DefaultHeight, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_DAMAGE", ref _staged.DamageState, showHex: true);

        ByteField("SE_EDITOR_INSPECTOR_WALL_OWNER", ref _staged.WallOwner);
        ImGui.SameLine();
        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_PLAYER_ARROW"), _staged.WallOwner + 1));

        ByteField("SE_EDITOR_INSPECTOR_TILETYPE", ref _staged.TileType, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_SHOW_HI", ref _staged.ShowHi, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_AI_ZONE", ref _staged.AIZone, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_OCCUPANCY", ref _staged.OccupancyMask, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_DELAY", ref _staged.Delay, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_LUMINESCENCE", ref _staged.Luminescence, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_STRUCTURE_WAS", ref _staged.StructureWas, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_AI_DANGER", ref _staged.AIDanger, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_AIV_BLOCK", ref _staged.AIVBlock, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_GATE_PATH", ref _staged.GatePath, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_UNK_0xA20D40", ref _staged.UnknownGrid_0xA20D40, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_UNK_0xD30080", ref _staged.UnknownGrid_0xD30080, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_UNK_0x134E700", ref _staged.UnknownGrid_0x134E700, showHex: true);
        ByteField("SE_EDITOR_INSPECTOR_UNK_0x13EB140", ref _staged.UnknownGrid_0x13EB140, showHex: true);

        // -----------------------------------------------------------------------
        // UInt16 properties  [0, 65535]
        // -----------------------------------------------------------------------
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_SEC_UINT16"));

        UInt16Field("SE_EDITOR_INSPECTOR_ORGANISM", ref _staged.OrganismId);
        UInt16Field("SE_EDITOR_INSPECTOR_STRUCTURE", ref _staged.StructureId);
        UInt16Field("SE_EDITOR_INSPECTOR_UNIT_ID", ref _staged.UnitId);
        UInt16Field("SE_EDITOR_INSPECTOR_MISC_DISPLAY", ref _staged.MiscDisplay);
        UInt16Field("SE_EDITOR_INSPECTOR_PATH_CONNECTION", ref _staged.PathConnection);
        UInt16Field("SE_EDITOR_INSPECTOR_RANDOM_NOISE", ref _staged.RandomNoise);
        UInt16Field("SE_EDITOR_INSPECTOR_UNK_0x75EF80", ref _staged.UnknownGrid_0x75EF80);
        UInt16Field("SE_EDITOR_INSPECTOR_UNK_0x1EA23F0", ref _staged.MoatWorkTaskIndexGrid);

        // -----------------------------------------------------------------------
        // Int16 properties (signed, stored as int for ImGui)
        // -----------------------------------------------------------------------
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_SEC_INT16"));

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_MACRO"), ref _staged.MacroLayer);
        ImGui.SameLine(); ImGui.Text($"0x{(short)_staged.MacroLayer:X4}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_FLY"), ref _staged.FlyLayer);
        ImGui.SameLine(); ImGui.Text($"0x{(short)_staged.FlyLayer:X4}");

        // -----------------------------------------------------------------------
        // Int32 properties  (full range)
        // -----------------------------------------------------------------------
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_SEC_INT32"));

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_LOGIC_FLAGS"), ref _staged.LogicFlags);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.LogicFlags:X8}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_PILLAR_GFX"), ref _staged.PillarGFX);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.PillarGFX:X8}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_GFX"), ref _staged.GFX);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.GFX:X8}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_ALPHA_GFX"), ref _staged.AlphaGFX);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.AlphaGFX:X8}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_CONSTRUCTION_GFX"), ref _staged.ConstructionGFX);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.ConstructionGFX:X8}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_WALL_GFX"), ref _staged.WallGFX);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.WallGFX:X8}");

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_UNK_0x1D68F70"), ref _staged.UnknownGrid_0x1D68F70);
        ImGui.SameLine(); ImGui.Text($"0x{_staged.UnknownGrid_0x1D68F70:X8}");

        // -----------------------------------------------------------------------
        // Action Buttons
        // -----------------------------------------------------------------------
        ImGui.Separator();
        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_BTN_SAVE"))) SaveChanges();
        ImGui.SameLine();
        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_INSPECTOR_BTN_READ"))) SelectTile(tileId);

        ImGui.End();
    }

    // -----------------------------------------------------------------------
    // Helpers to reduce boilerplate for clamped InputInt fields
    // -----------------------------------------------------------------------

    private void ByteField(string locKey, ref int value, bool showHex = false)
    {
        ImGui.InputInt(LocalizationManager.Instance.GetString(locKey), ref value);
        value = MathUtil.Clamp(value, byte.MinValue, byte.MaxValue);
        if (showHex) { ImGui.SameLine(); ImGui.Text($"0x{value:X2}"); }
    }

    private void UInt16Field(string locKey, ref int value)
    {
        ImGui.InputInt(LocalizationManager.Instance.GetString(locKey), ref value);
        value = MathUtil.Clamp(value, ushort.MinValue, ushort.MaxValue);
        ImGui.SameLine(); ImGui.Text($"0x{value:X4}");
    }

    // -----------------------------------------------------------------------
    // Read tile data from game memory into the staging struct.
    // -----------------------------------------------------------------------
    private unsafe void SelectTile(int tileId)
    {
        _selectedTileId = tileId;
        GameTileManagerView tm = GameTileManagerAPI.Instance.TileManager;

        // byte grids
        _staged.Height = tm.HeightGrid[tileId];
        _staged.DefaultHeight = tm.DefaultHeightGrid[tileId];
        _staged.DamageState = tm.DamageGrid[tileId];
        _staged.WallOwner = tm.WallOwnerGrid[tileId];
        _staged.TileType = tm.Logic2Grid[tileId];
        _staged.ShowHi = tm.ShowHiGrid[tileId];
        _staged.AIZone = tm.AIZoneGrid[tileId];
        _staged.OccupancyMask = (int)tm.OccupancyGrid[tileId];
        _staged.Delay = tm.DelayGrid[tileId];
        _staged.Luminescence = tm.LuminesenceGrid[tileId];
        _staged.StructureWas = tm.StructureWasGrid[tileId];
        _staged.AIDanger = tm.AIDangerGrid[tileId];
        _staged.AIVBlock = (int)tm.AIVBlockGrid[tileId];
        _staged.GatePath = tm.GatePathGrid[tileId];
        _staged.UnknownGrid_0xA20D40 = tm.UnknownGrid2[tileId];
        _staged.UnknownGrid_0xD30080 = tm.UnknownGrid_0xD30080[tileId];
        _staged.UnknownGrid_0x134E700 = tm.UnknownGrid_0x134E700[tileId];
        _staged.UnknownGrid_0x13EB140 = tm.UnknownGrid_0x13EB140[tileId];

        // UInt16 grids
        _staged.OrganismId = tm.OrganismGrid[tileId];
        _staged.StructureId = tm.StructureGrid[tileId];
        _staged.UnitId = tm.TileUnitIdGrid[tileId];
        _staged.MiscDisplay = tm.MiscDisplayGrid[tileId];
        _staged.PathConnection = tm.PathConnectionGrid[tileId];
        _staged.RandomNoise = tm.TileRandomNoiseGrid[tileId];
        _staged.UnknownGrid_0x75EF80 = tm.UnknownGrid_0x75EF80[tileId];
        _staged.MoatWorkTaskIndexGrid = tm.MoatWorkTaskIndexGrid[tileId];

        // Int16 grids
        _staged.MacroLayer = tm.MacroGrid[tileId];
        _staged.FlyLayer = tm.FlyGrid[tileId];

        // Int32 grids
        _staged.LogicFlags = tm.LogicGrid[tileId];
        _staged.PillarGFX = tm.PillarGFXGrid[tileId];
        _staged.GFX = tm.GFXGrid[tileId];
        _staged.AlphaGFX = tm.AlphaGFXGrid[tileId];
        _staged.ConstructionGFX = tm.ConstructionGrid[tileId];
        _staged.WallGFX = tm.WallGFXGrid[tileId];
        _staged.UnknownGrid_0x1D68F70 = tm.UnknownGrid_0x1D68F70[tileId];

        LogHelper.Information($"Selected tile {tileId} for inspection.");
    }

    // -----------------------------------------------------------------------
    // Write all staged values back to game memory with correct type casts.
    // -----------------------------------------------------------------------
    private void SaveChanges()
    {
        if (!_selectedTileId.HasValue) return;
        int tileId = _selectedTileId.Value;
        GameTileManagerView tm = GameTileManagerAPI.Instance.TileManager;

        LogHelper.Information($"Saving changes for tile {tileId}...");

        // byte grids
        tm.HeightGrid[tileId] = (byte)_staged.Height;
        tm.DefaultHeightGrid[tileId] = (byte)_staged.DefaultHeight;
        tm.DamageGrid[tileId] = (byte)_staged.DamageState;
        tm.WallOwnerGrid[tileId] = (byte)_staged.WallOwner;
        tm.Logic2Grid[tileId] = (byte)_staged.TileType;
        tm.ShowHiGrid[tileId] = (byte)_staged.ShowHi;
        tm.AIZoneGrid[tileId] = (byte)_staged.AIZone;
        tm.OccupancyGrid[tileId] = (Interop.Enums.CompactPlayerBitMask)_staged.OccupancyMask;
        tm.DelayGrid[tileId] = (byte)_staged.Delay;
        tm.LuminesenceGrid[tileId] = (byte)_staged.Luminescence;
        tm.StructureWasGrid[tileId] = (byte)_staged.StructureWas;
        tm.AIDangerGrid[tileId] = (byte)_staged.AIDanger;
        tm.AIVBlockGrid[tileId] = (Interop.Enums.CompactPlayerBitMask)_staged.AIVBlock;
        tm.GatePathGrid[tileId] = (byte)_staged.GatePath;
        tm.UnknownGrid2[tileId] = (byte)_staged.UnknownGrid_0xA20D40;
        tm.UnknownGrid_0xD30080[tileId] = (byte)_staged.UnknownGrid_0xD30080;
        tm.UnknownGrid_0x134E700[tileId] = (byte)_staged.UnknownGrid_0x134E700;
        tm.UnknownGrid_0x13EB140[tileId] = (byte)_staged.UnknownGrid_0x13EB140;

        // UInt16 grids
        tm.OrganismGrid[tileId] = (ushort)_staged.OrganismId;
        tm.StructureGrid[tileId] = (ushort)_staged.StructureId;
        tm.TileUnitIdGrid[tileId] = (ushort)_staged.UnitId;
        tm.MiscDisplayGrid[tileId] = (ushort)_staged.MiscDisplay;
        tm.PathConnectionGrid[tileId] = (ushort)_staged.PathConnection;
        tm.TileRandomNoiseGrid[tileId] = (ushort)_staged.RandomNoise;
        tm.UnknownGrid_0x75EF80[tileId] = (short)_staged.UnknownGrid_0x75EF80;
        tm.MoatWorkTaskIndexGrid[tileId] = (ushort)_staged.MoatWorkTaskIndexGrid;

        // Int16 grids
        tm.MacroGrid[tileId] = (short)_staged.MacroLayer;
        tm.FlyGrid[tileId] = (short)_staged.FlyLayer;

        // Int32 grids
        tm.LogicGrid[tileId] = _staged.LogicFlags;
        tm.PillarGFXGrid[tileId] = _staged.PillarGFX;
        tm.GFXGrid[tileId] = _staged.GFX;
        tm.AlphaGFXGrid[tileId] = _staged.AlphaGFX;
        tm.ConstructionGrid[tileId] = _staged.ConstructionGFX;
        tm.WallGFXGrid[tileId] = _staged.WallGFX;
        tm.UnknownGrid_0x1D68F70[tileId] = _staged.UnknownGrid_0x1D68F70;

        LogHelper.Information("Save complete.");

        // Re-read to confirm the write was successful.
        SelectTile(tileId);
    }
}