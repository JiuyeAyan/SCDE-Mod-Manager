using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Detours;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using UUIMGUI.Core;

namespace SHCDESE.ImMenu.MapEditor;

// ToDo:
// - Option: With Rim (Makes it so that it spawns shc-friendly impass rock walls at the edges of the current selection)
// ^ This is mostly a TilePropertyFlag option I think.
public class MassTileHeightEditor
{
    public enum ShapeType { Rectangle, Circle }

    private readonly MapRegionSelector _regionSelector;
    private readonly Stack<MapEditorUndoAction> _undoStack = new();
    private readonly Random _random = new();

    // --- UI State for Heights and Shapes ---
    private byte _newTileHeight = 8;
    private ShapeType _selectedShape = ShapeType.Rectangle;
    private bool _isEdgeMode = false;
    private int _edgeWidth = 1;

    // --- UI State for Randomization ---
    private bool _shouldRandomizeHeight = false;
    private int _minRandomHeight = 8;
    private int _maxRandomHeight = 20;

    // --- UI State for New Property Flags Feature ---
    private bool _shouldSetPropertyFlags = false;
    private int _newPropertyFlagsValue = 0;
    private bool _setPropertyForInnerTilesOnly = false;

    // --- State for the Live Preview feature ---
    private bool _isPreviewActive = false;
    private readonly Dictionary<int, byte> _previewOriginalStates = new();
    private HashSet<int> _currentlyPreviewedTiles = new();

    public MassTileHeightEditor(MapRegionSelector regionSelector)
    {
        _regionSelector = regionSelector;
    }

    // Destructor to ensure we clean up the preview if the editor is closed
    ~MassTileHeightEditor()
    {
        // If the preview is active when this object is destroyed, restore the tiles.
        // This prevents leaving black marks on the map if the user closes the tool.
        RestorePreview();
    }

    private string GetShapeLocString(ShapeType shape)
    {
        return shape switch
        {
            ShapeType.Rectangle => LocalizationManager.Instance.GetString("SE_EDITOR_MASS_SHAPE_RECT"),
            ShapeType.Circle => LocalizationManager.Instance.GetString("SE_EDITOR_MASS_SHAPE_CIRCLE"),
            _ => shape.ToString()
        };
    }

    public void Render()
    {
        ImGui.Begin(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_TITLE"));

        // --- Main Height and Shape Controls ---
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_RND_HEIGHT"), ref _shouldRandomizeHeight);

        if (_shouldRandomizeHeight)
        {
            ImGui.Indent();
            int min = _minRandomHeight;
            if (ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_MIN_HEIGHT"), ref min))
            {
                _minRandomHeight = (int)MathUtil.Clamp(min, byte.MinValue, byte.MaxValue);
            }
            ImGui.SameLine();
            ImGui.SetNextItemWidth(100);
            int max = _maxRandomHeight;
            if (ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_MAX_HEIGHT"), ref max))
            {
                _maxRandomHeight = (int)MathUtil.Clamp(max, byte.MinValue, byte.MaxValue);
            }
            ImGui.Unindent();
        }
        else
        {
            int height = _newTileHeight;
            if (ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_NEW_HEIGHT"), ref height))
            {
                _newTileHeight = (byte)MathUtil.Clamp(height, byte.MinValue, byte.MaxValue);
            }
        }

        if (ImGui.BeginCombo(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_SHAPE"), GetShapeLocString(_selectedShape)))
        {
            foreach (ShapeType shape in Enum.GetValues(typeof(ShapeType)))
            {
                if (ImGui.Selectable(GetShapeLocString(shape), _selectedShape == shape))
                    _selectedShape = shape;
            }
            ImGui.EndCombo();
        }

        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_EDGES_ONLY"), ref _isEdgeMode);
        if (_isEdgeMode)
        {
            ImGui.SameLine(); ImGui.SetNextItemWidth(100);
            ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_EDGE_WIDTH"), ref _edgeWidth);
            _edgeWidth = Math.Max(1, _edgeWidth);
        }
        ImGui.Separator();

        // --- New UI for Tile Property Flags ---
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_SET_FLAGS"), ref _shouldSetPropertyFlags);
        if (_shouldSetPropertyFlags)
        {
            ImGui.Indent();
            ImGui.InputInt(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_FLAGS_VAL"), ref _newPropertyFlagsValue);
            ImGui.SameLine(); ImGui.Text($"0x{_newPropertyFlagsValue:X8}");

            if (_isEdgeMode)
            {
                ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_INNER_ONLY"), ref _setPropertyForInnerTilesOnly);
            }
            else
            {
                _setPropertyForInnerTilesOnly = false; // This option is invalid if not in edge mode
            }
            ImGui.Unindent();
        }
        ImGui.Separator();

        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_PREVIEW"), ref _isPreviewActive);
        ImGui.Separator();

        // --- Action Buttons ---
        bool canPerformAction = _regionSelector.BeginPoint.HasValue && _regionSelector.EndPoint.HasValue;
        if (canPerformAction)
        {
            if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_BTN_APPLY")))
                ApplyChanges(_regionSelector.BeginPoint.Value, _regionSelector.EndPoint.Value);
        }
        else ImGui.Text(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_WARN_SELECT"));

        if (ImGui.Button(LocalizationManager.Instance.GetString("SE_EDITOR_MASS_BTN_UNDO")) && _undoStack.Count > 0) UndoLastAction();

        HandlePreviewState(canPerformAction);
        ImGui.End();
    }

    /// <summary>
    /// Manages the state of the preview, applying or restoring it as needed.
    /// This is the controller for the preview feature.
    /// </summary>
    private unsafe void HandlePreviewState(bool hasSelection)
    {
        // Get the set of tiles that *should* be previewed based on current UI settings.
        HashSet<int> targetPreviewTiles = new HashSet<int>();
        if (hasSelection && _isPreviewActive)
        {
            targetPreviewTiles = new HashSet<int>(GetTilesInShape(_regionSelector.BeginPoint.Value, _regionSelector.EndPoint.Value, _isEdgeMode));
        }

        // If the target selection is different from what's currently previewed, we need to update.
        if (!targetPreviewTiles.SetEquals(_currentlyPreviewedTiles))
        {
            // First, always restore the old preview to clean the slate.
            RestorePreview();

            // If the preview is active and there's a new selection, apply the new preview.
            if (_isPreviewActive && targetPreviewTiles.Count > 0)
            {
                ApplyPreview(targetPreviewTiles);
            }
        }
    }

    /// <summary>
    /// The master function that applies all configured changes (height and/or properties).
    /// </summary>
    private unsafe void ApplyChanges(Vector2 begin, Vector2 end)
    {
        if (_isPreviewActive) RestorePreview();

        // Determine which tiles are affected by which operation
        List<int> heightTiles = GetTilesInShape(begin, end, _isEdgeMode);
        List<int> propertyTiles = new List<int>();

        if (_shouldSetPropertyFlags)
        {
            if (_isEdgeMode && _setPropertyForInnerTilesOnly)
            {
                // Calculate the inner region by getting the full shape and subtracting the edges
                List<int> allTilesInShape = GetTilesInShape(begin, end, false); // Get the fill
                propertyTiles = allTilesInShape.Except(heightTiles).ToList();
            }
            else
            {
                // Apply to the same region as the height
                propertyTiles = new List<int>(heightTiles);
            }
        }

        if (heightTiles.Count == 0 && propertyTiles.Count == 0)
        {
            LogHelper.Warning("No tiles were selected for modification.");
            return;
        }

        // Record the original state for all affected tiles
        MapEditorUndoAction undoAction = new MapEditorUndoAction();
        GameTileManagerView tm = GameTileManagerAPI.Instance.TileManager;

        foreach (int tileId in heightTiles)
            if (tileId >= 0) undoAction.RecordHeightState(tileId, tm.HeightGrid[tileId]);

        foreach (int tileId in propertyTiles)
            if (tileId >= 0) undoAction.RecordPropertyFlagState(tileId, tm.LogicGrid[tileId]);

        _undoStack.Push(undoAction);

        // Apply the changes to game memory
        LogHelper.Information($"Applying changes: {heightTiles.Count} height tiles, {propertyTiles.Count} property tiles.");
        foreach (int tileId in heightTiles)
        {
            if (tileId >= 0)
            {
                byte heightToApply;
                if (_shouldRandomizeHeight)
                {
                    int min = Math.Min(_minRandomHeight, _maxRandomHeight);
                    int max = Math.Max(_minRandomHeight, _maxRandomHeight);
                    heightToApply = (byte)_random.Next(min, max + 1); // .Next's upper bound is exclusive
                }
                else
                {
                    heightToApply = _newTileHeight;
                }

                tm.HeightGrid[tileId] = heightToApply;
                UnmanagedVector2<UInt16> tileCoords = GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
                BulkMapEditorDetours.c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, tileCoords.X, tileCoords.Y);
            }
        }

        foreach (int tileId in propertyTiles)
        {
            if (tileId >= 0)
            {
                tm.LogicGrid[tileId] = _newPropertyFlagsValue;
                UnmanagedVector2<UInt16> tileCoords = GameTileManagerAPI.Instance.GetTileVectorFromId(tileId);
                BulkMapEditorDetours.c_game_tile_refresh_visual(GameTileManagerAPI.Instance.GetPathfindingContext(), 9, tileCoords.X, tileCoords.Y);
            }
        }

        LogHelper.Information("Operation completed.");
    }

    /// <summary>
    /// Saves the state of the target tiles and sets their height to 0.
    /// </summary>
    private unsafe void ApplyPreview(HashSet<int> tilesToPreview)
    {
        Span<byte> tileHeightGrid = GameTileManagerAPI.Instance.TileManager.HeightGrid;
        foreach (int tileId in tilesToPreview)
        {
            if (tileId >= 0 && tileId < tileHeightGrid.Length)
            {
                // Save the original height before changing it
                _previewOriginalStates[tileId] = tileHeightGrid[tileId];
                tileHeightGrid[tileId] = 0; // Set to black
            }
        }
        // Update our state to reflect what's currently on the screen
        _currentlyPreviewedTiles = tilesToPreview;
        LogHelper.Information($"Applied preview to {_currentlyPreviewedTiles.Count} tiles.");
    }

    /// <summary>
    /// Restores the original heights of any tiles modified by the preview.
    /// </summary>
    private unsafe void RestorePreview()
    {
        if (_previewOriginalStates.Count == 0) return;

        Span<byte> tileHeightGrid = GameTileManagerAPI.Instance.TileManager.HeightGrid;
        foreach (KeyValuePair<int, byte> state in _previewOriginalStates)
        {
            int tileId = state.Key;
            byte originalHeight = state.Value;
            if (tileId >= 0 && tileId < tileHeightGrid.Length)
            {
                tileHeightGrid[tileId] = originalHeight;
            }
        }

        LogHelper.Information($"Restored {_previewOriginalStates.Count} previewed tiles.");
        _previewOriginalStates.Clear();
        _currentlyPreviewedTiles.Clear();
    }

    /// <summary>
    /// Restores both height and property flags from the most recent action.
    /// </summary>
    private unsafe void UndoLastAction()
    {
        if (_undoStack.Count == 0)
            return;

        if (_isPreviewActive)
            RestorePreview();

        MapEditorUndoAction lastAction = _undoStack.Pop();
        GameTileManagerView tm = GameTileManagerAPI.Instance.TileManager;

        foreach (KeyValuePair<int, byte> state in lastAction.PreviousHeightStates)
        {
            if (state.Key >= 0) tm.HeightGrid[state.Key] = state.Value;
        }

        foreach (KeyValuePair<int, int> state in lastAction.PreviousPropertyFlagStates)
        {
            if (state.Key >= 0) tm.LogicGrid[state.Key] = state.Value;
        }

        LogHelper.Information($"Undid last action, restoring {lastAction.PreviousHeightStates.Count} heights and {lastAction.PreviousPropertyFlagStates.Count} property flags.");
    }

    /// <summary>
    /// Gathers a list of all Tile IDs that fall within the selected shape and mode (Fill or Edge).
    /// </summary>
    private List<int> GetTilesInShape(Vector2 begin, Vector2 end, bool useEdgeMode)
    {
        List<int> tiles = new List<int>();
        int startX = (int)Math.Min(begin.X, end.X);
        int startY = (int)Math.Min(begin.Y, end.Y);
        int endX = (int)Math.Max(begin.X, end.X);
        int endY = (int)Math.Max(begin.Y, end.Y);

        // --- If not in Edge Mode, run the original fill logic ---
        if (!useEdgeMode)
        {
            switch (_selectedShape)
            {
                case ShapeType.Rectangle:
                    for (int y = startY; y <= endY; y++)
                        for (int x = startX; x <= endX; x++)
                            tiles.Add(GameTileManagerAPI.Instance.GetTileId(x, y));
                    break;
                case ShapeType.Circle:
                    float centerX = (startX + endX) / 2.0f;
                    float centerY = (startY + endY) / 2.0f;
                    float radiusX = (endX - startX) / 2.0f;
                    float radiusY = (endY - startY) / 2.0f;
                    if (radiusX < 0.5f || radiusY < 0.5f) goto case ShapeType.Rectangle;
                    for (int y = startY; y <= endY; y++)
                        for (int x = startX; x <= endX; x++)
                            if (((x - centerX) * (x - centerX)) / (radiusX * radiusX) + ((y - centerY) * (y - centerY)) / (radiusY * radiusY) <= 1)
                                tiles.Add(GameTileManagerAPI.Instance.GetTileId(x, y));
                    break;
            }
            return tiles;
        }

        // --- New Logic for Edge Mode ---
        switch (_selectedShape)
        {
            case ShapeType.Rectangle:
                for (int y = startY; y <= endY; y++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        if (x < startX + _edgeWidth || x > endX - _edgeWidth || y < startY + _edgeWidth || y > endY - _edgeWidth)
                        {
                            tiles.Add(GameTileManagerAPI.Instance.GetTileId(x, y));
                        }
                    }
                }
                break;

            case ShapeType.Circle:
                // Calculate properties of the outer ellipse
                float outerCenterX = (startX + endX) / 2.0f;
                float outerCenterY = (startY + endY) / 2.0f;
                float outerRadiusX = (endX - startX) / 2.0f;
                float outerRadiusY = (endY - startY) / 2.0f;

                // Calculate properties of the inner ellipse (the "hole")
                float innerRadiusX = outerRadiusX - _edgeWidth;
                float innerRadiusY = outerRadiusY - _edgeWidth;

                // If the edge width is too big, just fill the whole shape
                if (innerRadiusX < 0 || innerRadiusY < 0)
                {
                    return GetTilesInShape(begin, end, false);
                }

                for (int y = startY; y <= endY; y++)
                {
                    for (int x = startX; x <= endX; x++)
                    {
                        float dx = x - outerCenterX;
                        float dy = y - outerCenterY;

                        // Check if the point is inside the OUTER ellipse
                        bool isInsideOuter = ((dx * dx) / (outerRadiusX * outerRadiusX) + (dy * dy) / (outerRadiusY * outerRadiusY)) <= 1;

                        // Check if the point is OUTSIDE the INNER ellipse
                        bool isOutsideInner = ((dx * dx) / (innerRadiusX * innerRadiusX) + (dy * dy) / (innerRadiusY * innerRadiusY)) > 1;

                        if (isInsideOuter && isOutsideInner)
                        {
                            tiles.Add(GameTileManagerAPI.Instance.GetTileId(x, y));
                        }
                    }
                }
                break;
        }
        return tiles;
    }
}