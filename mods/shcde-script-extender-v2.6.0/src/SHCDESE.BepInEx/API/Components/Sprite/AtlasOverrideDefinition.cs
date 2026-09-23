using SHCDESE.API.Components.Assets;
namespace SHCDESE.API.Components.Sprite;

/// <summary>
/// Defines a mod-supplied texture atlas that replaces an entire GM sprite group.
/// </summary>
public class AtlasOverrideDefinition
{
    internal IndexedModResource? AtlasTextureResource { get; init; }
    internal IndexedModResource? MaskTextureResource { get; init; }
    internal IndexedModResource? JsonResource { get; init; }

    /// <summary>
    /// The GM file name as used by the game, e.g. "body_bedouin_healer".
    /// This must match the prefix used in sprite frame names.
    /// </summary>
    public string GmFileName { get; init; }

    /// <summary>
    /// The corresponding GM enum value, e.g. Enums.GM.GM_BODY_BEDOUIN_HEALER.
    /// </summary>
    public Enums.GM GmFileID { get; init; }

    /// <summary>
    /// Absolute path to the atlas texture (.png or .dds).
    /// </summary>
    public string AtlasTexturePath { get; init; }

    /// <summary>
    /// Absolute path to the team-colour/foliage mask texture (.png or .dds).
    /// Optional for Auto/Plain; required when TeamColour or Foliage is selected explicitly.
    /// </summary>
    public string MaskTexturePath { get; init; }

    /// <summary>
    /// Absolute path to the atlas JSON (frame definitions).
    /// </summary>
    public string JsonPath { get; init; }

    /// <summary>
    /// Optional colour palette override. If null, the game's default colour array is used.
    /// Must be exactly 10 entries to match the game's gmColors layout.
    /// </summary>
    public UnityEngine.Color[] ColourOverride { get; init; }

    /// <summary>
    /// Material used by the atlas. <see cref="SpriteMaterialMode.Auto"/> matches
    /// the already-loaded vanilla GM group when a mask is supplied, and uses
    /// <see cref="SpriteMaterialMode.Plain"/> when the atlas has no mask.
    /// </summary>
    public SpriteMaterialMode MaterialMode { get; init; } = SpriteMaterialMode.Auto;

    /// <summary>
    /// True for dash-format sprite names ("body_archer-0"),
    /// false for space+3digit format ("tile_land8 000").
    /// Populated automatically by discovery; only set manually if using RegisterAtlasOverride directly.
    /// </summary>
    public bool DashFormat { get; init; } = true;
}
