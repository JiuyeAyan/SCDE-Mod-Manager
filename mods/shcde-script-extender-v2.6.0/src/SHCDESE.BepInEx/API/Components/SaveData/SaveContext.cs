using System;

namespace SHCDESE.API.Components.SaveData;


/// <summary>
/// Context information provided to mods when saving data.
/// </summary>
public class SaveContext
{
    /// <summary>
    /// True if this is a save file (.sav) being saved during gameplay.
    /// False if this is a map file (.map).
    /// </summary>
    public bool IsSaveFile { get; init; }

    /// <summary>
    /// True if the save is happening from the map editor.
    /// False if it's a normal gameplay save.
    /// </summary>
    public bool IsMapEditorSave { get; init; }

    /// <summary>
    /// The file path being saved to, if available.
    /// </summary>
    public string? FilePath { get; init; }

    public SaveContext(bool isSaveFile, bool isMapEditorSave, string? filePath = null)
    {
        IsSaveFile = isSaveFile;
        IsMapEditorSave = isMapEditorSave;
        FilePath = filePath;
    }
}