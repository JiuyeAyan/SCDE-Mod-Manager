using Noesis;
using SHCDESE.Interop.Enums;
using System.Collections.Generic;
using UnityEngine;

namespace SHCDESE.API.Components.AI;

/// <summary>
/// Represents a fully resolved, runtime entry for a custom lord registered with <see cref="GameAIManagerAPI"/>.
/// </summary>
/// <remarks>
/// Populated during map load from <c>info.json</c> and <c>lordmeta.json</c>. Assets (face texture, voice lines)
/// are resolved lazily on first access.
/// </remarks>
public class CustomLordEntry
{
    /// <summary>Gets or sets the deserialized <c>lordmeta.json</c> data for this lord.</summary>
    public LordInfo LordInfo { get; set; } = null!;

    /// <summary>
    /// Gets or sets the lazily loaded face portrait texture.
    /// <see langword="null"/> until first accessed via <see cref="GameAIManagerAPI.TryGetFace"/>.
    /// </summary>
    public Texture2D? FaceTexture { get; set; }

    /// <summary>
    /// Gets or sets the Noesis-compatible image source wrapping <see cref="FaceTexture"/>.
    /// <see langword="null"/> until first accessed via <see cref="GameAIManagerAPI.TryGetFace"/>.
    /// </summary>
    public ImageSource? Face { get; set; }

    /// <summary>
    /// Gets or sets the active localized title list for this lord.
    /// Resolved from <see cref="LordInfo.LocalizedTitles"/> at access time against the current game language.
    /// </summary>
    public List<string> Titles { get; set; } = new List<string>();

    /// <summary>
    /// Gets or sets the active localized display name for this lord.
    /// Resolved from <see cref="LordInfo.LocalizedDisplayName"/> at access time.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>Gets or sets the lowercase internal lord name used as the dictionary key.</summary>
    public string InternalName { get; set; } = null!;

    /// <summary>
    /// Gets or sets all parsed voice line clips, keyed by <see cref="AILordMessageType"/>.
    /// Populated at load time from <see cref="LordInfo.Messages"/>.
    /// </summary>
    public Dictionary<AILordMessageType, List<LordMessageClip>> VoiceLines { get; set; }
        = new Dictionary<AILordMessageType, List<LordMessageClip>>();

    /// <summary>
    /// Gets or sets the absolute path to this lord's <c>init.lua</c> script.
    /// <see langword="null"/> if the lord has no Lua AI script.
    /// </summary>
    public string? LuaInitPath { get; set; }
}