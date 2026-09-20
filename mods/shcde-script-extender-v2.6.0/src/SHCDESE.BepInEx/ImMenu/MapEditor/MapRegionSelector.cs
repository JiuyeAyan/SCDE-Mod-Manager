using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Interop;
using System.Numerics;
using UUIMGUI.Core;

namespace SHCDESE.ImMenu.MapEditor;


/// <summary>
/// A reusable ImGui component to select a rectangular region on a tile-based map.
/// It captures two points (Begin and End) using Left-Alt + Left-Click.
/// </summary>
public class MapRegionSelector
{
    public Vector2? BeginPoint { get; private set; }
    public Vector2? EndPoint { get; private set; }

    private bool _isSelectingFirstPoint = true;
    private const int MAP_WIDTH = 800; // As per the tile array size
    private const int MAP_HEIGHT = 800;

    /// <summary>
    /// Renders the UI for the region selector and handles the input logic.
    /// This should be called every frame within your ImGui rendering loop.
    /// </summary>
    public unsafe void Render()
    {
        ImGui.Begin(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_TITLE"));

        // Display the currently selected coordinates
        string notSet = LocalizationManager.Instance.GetString("SE_EDITOR_REGION_NOT_SET");
        string beginText = BeginPoint.HasValue ? $"X: {BeginPoint.Value.X}, Y: {BeginPoint.Value.Y}" : notSet;
        string endText = EndPoint.HasValue ? $"X: {EndPoint.Value.X}, Y: {EndPoint.Value.Y}" : notSet;

        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_BEGIN"), beginText));
        ImGui.Text(string.Format(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_END"), endText));

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_BTN_COPY")))
        {
            MinWinAPI.SetClipboardText($"RectangularArea2D({(BeginPoint.HasValue ? BeginPoint.Value.X : 0)}, {(BeginPoint.HasValue ? BeginPoint.Value.Y : 0)})");
        }

        ImGui.Separator();

        // Instructions
        ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_HELP_TITLE"));
        ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_HELP_1"));
        ImGui.TextWrapped(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_HELP_2"));

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_REGION_BTN_RESET")))
        {
            Reset();
        }

        // --- Input Handling Logic ---
        // This logic captures the mouse clicks with the modifier key.
        // For a real-world scenario, you might need a global mouse hook if the game window isn't focused.
        if (ImGui.IsKeyDown(ImGuiKey.LeftAlt) && ImGui.IsMouseClicked(ImGuiMouseButton.Left))
        {
            // IMPORTANT: This is a placeholder for getting the mouse position
            // in terms of game tile coordinates. You would need to implement
            // this logic yourself, likely by reading the game's camera/cursor memory.
            Vector2? currentTileCoords = new Vector2(GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileX, GamePlayerManagerAPI.Instance.CursorManager->r_MouseTileY);

            if (currentTileCoords.HasValue)
            {
                if (_isSelectingFirstPoint)
                {
                    BeginPoint = currentTileCoords;
                    EndPoint = null; // Reset end point when starting a new selection
                    _isSelectingFirstPoint = false;
                }
                else
                {
                    EndPoint = currentTileCoords;
                    _isSelectingFirstPoint = true; // Reset for the next selection cycle
                }
            }
        }
        ImGui.End();
    }

    /// <summary>
    /// Resets the selection points.
    /// </summary>
    public void Reset()
    {
        BeginPoint = null;
        EndPoint = null;
        _isSelectingFirstPoint = true;
    }
}