using ImGuiNET;
using SHCDESE.API;
using SHCDESE.Interop.Enums;
using System;
using System.Collections.Generic;
using UUIMGUI.Core;

namespace SHCDESE.ImMenu.MapEditor;

public class TilePropertyMutatorTool
{
    public bool Enabled = false;
    public bool ForceSetEnabled = false;

    private string[] _tileProperties = null;
    private int _currentTilePropertySelected = 0;

    /// <summary>
    /// Renders the UI
    /// </summary>
    public unsafe void Render()
    {
        if (_tileProperties == null)
        {
            List<string> items = new List<string>();
            foreach (string s in Enum.GetNames(typeof(TilePropertyFlag)))
            {
                items.Add(s);
            }
            _tileProperties = items.ToArray();
        }

        ImGui.Begin(LocalizationManager.Instance.GetString("SE_EDITOR_MUTATOR_TITLE"));

        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_MUTATOR_OPTIONS"));
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MUTATOR_ENABLED"), ref Enabled);
        ImGui.Checkbox(LocalizationManager.Instance.GetString("SE_EDITOR_MUTATOR_FORCE_SET"), ref ForceSetEnabled);

        ImGui.BeginGroup();
        ImGui.SeparatorText(LocalizationManager.Instance.GetString("SE_EDITOR_MUTATOR_AUGMENT"));
        ImGui.Combo(LocalizationManager.Instance.GetString("SE_EDITOR_MUTATOR_TYPE"), ref _currentTilePropertySelected, _tileProperties, _tileProperties.Length);
        ImGui.EndGroup();

        ImGui.End();
    }

    /// <summary>
    /// Resets misc data
    /// </summary>
    public void Reset()
    {


    }

    public int ExecuteAugmentAction(int previousTileProperty)
    {
        GameTileManagerAPI gtm = GameTileManagerAPI.Instance;
        TilePropertyFlag prev = (TilePropertyFlag)previousTileProperty;

        if (ForceSetEnabled)
        {
            prev = (TilePropertyFlag)Enum.Parse(typeof(TilePropertyFlag), _tileProperties[_currentTilePropertySelected]);
        }
        else
        {
            prev |= (TilePropertyFlag)Enum.Parse(typeof(TilePropertyFlag), _tileProperties[_currentTilePropertySelected]);
        }
        return (int)prev;
    }
}