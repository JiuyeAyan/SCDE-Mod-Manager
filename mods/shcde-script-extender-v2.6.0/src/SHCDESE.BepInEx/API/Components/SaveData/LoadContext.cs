using System;

namespace SHCDESE.API.Components.SaveData;

/// <summary>
/// Context information provided to mods when loading data.
/// </summary>
public class LoadContext
{
    /// <summary>
    /// True if this is a save file (.sav) being loaded.
    /// False if this is a map file (.map) being loaded.
    /// </summary>
    public bool IsSaveFile { get; init; }

    /// <summary>
    /// The file path being loaded from, if available.
    /// </summary>
    public string? FilePath { get; init; }

    public LoadContext(bool isSaveFile, string? filePath = null)
    {
        IsSaveFile = isSaveFile;
        FilePath = filePath;
    }
}
