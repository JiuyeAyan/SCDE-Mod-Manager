using ImGuiNET;
using SHCDESE.API;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.Network;
using SHCDESE.GameGlobals;
using SHCDESE.ImMenu.Visualizers;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using SHCDESE.Lua;
using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Security;
using UUIMGUI.Core;

namespace SHCDESE.DebugMenu.ImMenu;

[SuppressUnmanagedCodeSecurity]
public unsafe static class UserMenuRenderer
{
    public static void PresentCallbackLive(ref IntPtr IDXGISwapChain, ref uint SyncInterval, ref uint Flags)
    {
        try
        {
            if (ShowMapEditorTools)
            {
                DebugMenuManager.Instance._mapRegionSelector.Render();
                DebugMenuManager.Instance._massTileHeightEditor.Render();
                DebugMenuManager.Instance._tileInspectorTool.Render();
                DebugMenuManager.Instance._mirrorTool.Render();
                DebugMenuManager.Instance._tilePropertyMutatorTool.Render();
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during Live Present Callback");
        }
    }

    private static bool _visualizersInitialized = false;
    public static void PresentCallback(ref IntPtr IDXGISwapChain, ref uint SyncInterval, ref uint Flags)
    {
        try
        {
            if (!_visualizersInitialized)
            {
                _visualizersInitialized = true;
                UnityMainThreadDispatcher.EnqueueStatic(() =>
                {
                    if (DebugGridVisualizer.Instance == null)
                    {
                        // Create a hidden GameObject to host the MonoBehaviour
                        UnityEngine.GameObject go = new UnityEngine.GameObject("UnityTileGridVisualizer");
                        go.AddComponent<DebugGridVisualizer>();
                        UnityEngine.Object.DontDestroyOnLoad(go);
                    }
                    if (LogicDebugInfoVisualizer.Instance == null)
                    {
                        UnityEngine.GameObject go = new UnityEngine.GameObject("LogicDebugInfoVisualizer");
                        go.AddComponent<LogicDebugInfoVisualizer>();
                        UnityEngine.Object.DontDestroyOnLoad(go);
                    }
                    if (KeepProximityVisualizer.Instance == null)
                    {
                        UnityEngine.GameObject go = new UnityEngine.GameObject("KeepProximityVisualizer");
                        go.AddComponent<KeepProximityVisualizer>();
                        UnityEngine.Object.DontDestroyOnLoad(go);
                    }
                });
            }

            if (ImGui.BeginTabItem(LocalizationManager.Instance.GetString("SE_EDITOR_TITLE")))
            {
                if (ImGui.BeginTabBar(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_TITLE")))
                {
                    if (ImGui.BeginTabItem(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_GENERAL")))
                    {
                        GeneralComponent();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_ENTITYLIST")))
                    {
                        EntityListComponent();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_LUA")))
                    {
                        LUAComponent();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MANAGERS")))
                    {
                        ManagerComponent();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MAP_ARCHIVE_TOOLS")))
                    {
                        MapArchiveToolsComponent();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Debug Grid Visualizer"))
                    {
                        DebugGridVisualizer.Instance?.RenderUI();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Layer Grid Visualizer"))
                    {
                        LogicDebugInfoVisualizer.Instance?.RenderUI();
                        ImGui.EndTabItem();
                    }

                    if (ImGui.BeginTabItem("Keep Proximity"))
                    {
                        KeepProximityVisualizer.Instance?.RenderUI();
                        ImGui.EndTabItem();
                    }

                    ImGui.EndTabBar();
                }
                ImGui.EndTabItem();
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during Present Callback");
        }
    }

    private static void MapArchiveToolsComponent()
    {
        if (GameMapArchiveManagerAPI.Instance.GetMapArchive() == null)
        {
            ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MAS_SEPERATOR"));
            ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MAS_NOT_FOUND"));
            ImGui.Spacing();

            ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MAS_ADD_HELP"));

            ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 0.8f, 0.0f, 1.0f));
            ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MAS_WARN"));
            ImGui.PopStyleColor();
            ImGui.Spacing();

            if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_TOOLS_MAS_CREATE_NEW"), new Vector2(-1, 0))) // Full-width button
            {
                GameMapArchiveManagerAPI.Instance.CreateForActiveMapEditorMap();
            }
            return;
        }

        // Main view when an archive exists.
        MapArchive? archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();

        // -- Archive Information Section --
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_INFO"));
        if (ImGui.BeginTable("ArchiveInfo", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.BordersInnerV))
        {
            ImGui.TableNextColumn(); ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_AUTHOR"));
            ImGui.TableNextColumn(); ImGui.Text($": {archive?.Info?.Author ?? LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_NA")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_MAPNAME"));
            ImGui.TableNextColumn(); ImGui.Text($": {archive?.Info?.Name ?? LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_NA")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_DESC"));
            ImGui.TableNextColumn(); ImGui.Text($": {archive?.Info?.Description ?? LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_NA")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_VER"));
            ImGui.TableNextColumn(); ImGui.Text($": {archive?.Info?.Version ?? LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_NA")}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn(); ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_WEB"));
            ImGui.TableNextColumn(); ImGui.Text($": {archive?.Info?.Website ?? LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_NA")}");

            ImGui.EndTable();
        }

        ImGui.Spacing();
        ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_REMOVE_WARN"));
        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_ARCHIVE_REMOVE_BTN"), new Vector2(-1, 0)))
        {
            GameMapArchiveManagerAPI.Instance.RemoveActiveArchive();
        }

        // -- Trigger Management Section --
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_TRIG_TITLE"));
        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_TRIG_ACTIVE"), GameTriggerManager.Instance.GetAllTriggers().Length));
        ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_TRIG_REMOVE_WARN"));

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_TRIG_REMOVE_BTN"), new Vector2(-1, 0)))
        {
            GameTriggerManager.Instance.RemoveAllTriggers();
        }

        // -- Timer Management Section --
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_TIMER_TITLE"));
        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_TIMER_ACTIVE"), GameTimeManagerAPI.Instance.GetTimerEngine().GetTimersCount()));
        ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_TIMER_REMOVE_WARN"));

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_TIMER_REMOVE_BTN"), new Vector2(-1, 0)))
        {
            GameTimeManagerAPI.Instance.GetTimerEngine().RemoveAllTimers();
        }
    }

    private static void ManagerComponent()
    {
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_MANAGER_TITLE"));

        if (ImGui.BeginTable("ManagersTable", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
        {
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_MANAGER_COL_NAME"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_MANAGER_COL_ADDR"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_MANAGER_COL_COPY"));
            ImGui.TableHeadersRow();

            static void Row(string name, string address)
            {
                ImGui.TableNextRow();

                // Column 1: Name
                ImGui.TableSetColumnIndex(0);
                ImGui.Text(name);

                // Column 2: Address
                ImGui.TableSetColumnIndex(1);
                ImGui.Text($"0x{address}");

                // Column 3: Copy button
                ImGui.TableSetColumnIndex(2);
                if (ImGui.Button($"{LocalizationManager.Instance.GetString("SE_EDITOR_MANAGER_BTN_COPY")}##{name}"))
                    MinWinAPI.SetClipboardText(address);
            }
            Row("PlayerManager", GamePlayerManagerAPI.Instance.GetPlayerManager().ToString("X16"));
            Row("PlayerResources", GamePlayerManagerAPI.Instance.GetPlayerResources().GetArrayAddress().ToString("X16"));
            Row("BuildingManager", ((UInt64)GameBuildingManagerAPI.Instance.GetBuildingManager().Pointer).ToString("X16"));
            Row("TribeManager", ((UInt64)GameTribeManagerAPI.Instance.GetTribeManager().Pointer).ToString("X16"));
            Row("VegetationManager", ((UInt64)GameVegetationManagerAPI.Instance.GetVegetationManager().Pointer).ToString("X16"));
            Row("UnitManager", ((UInt64)GameUnitManagerAPI.Instance.GetUnitManager().Pointer).ToString("X16"));
            Row("ProjectileManager", ((UInt64)GameProjectileManagerAPI.Instance.GetProjectileManager().Pointer).ToString("X16"));
            Row("CursorManager", ((UInt64)GamePlayerManagerAPI.Instance.GetCursorManager().Pointer).ToString("X16"));
            Row("TileManager", GameTileManagerAPI.Instance.GetTileManager().ToString("X16"));
            Row("PitchManager", GamePitchManagerAPI.Instance.GetPitchArray().GetArrayAddress().ToString("X16"));
            Row("AICArray", GameAIManagerAPI.Instance.GetAICArray().GetArrayAddress().ToString("X16"));
            Row("PathConnectionsManager", GamePathingManagerAPI.Instance.GetPathConnectionArray().GetArrayAddress().ToString("X16"));
            Row("ChoreManager", GameGlobalsManager.Instance.ChoreManagerVA.ToString("X16"));
            Row("PathingManager", GameGlobalsManager.Instance.PathfindingContextVA.ToString("X16"));

            ImGui.EndTable();
        }
    }

    private static int tmpTileId = 0;
    private static int tmpTileUnknown = 0;
    private static int tmpTileWallHealthOrHeight = 0;
    private static int tmpWallTopDamaged = 0;
    private static int tmpTilePropertyFlag = 0;
    private static int tmpTileVegLookup = 0;
    private static int tmpTilePlayerOwnerId = 0;
    internal static bool ShowMapEditorTools = false;
    private static void GeneralComponent()
    {
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_SETTINGS_TITLE"));
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_SHOW_TOOLS"), ref ShowMapEditorTools);

        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_CURSOR_TITLE"));

        float mousePosX = 0, mousePosY = 0;
        float mouseTileX = 0, mouseTileY = 0;
        MainControls.instance.getMouseTileCentrePosition(ref mousePosX, ref mousePosY);
        MainControls.instance.getMouseMapTilePosition(ref mouseTileX, ref mouseTileY);

        if (ImGui.BeginTable("CursorView", 2, ImGuiTableFlags.BordersInnerV | ImGuiTableFlags.SizingFixedFit))
        {
            // Column 1: General Mouse Data
            ImGui.TableNextColumn();
            ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_MOUSE_POS"));
            ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_MOUSE_TILE"));

            ImGui.TableNextColumn();
            ImGui.Text($": {mousePosX:F4}, {mousePosY:F4}");
            ImGui.Text($": {mouseTileX:F4}, {mouseTileY:F4}");

            // Column 2: Game-Specific Cursor Data from the API
            if (GamePlayerManagerAPI.Instance.CursorManager != null)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_HOVER_BUILD_ID"));
                ImGui.TableNextColumn();
                ImGui.Text($": {GamePlayerManagerAPI.Instance.CursorManager->r_HoverOverBuildingId}");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_HOVER_UNIT_ID"));
                ImGui.TableNextColumn();
                ImGui.Text($": {GamePlayerManagerAPI.Instance.CursorManager->r_HoverOverUnitId}");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_HOVER_PLAYER_ID"));
                ImGui.TableNextColumn();
                int playerId = GetPlayerIdFromAvailable((int)GamePlayerManagerAPI.Instance.CursorManager->r_HoverOverUnitId, (int)GamePlayerManagerAPI.Instance.CursorManager->r_HoverOverBuildingId);
                ImGui.Text($": {playerId}");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_HOVER_BUILD_TILE_ID"));
                ImGui.TableNextColumn();
                ImGui.Text($": {GamePlayerManagerAPI.Instance.CursorManager->r_HoverOverBuildingTileId:X8}");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_MOUSE_TILEX_WORLD"));
                ImGui.TableNextColumn();
                ImGui.Text($": {GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileX} ({GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileX * 8})");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_MOUSE_TILEY_WORLD"));
                ImGui.TableNextColumn();
                ImGui.Text($": {GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileY} ({GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileY * 8})");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_MOUSE_TILE_ID"));
                ImGui.TableNextColumn();
                ImGui.Text($": {GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileId:X8}");
            }
            ImGui.EndTable();
        }


        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_LIVE_TILE_TITLE"));

        if ((GameTileManagerAPI.Instance.TileManager != null) && (mouseTileX > 0) && (mouseTileY > 0) && (mouseTileX < 800) && (mouseTileY < 800))
        {
            int tileId = (int)GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileId;
            if (ImGui.BeginTable("LiveTileView", 3, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_COL_PROP"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_COL_VAL"), ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_COL_MEM"), ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableHeadersRow();

                int value = 0;

                // Tile Height
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_HEIGHT"));
                ImGui.TableNextColumn();
                byte* tileHeightPtr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.HeightGrid[tileId]);
                value = *tileHeightPtr;
                if (ImGui.InputInt("##TileHeight", ref value))
                {
                    GameTileManagerAPI.Instance.TileManager.HeightGrid[tileId] = (byte)value;
                }
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(tileHeightPtr).ToString("X16")}");

                // Tile State (damage_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_STATE"));
                ImGui.TableNextColumn();
                value = GameTileManagerAPI.Instance.TileManager.DamageGrid[tileId];
                if (ImGui.InputInt("##TileState", ref value))
                {
                    GameTileManagerAPI.Instance.TileManager.DamageGrid[tileId] = (byte)value;
                }
                ImGui.TableNextColumn();

                // Tile Property Flag
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_PROP_FLAG"));
                ImGui.TableNextColumn();
                ImGui.InputInt("##TilePropFlag", ref GameTileManagerAPI.Instance.TileManager.LogicGrid[tileId], 0, 0, ImGuiInputTextFlags.CharsHexadecimal);
                ImGui.TableNextColumn();
                UInt32* tilePropPtr = (UInt32*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.LogicGrid[tileId]);
                ImGui.Text($"0x{new IntPtr(tilePropPtr).ToString("X16")}");

                // Tile Property Enum
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_PROP_ENUM"));
                ImGui.TableNextColumn();
                ImGui.Text($"{((TilePropertyFlag)GameTileManagerAPI.Instance.TileManager.LogicGrid[tileId]).ToString()}");
                ImGui.TableNextColumn();

                // Tile Vegetation ID
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_VEG_ID"));
                ImGui.TableNextColumn();
                UInt16* tileVegPtr = (UInt16*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.OrganismGrid[tileId]);
                int tileVegetationId = *tileVegPtr;
                ImGui.Text($"{tileVegetationId:X4}h ({tileVegetationId}d)");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(tileVegPtr).ToString("X16")}");

                // Tile Type
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_TYPE"));
                ImGui.TableNextColumn();
                byte* tileTypePtr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.Logic2Grid[tileId]);
                byte tileType = *tileTypePtr;
                ImGui.Text($"{tileType:X2}h ({(TileType)tileType})");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(tileTypePtr).ToString("X16")}");


                // PillarGFXGrid (pillar_gfx_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_PILLAR_GFX"));
                ImGui.TableNextColumn();
                int* unknownGrid1Ptr = (int*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.PillarGFXGrid[tileId]);
                ImGui.Text($"{*unknownGrid1Ptr:X8}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid1Ptr).ToString("X16")}");

                // UnknownGrid_0xA20D40 (byte, TODO)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_UNK_0xA20D40"));
                ImGui.TableNextColumn();
                byte* unknownGrid2Ptr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.UnknownGrid2[tileId]);
                ImGui.Text($"{*unknownGrid2Ptr:X4}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid2Ptr).ToString("X16")}");

                // ShowHiGrid (show_hi_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_SHOW_HI"));
                ImGui.TableNextColumn();
                byte* unknownGrid3Ptr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.ShowHiGrid[tileId]);
                ImGui.Text($"{*unknownGrid3Ptr:X4}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid3Ptr).ToString("X16")}");

                // MiscDisplayGrid (misc_display_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_MISC_DISPLAY"));
                ImGui.TableNextColumn();
                UInt16* unknownGrid4Ptr = (UInt16*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.MiscDisplayGrid[tileId]);
                ImGui.Text($"{*unknownGrid4Ptr:X8}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid4Ptr).ToString("X16")}");

                // PathConnectionGrid (path_connection_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_PATH_CONNECTION"));
                ImGui.TableNextColumn();
                UInt16* unknownGrid5Ptr = (UInt16*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.PathConnectionGrid[tileId]);
                ImGui.Text($"{*unknownGrid5Ptr}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid5Ptr).ToString("X16")}");

                // PathEdgeMaskGrid (path_linkage_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_PATH_EDGE_MASK"));
                ImGui.TableNextColumn();
                byte* pemgPtr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.PathEdgeMaskGrid[tileId]);
                ImGui.Text($"{*pemgPtr}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(pemgPtr).ToString("X16")}");

                // GatePathGrid
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_PATH_GATE"));
                ImGui.TableNextColumn();
                byte* pgpgPtr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.GatePathGrid[tileId]);
                ImGui.Text($"{*pgpgPtr}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(pgpgPtr).ToString("X16")}");

                // AIZoneGrid (ai_zone_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_AI_ZONE"));
                ImGui.TableNextColumn();
                byte* unknownGrid6Ptr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.AIZoneGrid[tileId]);
                ImGui.Text($"{*unknownGrid6Ptr:X4}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid6Ptr).ToString("X16")}");

                // OccupancyGrid (occupancy_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_OCCUPANCY"));
                ImGui.TableNextColumn();
                byte* unknownGrid7Ptr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.OccupancyGrid[tileId]);
                ImGui.Text($"{*unknownGrid7Ptr:X4}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid7Ptr).ToString("X16")}");

                // DelayGrid (delay_layer)
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_DELAY"));
                ImGui.TableNextColumn();
                byte* unknownGrid8Ptr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.DelayGrid[tileId]);
                ImGui.Text($"{*unknownGrid8Ptr:X4}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(unknownGrid8Ptr).ToString("X16")}");

                // TileDefaultHeightGrid
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_DEF_HEIGHT"));
                ImGui.TableNextColumn();
                byte* defaultHeightPtr = (byte*)Unsafe.AsPointer(ref GameTileManagerAPI.Instance.TileManager.DefaultHeightGrid[tileId]);
                ImGui.Text($"{*defaultHeightPtr:X2}");
                ImGui.TableNextColumn();
                ImGui.Text($"0x{new IntPtr(defaultHeightPtr).ToString("X16")}");

                // Building ID
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_BUILD_ID"));
                ImGui.TableNextColumn();
                int tileBuildingId = GameTileManagerAPI.Instance.TileManager.StructureGrid[tileId];
                ImGui.Text($"{tileBuildingId:X4}h ({tileBuildingId}d)");
                ImGui.TableNextColumn();

                // Unit ID
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_UNIT_ID"));
                ImGui.TableNextColumn();
                int tileUnitId = GameTileManagerAPI.Instance.TileManager.TileUnitIdGrid[tileId];
                ImGui.Text($"{tileUnitId:X4}h ({tileUnitId}d)");
                ImGui.TableNextColumn();

                // Tile Owner
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_OWNER_ID"));
                ImGui.TableNextColumn();
                ImGui.Text($"{(GameTileManagerAPI.Instance.TileManager.WallOwnerGrid[tileId] + 1):X2}");
                ImGui.TableNextColumn();

                // Tile AIVBlock
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_TILE_AIVBLOCK_ID"));
                ImGui.TableNextColumn();
                ImGui.Text($"{((byte)GameTileManagerAPI.Instance.TileManager.AIVBlockGrid[tileId]):X2}");
                ImGui.TableNextColumn();

                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_GEN_CURSOR_OUT"));
        }

        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_TITLE"));
        if (GameTileManagerAPI.Instance.TileManager != null)
        {
            if (ImGui.BeginTable("EditTileView", 2, ImGuiTableFlags.SizingFixedFit))
            {
                ImGui.TableNextColumn();
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_ID"), ref tmpTileId);
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_UNK_0xA20D40"), ref tmpTileUnknown);
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_HEIGHT"), ref tmpTileWallHealthOrHeight);
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_STATE"), ref tmpWallTopDamaged);
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_PROP"), ref tmpTilePropertyFlag);
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_VEG"), ref tmpTileVegLookup);
                ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_OWNER"), ref tmpTilePlayerOwnerId);

                ImGui.TableNextColumn();
                if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_EDIT_TILE_APPLY"), new Vector2(200, 200)))
                {
                    GameTileManagerAPI.Instance.TileManager.HeightGrid[tmpTileId] = (byte)tmpTileWallHealthOrHeight;
                    GameTileManagerAPI.Instance.TileManager.DamageGrid[tmpTileId] = (byte)tmpWallTopDamaged;
                    GameTileManagerAPI.Instance.TileManager.LogicGrid[tmpTileId] = tmpTilePropertyFlag;
                    GameTileManagerAPI.Instance.TileManager.OrganismGrid[tmpTileId] = (UInt16)tmpTileVegLookup;
                    GameTileManagerAPI.Instance.TileManager.WallOwnerGrid[tmpTileId] = (byte)tmpTilePlayerOwnerId;
                }
                ImGui.EndTable();
            }
        }
    }

    private static int GetPlayerIdFromAvailable(int unitId, int buildingId)
    {
        if (unitId != 0)
        {
            return GameUnitManagerAPI.Instance.GetOwner(unitId);
        }
        else if (buildingId != 0)
        {
            return GameBuildingManagerAPI.Instance.GetOwner(buildingId);
        }
        return 0; // Default to 0 if neither is available
    }

    private static int _debugSeed = 0;
    private static string _luaCode = "";
    private static void LUAComponent()
    {
        Vector2 available = ImGui.GetContentRegionAvail();
        bool luaStateExists = LuaManager.Instance.Lua != null;

        // --- LUA State Management ---
        ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_MANAGE_TITLE"));
        ImGui.Separator();

        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_STATE_LABEL"),
            (luaStateExists ? LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_EXISTS") : LocalizationManager.Instance.GetString("SE_EDITOR_COMMON_NOT_FOUND"))));

        // Disable the button if the Lua state already exists
        if (!luaStateExists)
        {
            if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_CREATE"), new Vector2(200, 0)))
            {
                LuaManager.Instance.TryInitState();
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_CREATE"), new Vector2(200, 0));
            ImGui.EndDisabled();
        }

        ImGui.SameLine();

        // Disable the button if the Lua state does not exist
        if (luaStateExists)
        {
            if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_CLOSE"), new Vector2(200, 0)))
            {
                try
                {
                    LuaManager.Instance.TryUnload();
                }
                catch (Exception ex)
                {
                    LogHelper.Error(ex, "Error");
                }
            }
        }
        else
        {
            ImGui.BeginDisabled();
            ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_CLOSE"), new Vector2(200, 0));
            ImGui.EndDisabled();
        }


        ImGui.Spacing();
        ImGui.Spacing();

        // --- LUA Script Control ---
        ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_SCRIPT_TITLE"));
        ImGui.Separator();

        // Assuming these require a Lua state to be active
        if (!luaStateExists) ImGui.BeginDisabled();

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_CALL_INIT"), new Vector2(200, 0)))
        {
            try
            {
                LuaManager.Instance.TryRunInitForce();
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Error");
            }
        }

        if (!luaStateExists) ImGui.EndDisabled();

        ImGui.Spacing();
        ImGui.Spacing();

        // --- Random Engine ---
        ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_RND_TITLE"));
        ImGui.Separator();

        ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_RND_SEED"), ref _debugSeed);
        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_RND_INIT"), new Vector2(250, 0)))
        {
            DeterministicRandom.Initialize(_debugSeed);
        }

        ImGui.Spacing();
        ImGui.Spacing();

        // --- LUA Code Execution ---
        ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_EXEC_TITLE"));
        ImGui.Separator();

        if (!luaStateExists) ImGui.BeginDisabled();

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_LUA_RUN"), new Vector2(100, 0)))
        {
            try
            {
                LogHelper.Information("Executing...");
                LuaManager.Instance.Lua?.DoString(_luaCode, "imgui");
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Error");
            }
        }

        if (!luaStateExists) ImGui.EndDisabled();

        ImGui.InputTextMultiline("##luaCode", ref _luaCode, 1024 * 64, new Vector2(available.X, available.Y - 30)); // Adjusted height
    }

    private static DateTime _lastUpdateTime = DateTime.MinValue;
    private static readonly TimeSpan _updateInterval = TimeSpan.FromMilliseconds(500);
    private static uint _totalUnitsCached = 0;
    private static void EntityListComponent()
    {
        // Refresh entity data only once every 500ms
        if ((DateTime.Now - _lastUpdateTime) > _updateInterval)
        {
            _totalUnitsCached = GameUnitManagerAPI.Instance._unitManager->r_TotalUnits;
            _lastUpdateTime = DateTime.Now;
        }

        if (_totalUnitsCached <= 0)
        {
            ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_NO_UNITS"));
            return;
        }
        ImGui.BeginChild("##EntityListScrollingRegion", new Vector2(0, 0));
        if (ImGui.BeginTable("entityTable", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY))
        {
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_ID"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_TYPE"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_POS_XY"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_POS_Z"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_PLAYER"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_HEALTH"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_ADDR"));
            ImGui.TableSetupColumn(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_COL_ACTION"));
            ImGui.TableHeadersRow();

            GameUnit* gameUnit;
            for (int i = 0; i < _totalUnitsCached; i++)
            {
                gameUnit = GameUnitManagerAPI.Instance._unitArray.GetValuePointer(i);
                if (gameUnit == null) continue;
                if (gameUnit->r_AliveState != AliveState.IsAlive) continue;

                ImGui.TableNextRow();

                ImGui.TableSetColumnIndex(0);
                ImGui.Text(i.ToString());

                ImGui.TableSetColumnIndex(1);
                ImGui.Text(gameUnit->r_UnitChimp.ToString());

                ImGui.TableSetColumnIndex(2);
                ImGui.Text($"{gameUnit->r_CurrentTilePositionX}, {gameUnit->r_CurrentTilePositionY}");

                ImGui.TableSetColumnIndex(3);
                ImGui.Text($"{gameUnit->r_HeightElevation}");

                ImGui.TableSetColumnIndex(4);
                ImGui.Text(gameUnit->r_ControllableForPlayerId.ToString());

                ImGui.TableSetColumnIndex(5);
                ImGui.Text($"{gameUnit->r_CurrentHealth} / {gameUnit->r_MaxHealth}");

                ImGui.TableSetColumnIndex(6);
                ImGui.Text($"{new IntPtr(gameUnit).ToString("X16")}");
                if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_BTN_COPY") + $"##{i}"))
                {
                    LogHelper.Debug($"Copy Addr button clicked for entity: {i}");
                    MinWinAPI.SetClipboardText(new IntPtr(gameUnit).ToString("X16"));
                }

                ImGui.TableSetColumnIndex(7);
                if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_BTN_DELETE") + $"##{i}"))
                {
                    LogHelper.Debug($"Delete button clicked for entity: {i}");
                    gameUnit->r_AliveState = AliveState.MarkedForDeletion;
                }

                // Right-click context menu
                if (ImGui.BeginPopupContextItem($"entityContextMenu_{i}"))
                {
                    if (ImGui.MenuItem(LocalizationManager.Instance.GetString("SE_EDITOR_ENT_CTX_DELETE")))
                    {
                        LogHelper.Information($"Delete context menu clicked for entity: {i}");
                        gameUnit->r_AliveState = AliveState.MarkedForDeletion;
                    }
                    ImGui.EndPopup();
                }
            }
            ImGui.EndTable();
        }
        ImGui.EndChild();
    }
}