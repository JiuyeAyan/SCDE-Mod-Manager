using System;
using System.Collections.Generic;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Represents metadata for a mod or map, deserialized from an 'info.json' file within a folder, map archive, etc.
/// </summary>
public sealed class ModInfo
{
    /// <summary>
    /// Gets or sets the internal guid of the mod
    /// </summary>
    public string GUID { get; set; } = null!;

    /// <summary>
    /// Gets or sets the name of the mod's author.
    /// </summary>
    public string Author { get; set; } = null!;

    /// <summary>
    /// Gets or sets the display name of the mod.
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets or sets the description of the mod.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Gets or sets the version of the mod content (e.g., scripts, assets).
    /// </summary>
    public string Version { get; set; } = null!;

    /// <summary>
    /// Gets or sets the other mods required by this mod, identified by their <see cref="GUID"/>.
    /// Missing dependencies and installed versions outside an optional inclusive range are reported during startup.
    /// </summary>
    public List<ModDependency> Dependencies { get; set; } = [];

    /// <summary>
    /// Gets or sets a URL for the mod's website or repository.
    /// </summary>
    public string Website { get; set; } = null!;

    /// <summary>
    /// Gets or sets the GitHub or GitLab repository URL used to check the latest release.
    /// Leave this empty to disable automatic update checks for the mod.
    /// </summary>
    public string VersionCheckUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the mod's Steam Workshop page. 
    /// When present, this is normalized and advertised with the mod's multiplayer lobby metadata for future workshop actions.
    /// </summary>
    public string WorkshopUrl { get; set; } = string.Empty;

    /// <summary>
    /// Determines the content this mod features.
    /// Asset by default.
    /// </summary>
    public ModManifest Manifest { get; set; } = ModManifest.Asset;

    /// <summary>
    /// Determines whether files supplied through this mod's <c>Override/</c> folder are global overrides or private resources belonging only to this mod. 
    /// Global by default.
    /// </summary>
    public ModAssetMode AssetMode { get; set; } = ModAssetMode.Global;

    /// <summary>
    /// Determines the network mode of the mod.
    /// </summary>
    public ModNetworkMode NetworkMode { get; set; } = ModNetworkMode.Clientside;
}
