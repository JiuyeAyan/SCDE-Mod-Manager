using System;

namespace SHCDESE.API.Components.Sprite;

/// <summary>
/// Represents a single parsed sprite frame from a mod atlas JSON.
/// Supports both the game's own raw sprite JSON schema and a simplified TexturePacker schema.
/// </summary>
public class AtlasFrameData
{
    /// <summary>Full sprite name, e.g. "body_bedouin_healer-86" or "body_bedouin_healer-86x"</summary>
    public string Name { get; set; }

    /// <summary>Pixel rect within the atlas texture (origin bottom-left, Unity convention).</summary>
    public UnityEngine.Rect TextureRect { get; set; }

    /// <summary>Normalised pivot (0..1 range).</summary>
    public UnityEngine.Vector2 Pivot { get; set; }

    /// <summary>Pixels per unit. Typically 64 for body sprites.</summary>
    public float PixelsPerUnit { get; set; } = 64f;

    /// <summary>True if this is an alt-frame (name ends in 'x').</summary>
    public bool IsAltFrame => Name != null && Name.EndsWith("x", StringComparison.Ordinal);
}