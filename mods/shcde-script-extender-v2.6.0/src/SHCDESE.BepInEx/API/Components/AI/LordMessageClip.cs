using System.Collections.Generic;
namespace SHCDESE.API.Components.AI;

/// <summary>
/// Represents a single playable voice clip for a custom lord, pairing video, audio, and optional localized subtitle text.
/// </summary>
public class LordMessageClip
{
    /// <summary>
    /// Gets or sets the path to the lord's video file.
    /// Leave as an empty string if no video is used for this clip.
    /// </summary>
    public string VideoPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the lord's audio file.
    /// Resolved via the Asset API, so locale-specific overrides apply.
    /// </summary>
    public string AudioPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the localized subtitle text for this clip, keyed by BCP 47 locale (e.g. <c>"en-US"</c>).
    /// Falls back to <c>"en-US"</c> if the player's current language has no entry.
    /// May be <see langword="null"/> if no subtitles are provided.
    /// </summary>
    public Dictionary<string, string>? LocalizedText { get; set; }
}