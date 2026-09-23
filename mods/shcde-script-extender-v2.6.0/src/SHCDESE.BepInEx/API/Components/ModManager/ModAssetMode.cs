using System.Text.Json.Serialization;

namespace SHCDESE.API.Components.ModManager;

/// <summary>
/// Determines whether a mod's <c>Override/</c> resources participate in the shared game override namespace.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ModAssetMode : byte
{
    /// <summary>
    /// The mod's <c>Override/</c> resources replace matching game or mod resources using normal load order.
    /// </summary>
    Global = 0,

    /// <summary>
    /// The mod's <c>Override/</c> resources remain private to its GUID and do not replace resources from other providers.
    /// Custom-lord face, voice, and video paths are resolved inside this private namespace.
    /// </summary>
    Local = 1
}
