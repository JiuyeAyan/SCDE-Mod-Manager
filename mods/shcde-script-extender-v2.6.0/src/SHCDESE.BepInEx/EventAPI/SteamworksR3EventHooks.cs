using SHCDESE.EventAPI.Steamworks;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to steamworks events
/// </summary>
public static class SteamworksR3EventHooks
{
    /// <summary>
    /// Fired when steamworks becomes intiialized
    /// </summary>
    public static readonly R3EventHook<SteamworksInitializedEventArgs> OnSteamworksInitialized = new();


}