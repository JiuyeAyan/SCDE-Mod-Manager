using SHCDESE.EventAPI.Network;
using SHCDESE.Lua.DocsGen;
using static Platform_Multiplayer;

namespace SHCDESE.EventAPI;

/// <summary>
/// Provides static R3EventHook fields for subscribing to network-related events.
/// </summary>
/// <remarks>
/// This class centralizes all events related to chat messages and commands,
/// allowing mods to intercept, modify, or react to communication in both the pre-game lobby and during a match.
/// The events are parsed and raised by the <see cref="API.GameNetworkAPI"/>.
/// </remarks>
public static class NetworkR3EventHooks
{
    /// <summary>
    /// Fired when a chat message is received from another player in the multiplayer lobby.
    /// </summary>
    [LuaApiExport("OnReceiveLobbyChatMessage")]
    public static readonly R3EventHook<ReceiveLobbyChatMessageEventArgs> OnReceiveLobbyChatMessage = new();

    /// <summary>
    /// Fired when a received lobby chat message is identified as a command (i.e., starts with '/').
    /// </summary>
    [LuaApiExport("OnReceiveLobbyChatCommand")]
    public static readonly R3EventHook<ReceiveLobbyChatCommandEventArgs> OnReceiveLobbyChatCommand = new();

    /// <summary>
    /// Fired just before the local player sends a chat message in the multiplayer lobby.
    /// </summary>
    [LuaApiExport("OnSendLobbyChatMessage")]
    public static readonly R3EventHook<SendLobbyChatMessageEventArgs> OnSendLobbyChatMessage = new();

    /// <summary>
    /// Fired when an in-game chat message is received from another player.
    /// </summary>
    [LuaApiExport("OnReceiveInGameChatMessage")]
    public static readonly R3EventHook<ReceiveInGameChatMessageEventArgs> OnReceiveInGameChatMessage = new();

    /// <summary>
    /// Fired just before the local player sends an in-game chat message to other players.
    /// </summary>
    [LuaApiExport("OnSendInGameChatMessage")]
    public static readonly R3EventHook<SendInGameChatMessageEventArgs> OnSendInGameChatMessage = new();

    /// <summary>
    /// Fired when a received in-game chat message is identified as a command (i.e., starts with '/').
    /// </summary>
    [LuaApiExport("OnReceiveInGameChatCommand")]
    public static readonly R3EventHook<ReceiveInGameChatCommandEventArgs> OnReceiveInGameChatCommand = new();

    /// <summary>
    /// Fired when custom info data is being sent to players (happens after they join, usually for custom AIV related data)
    /// </summary>
    public static readonly R3EventHook<OnSendCustomInfoToLobbyMemberEventArgs> OnSendCustomInfoToLobbyMember = new();
}