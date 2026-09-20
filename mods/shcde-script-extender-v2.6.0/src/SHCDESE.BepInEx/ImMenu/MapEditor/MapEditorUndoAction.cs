using System;
using System.Collections.Generic;

namespace SHCDESE.ImMenu.MapEditor;

/// <summary>
/// A class that stores the state of tiles before a modification,
/// allowing the action to be undone.
/// </summary>
public class MapEditorUndoAction
{
    public Dictionary<int, byte> PreviousHeightStates { get; }
    public Dictionary<int, int> PreviousPropertyFlagStates { get; }

    public MapEditorUndoAction()
    {
        PreviousHeightStates = new Dictionary<int, byte>();
        PreviousPropertyFlagStates = new Dictionary<int, int>();
    }

    public void RecordHeightState(int tileId, byte originalHeight)
    {
        if (!PreviousHeightStates.ContainsKey(tileId))
        {
            PreviousHeightStates.Add(tileId, originalHeight);
        }
    }

    public void RecordPropertyFlagState(int tileId, int originalFlags)
    {
        if (!PreviousPropertyFlagStates.ContainsKey(tileId))
        {
            PreviousPropertyFlagStates.Add(tileId, originalFlags);
        }
    }
}
