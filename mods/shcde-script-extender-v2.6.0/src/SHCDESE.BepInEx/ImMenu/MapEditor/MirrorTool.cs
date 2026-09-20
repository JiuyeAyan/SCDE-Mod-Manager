using ImGuiNET;
using SHCDESE.API;
using SHCDESE.DebugMenu;
using SHCDESE.Logging;
using System;
using UUIMGUI.Core;

namespace SHCDESE.ImMenu.MapEditor;

public class MirrorTool
{
    public bool VerticalMirrorEnabled = false;
    public bool HorizontalMirrorsEnabled = false;
    public bool TileTypeMirrorEnabled = false;
    public bool TileHeightMirrorEnabled = false;
    public bool WallMirrorEnabled = false;
    public bool BuildingMirrorEnabled = false;
    public bool UnitMirrorEnabled = false;
    public bool VegetationMirrorEnabled = false;
    public bool AnimalMirrorEnabled = false;
    public bool DeleteMirrorEnabled = false;

    /// <summary>
    /// Renders the UI for the Mirror Tool.
    /// </summary>
    public unsafe void Render()
    {
        ImGui.Begin(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_TITLE"));

        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_OPTIONS"));
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_VERT"), ref VerticalMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_HORZ"), ref HorizontalMirrorsEnabled);

        ImGui.BeginGroup();
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_ENABLED_FOR"));
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_TYPE"), ref TileTypeMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_HEIGHT"), ref TileHeightMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_WALLS"), ref WallMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_BLD"), ref BuildingMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_UNITS"), ref UnitMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_VEG"), ref VegetationMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_ANIMALS"), ref AnimalMirrorEnabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MIRROR_DELETE"), ref DeleteMirrorEnabled);
        ImGui.EndGroup();

        ImGui.End();
    }

    /// <summary>
    /// Resets misc data
    /// </summary>
    public void Reset()
    {


    }

    /// <summary>
    /// A generic helper that calculates mirrored positions and executes a provided action for each.
    /// </summary>
    /// <param name="centerTileId">The original Tile ID from the game hook.</param>
    /// <param name="centerTileY">The original Tile Y-coordinate from the game hook.</param>
    /// <param name="mirroredAction">An action to be executed for each calculated mirror point. It receives the mirrored Tile ID and the mirrored Y-coordinate.</param>
    public static void ExecuteMirrorAction(int centerTileId, int centerTileY, Action<int, int, int> mirroredAction)
    {
        GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
        int playableMapSize = gtm.GetCurrentMapSize();
        int playableAreaMin = (GameTileManagerAPI.MAX_HEIGHT / 2) - (playableMapSize / 2);

        // Accurately determine the original absolute (X, Y) of the brush center.
        int originalX = gtm.GetTileXFromTileIdAndY(centerTileId, centerTileY);
        int originalY = centerTileY;

        // Safety Check: If coordinate calculation fails or the click is outside the playable area, do not mirror.
        if (originalX == -1 ||
            originalX < playableAreaMin || originalX >= (playableAreaMin + playableMapSize) ||
            originalY < playableAreaMin || originalY >= (playableAreaMin + playableMapSize))
        {
            return;
        }

        bool verticalMirror = DebugMenuManager.Instance._mirrorTool.VerticalMirrorEnabled;
        bool horizontalMirror = DebugMenuManager.Instance._mirrorTool.HorizontalMirrorsEnabled;

        // --- Horizontal Mirror ---
        if (horizontalMirror)
        {
            int relativeX = originalX - playableAreaMin;
            int mirroredRelativeX = (playableMapSize - 1) - relativeX;
            int mirroredAbsoluteX = mirroredRelativeX + playableAreaMin;

            int mirroredTileId = gtm.GetTileId(mirroredAbsoluteX, originalY);

            // Execute the provided action with the mirrored coordinates.
            mirroredAction(mirroredTileId, mirroredAbsoluteX, originalY);
        }

        // --- Vertical Mirror ---
        if (verticalMirror)
        {
            int relativeY = originalY - playableAreaMin;
            int mirroredRelativeY = (playableMapSize - 1) - relativeY;
            int mirroredAbsoluteY = mirroredRelativeY + playableAreaMin;

            int mirroredTileId = gtm.GetTileId(originalX, mirroredAbsoluteY);

            mirroredAction(mirroredTileId, originalX, mirroredAbsoluteY);
        }

        // --- Diagonal Mirror (if both are active) ---
        if (horizontalMirror && verticalMirror)
        {
            int relativeX = originalX - playableAreaMin;
            int mirroredRelativeX = (playableMapSize - 1) - relativeX;
            int mirroredAbsoluteX = mirroredRelativeX + playableAreaMin;

            int relativeY = originalY - playableAreaMin;
            int mirroredRelativeY = (playableMapSize - 1) - relativeY;
            int mirroredAbsoluteY = mirroredRelativeY + playableAreaMin;

            int mirroredTileId = gtm.GetTileId(mirroredAbsoluteX, mirroredAbsoluteY);

            mirroredAction(mirroredTileId, mirroredAbsoluteX, mirroredAbsoluteY);
        }
    }

    /// <summary>
    /// A generic helper that calculates mirrored positions for a line or rectangle and executes a provided action for each.
    /// </summary>
    /// <param name="beginX">The starting X-coordinate of the line.</param>
    /// <param name="beginY">The starting Y-coordinate of the line.</param>
    /// <param name="endX">The ending X-coordinate of the line.</param>
    /// <param name="endY">The ending Y-coordinate of the line.</param>
    /// <param name="mirroredAction">An action to be executed for each calculated mirror. It receives the mirrored begin and end coordinates.</param>
    public static void ExecuteMirrorActionForLine(int beginX, int beginY, int endX, int endY, Action<int, int, int, int> mirroredAction)
    {
        GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
        int playableMapSize = gtm.GetCurrentMapSize();
        const int absoluteMapSize = 800;
        int playableAreaMin = (absoluteMapSize / 2) - (playableMapSize / 2);

        // Safety Check: Ensure the entire original drag is within the playable area.
        if (beginX < playableAreaMin || beginX >= (playableAreaMin + playableMapSize) ||
            beginY < playableAreaMin || beginY >= (playableAreaMin + playableMapSize) ||
            endX < playableAreaMin || endX >= (playableAreaMin + playableMapSize) ||
            endY < playableAreaMin || endY >= (playableAreaMin + playableMapSize))
        {
            return;
        }

        bool verticalMirror = DebugMenuManager.Instance._mirrorTool.VerticalMirrorEnabled;
        bool horizontalMirror = DebugMenuManager.Instance._mirrorTool.HorizontalMirrorsEnabled;

        // --- Horizontal Mirror ---
        if (horizontalMirror)
        {
            int mBeginX = (playableMapSize - 1) - (beginX - playableAreaMin) + playableAreaMin;
            int mEndX = (playableMapSize - 1) - (endX - playableAreaMin) + playableAreaMin;
            mirroredAction(mBeginX, beginY, mEndX, endY);
        }

        // --- Vertical Mirror ---
        if (verticalMirror)
        {
            int mBeginY = (playableMapSize - 1) - (beginY - playableAreaMin) + playableAreaMin;
            int mEndY = (playableMapSize - 1) - (endY - playableAreaMin) + playableAreaMin;
            mirroredAction(beginX, mBeginY, endX, mEndY);
        }

        // --- Diagonal Mirror (if both are active) ---
        if (horizontalMirror && verticalMirror)
        {
            int mBeginX = (playableMapSize - 1) - (beginX - playableAreaMin) + playableAreaMin;
            int mEndX = (playableMapSize - 1) - (endX - playableAreaMin) + playableAreaMin;
            int mBeginY = (playableMapSize - 1) - (beginY - playableAreaMin) + playableAreaMin;
            int mEndY = (playableMapSize - 1) - (endY - playableAreaMin) + playableAreaMin;
            mirroredAction(mBeginX, mBeginY, mEndX, mEndY);
        }
    }
}