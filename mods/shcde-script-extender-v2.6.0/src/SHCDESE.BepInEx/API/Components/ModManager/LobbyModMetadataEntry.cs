using System.Text.Json.Serialization;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Compact representation of one active mod stored in Steam lobby metadata.
/// </summary>
internal sealed class LobbyModMetadataEntry
{
    [JsonPropertyName("g")]
    public string Guid { get; set; } = string.Empty;

    [JsonPropertyName("n")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("v")]
    public string Version { get; set; } = string.Empty;

    [JsonPropertyName("w")]
    public string WorkshopUrl { get; set; } = string.Empty;

    [JsonPropertyName("c")]
    public bool Clientside { get; set; }
}
