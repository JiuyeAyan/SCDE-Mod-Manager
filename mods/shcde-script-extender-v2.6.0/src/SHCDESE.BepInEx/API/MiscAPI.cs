using CrusaderDE;
using System;
using System.Reflection;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for miscellaneous, uncategorized, or global game functions.
/// </summary>
/// <remarks>
/// This class is a singleton that serves as a catch-all for utility functions that do not fit
/// into the more specific managers like <see cref="GameUnitManagerAPI"/> or <see cref="GameBuildingManagerAPI"/>.
/// It includes functions for accessing general game state, such as the version string.
/// </remarks>
public sealed class MiscAPI
{
#pragma warning disable 0618
    private static readonly Lazy<MiscAPI> lazy = new Lazy<MiscAPI>(() => new MiscAPI());

    /// <summary>
    /// Gets the singleton instance of the <see cref="MiscAPI"/>.
    /// </summary>
    public static MiscAPI Instance { get { return lazy.Value; } }

    /// <summary>
    /// Initializes a new instance of the <see cref="MiscAPI"/> class.
    /// This constructor is private to enforce the singleton pattern.
    /// </summary>
    private MiscAPI()
    {


    }

    //
    // Common Functions
    //


    /// <summary>
    /// Gets the current game version string displayed in the main menu.
    /// </summary>
    /// <returns>The game version string, for example: <c>" Version V1.05"</c>.</returns>
    /// <remarks>
    /// This function uses C# reflection to access a private static field within the game's core <c>MainViewModel</c> class.
    /// </remarks>
    public string GetGameVersion()
    {
        FieldInfo? _versionField = typeof(MainViewModel).GetField(nameof(MainViewModel._VersionString), BindingFlags.NonPublic | BindingFlags.Static);
        return (string)_versionField.GetValue(null);
    }

    /// <summary>
    /// Sets the game version string displayed in the main menu.
    /// </summary>
    /// <param name="version">The new version string to display.</param>
    /// <remarks>
    /// This function is intended for debugging or special use cases only. Modifying the version string
    /// does not change any game logic but can be used to display custom information in the UI.
    /// Use this function with caution as it directly modifies a private game field via reflection.
    /// </remarks>
    public void SetGameVersion(string version)
    {
        FieldInfo? _versionField = typeof(MainViewModel).GetField(nameof(MainViewModel._VersionString), BindingFlags.NonPublic | BindingFlags.Static);
        _versionField.SetValue(null, version);
    }

#pragma warning restore 0618
}
