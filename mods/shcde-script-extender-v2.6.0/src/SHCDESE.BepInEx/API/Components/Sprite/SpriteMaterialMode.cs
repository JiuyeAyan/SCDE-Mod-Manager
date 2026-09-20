using System;

namespace SHCDESE.API.Components.Sprite;

/// <summary>
/// Selects the shader family used to render a sprite or GM atlas override.
/// </summary>
public enum SpriteMaterialMode
{
    /// <summary>
    /// Match the material used by the already-loaded vanilla GM group. Atlas
    /// overrides without a mask remain plain for backwards compatibility.
    /// </summary>
    Auto = 0,

    /// <summary>Use Unity's plain unlit 2D sprite material.</summary>
    Plain = 1,

    /// <summary>Use the game's <c>Unlit/TeamColour</c> shader.</summary>
    TeamColour = 2,

    /// <summary>Use the game's <c>Unlit/Foliage</c> shader.</summary>
    Foliage = 3,
}