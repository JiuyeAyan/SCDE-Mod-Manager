using System;
using System.Collections.Generic;

namespace SHCDESE.API.Components.AI;

/// <summary>
/// Represents SE-specific metadata for a custom lord
/// </summary>
public sealed class LordInfo
{
    /// <summary>
    /// Gets or sets public display name of the lord
    /// </summary>
    public Dictionary<string, string>? LocalizedDisplayName { get; set; }

    public Dictionary<string, List<string>>? LocalizedTitles { get; set; }

    /// <summary>Gets or sets the lord description per locale.</summary>
    public Dictionary<string, string>? LocalizedDescription { get; set; }

    /// <summary>Gets or sets the displayed difficulty rating per locale.</summary>
    public Dictionary<string, string>? LocalizedDifficultyRating { get; set; }

    /// <summary>Gets or sets the lord's favourite troops per locale.</summary>
    public Dictionary<string, string>? LocalizedFavouriteTroops { get; set; }

    /// <summary>Gets or sets the lord's typical castle designs per locale.</summary>
    public Dictionary<string, string>? LocalizedCastles { get; set; }

    /// <summary>Gets or sets the lord's play style per locale.</summary>
    public Dictionary<string, string>? LocalizedPlayStyle { get; set; }

    /// <summary>Gets or sets the lord's favourite saying per locale.</summary>
    public Dictionary<string, string>? LocalizedFavouriteSaying { get; set; }

    /// <summary>
    /// Gets or sets public face of the lord
    /// </summary>
    public string FacePath { get; set; } = null!;

    /// <summary>
    /// Gets or sets introduction audio path of the lord (when added to lobby)
    /// </summary>
    public string JoinAudioPath { get; set; } = null!;

    /// <summary>
    /// Gets or sets exit audio path of the lord (when kicked from lobby)
    /// </summary>
    public string LeaveAudioPath { get; set; } = null!;

    public Dictionary<string, List<LordMessageClip>> Messages { get; set; } = new Dictionary<string, List<LordMessageClip>>();

    /// <summary>
    /// Gets or sets "incoming message" audio file
    /// </summary>
    public string IncomingMessage { get; set; } = null!;
}
