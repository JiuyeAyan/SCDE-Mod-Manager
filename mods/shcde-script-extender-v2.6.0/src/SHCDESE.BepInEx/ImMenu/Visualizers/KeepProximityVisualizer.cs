using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using UnityEngine;

namespace SHCDESE.ImMenu.Visualizers;

public unsafe class KeepProximityVisualizer : MonoBehaviour
{
    public static KeepProximityVisualizer Instance { get; private set; }

    public bool IsActive = false;
    public int TargetPlayerId = 1;
    public float Alpha = 0.5f;

    // Colors
    private readonly Color ColorInRange = new Color(0, 1, 0);    // Green
    private readonly Color ColorBuffer = new Color(1, 1, 0);     // Yellow (+5 buffer)
    private readonly Color ColorTooFar = new Color(1, 0, 0);     // Red

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    public void RenderUI()
    {
        ImGui.Checkbox("Enable Keep Proximity Visualizer", ref IsActive);

        if (!IsActive) return;

        ImGui.Separator();

        // Player Selector
        if (ImGui.BeginCombo("Target Player", $"Player {TargetPlayerId}"))
        {
            for (int i = 1; i <= 8; i++)
            {
                if (ImGui.Selectable($"Player {i}", TargetPlayerId == i))
                {
                    TargetPlayerId = i;
                }
            }
            ImGui.EndCombo();
        }

        ImGui.SliderFloat("Overlay Alpha", ref Alpha, 0.0f, 1.0f);

        // Display current stats for selected player
        UnmanagedVector2<int> keepPos = GamePlayerManagerAPI.Instance.GetPlayerKeepPosition(TargetPlayerId);
        int mapSize = (int)GameTileManagerAPI.Instance.TileManager.CurrentMapSize;
        int baseRange = GameBuildingManagerAPI.Instance.GetKeepProximityRange(mapSize);

        ImGui.TextDisabled($"Keep Origin: {keepPos.X}, {keepPos.Y}");
        ImGui.TextDisabled($"Base Range: {baseRange} tiles");
        ImGui.TextDisabled($"Max Range (incl. buffer): {baseRange + 5} tiles");

        if (ImGui.Button("Refresh Tilemap"))
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                if (TilemapManager.instance != null)
                    TilemapManager.instance.triggerTMFullRefresh();
            });
        }
    }

    /// <summary>
    /// Called by the Tilemap rendering system to apply tints to tiles.
    /// </summary>
    public bool TryGetColorOverride(int x, int y, out Color color)
    {
        color = Color.white;
        if (!IsActive) 
            return false;

        // Get Keep Position
        UnmanagedVector2<int> keepDoorPos = GamePlayerManagerAPI.Instance.GetPlayerKeepDoorPosition(TargetPlayerId);
        if (keepDoorPos.X <= 0 && keepDoorPos.Y <= 0) 
            return false;

        // Calculate Directional Distances with offsets xDist/yDist represent how "far" the engine thinks the tile is.

        int xDist;
        if (x < keepDoorPos.X)
        {
            xDist = Math.Abs(x - keepDoorPos.X); // LEFT
        }
        else
        {
            xDist = Math.Abs(x - keepDoorPos.X); // RIGHT
        }

        int yDist;
        if (y < keepDoorPos.Y)
        {
            yDist = Math.Abs(y - keepDoorPos.Y) - 1; // TOP
        }
        else
        {
            yDist = Math.Abs(y - keepDoorPos.Y) + 1; // BOTTOM
        }

        // Chebyshev Distance (The Square Logic)
        int effectiveDistance = Math.Max(xDist, yDist);

        // Retrieve Ranges
        int mapSize = (int)GameTileManagerAPI.Instance.TileManager.CurrentMapSize;
        int baseRange = GameBuildingManagerAPI.Instance.GetKeepProximityRange(mapSize);
        int bufferZone = baseRange + 5;

        // Color Assignment
        if (effectiveDistance <= baseRange)
        {
            color = ColorInRange.ToAlpha(Alpha);
        }
        else if (effectiveDistance <= bufferZone)
        {
            color = ColorBuffer.ToAlpha(Alpha);
        }
        else
        {
            color = ColorTooFar.ToAlpha(Alpha);
        }

        return true;
    }
}