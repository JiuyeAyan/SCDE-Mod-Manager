using CrusaderDE;
using NLua;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Extensions;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;

namespace SHCDESE.Lua;

/// <summary>
/// Exposes a comprehensive set of networking functions to the Lua scripting environment.
/// This includes methods for sending RPCs and table packets, querying player information,
/// and managing in-game chat.
/// </summary>
[LuaApiNamespace("Net")]
public static class LuaNetworkAPI
{
    /// <summary>
    /// Registers all exported networking functions with a given NLua state.
    /// </summary>
    /// <param name="lua">The Lua instance to register the functions with.</param>
    public static void RegisterFunctions(NLua.Lua lua)
    {
        lua.RegisterExportedStaticMethods(typeof(LuaNetworkAPI));
        lua.RegisterExportedStaticMethods(typeof(GameNetworkAPI));
        lua.RegisterExportedStaticMethods(typeof(DeterministicRandom));
    }

    /// <summary>
    /// Gets a list of all players currently in the game session.
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetPlayersAsync")]
    public static void GetPlayersAsync(LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Retrieving players");
        UnityMainThreadDispatcher.DispatchGetAsync(GameNetworkAPI.GetPlayers, onCompleteCallback);
    }

    /// <summary>
    /// Gets a specific player by their unique player ID.
    /// </summary>
    /// <param name="playerId">The ID of the player to find.</param>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetPlayerByIdAsync")]
    public static void GetPlayerByIdAsync(int playerId, LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Retrieving player by id");
        UnityMainThreadDispatcher.DispatchGetAsync(() => GameNetworkAPI.GetPlayerById(playerId), onCompleteCallback);
    }

    /// <summary>
    /// Checks if the game is currently in a multiplayer session.
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("IsNetworkedEnvironmentAsync")]
    public static void IsNetworkedEnvironmentAsync(LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Checking for networked environment");
        UnityMainThreadDispatcher.DispatchGetAsync(GameNetworkAPI.IsNetworkedEnvironment, onCompleteCallback);
    }

    /// <summary>
    /// Mutes or unmutes the in-game chat for a specific player.
    /// </summary>
    /// <param name="playerId">The ID of the player to mute/unmute.</param>
    /// <param name="muted">The new mute status.</param>
    [LuaApiExport("SetChatMute")]
    public static void SetChatMute(int playerId, bool muted)
    {
        LogHelper.Verbose("Setting chat mute state");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameNetworkAPI.SetChatMute(playerId, muted);
        });
    }

    /// <summary>
    /// Checks if a specific player is currently chat-muted.
    /// </summary>
    /// <param name="playerId">The ID of the player to check.</param>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("IsChatMuteAsync")]
    public static void IsChatMuteAsync(int playerId, LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Checking player chat mute state");
        UnityMainThreadDispatcher.DispatchGetAsync(() => GameNetworkAPI.IsChatMute(playerId), onCompleteCallback);
    }

    /// <summary>
    /// Checks if the current player is the host.
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("Net_IsLocalHostAsync")]
    public static void IsLocalHostAsync(LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Checking if local is host");
        UnityMainThreadDispatcher.DispatchGetAsync(() => GameNetworkAPI.IsLocalHost(), onCompleteCallback);
    }

    /// <summary>
    /// Gets the local player id
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetLocalPlayerIdAsync")]
    public static void GetLocalPlayerIdAsync(LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Retrieving local player id");
        UnityMainThreadDispatcher.DispatchGetAsync(() => GameNetworkAPI.GetLocalPlayerId(), onCompleteCallback);
    }

    /// <summary>
    /// Forces the local player to leave the current game session.
    /// </summary>
    [LuaApiExport("LeaveGame")]
    public static void LeaveGame()
    {
        LogHelper.Verbose("Leaving game");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameNetworkAPI.LeaveGame();
        });
    }

    /// <summary>
    /// Gets the number of players currently active in the game (does not include kicked players).
    /// </summary>
    /// <param name="onCompleteCallback">The result callback</param>
    [LuaApiExport("GetNumActivePlayersAsync")]
    public static void GetNumActivePlayers(LuaFunction onCompleteCallback)
    {
        LogHelper.Verbose("Retrieving number of active players");
        UnityMainThreadDispatcher.DispatchGetAsync(GameNetworkAPI.GetNumActivePlayers, onCompleteCallback);
    }

    /// <summary>
    /// Sends an in-game chat message for the local player.
    /// </summary>
    /// <param name="message">The message content.</param>
    /// <param name="duration">The duration for which the message should be displayed.</param>
    /// <param name="fromName">The name of the sender (default is "SYSTEM").</param>
    /// <param name="fromPlayerId">The ID of the sender (default is 0 aka unused).</param>
    [LuaApiExport("SendIngameChatLocal")]
    public static void SendIngameChatLocal(string message, string fromName = "SYSTEM", int fromPlayerId = 0, int duration = 20)
    {
        LogHelper.Verbose("Sending ingame chat msg for local");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            MainViewModel.instance.HUDMPChatMessages.recieveIngameChat(fromName, fromPlayerId, message, duration);
        });
    }

    /// <summary>
    /// Sends an in-game chat message to a specific list of recipients.
    /// </summary>
    /// <param name="recipients">A Lua table (array) of player IDs who should receive the message.</param>
    /// <param name="message">The message content.</param>
    [LuaApiExport("SendIngameChat")]
    public static void SendIngameChat(LuaTable recipients, string message)
    {
        LogHelper.Verbose("Sending ingame chat msg");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameNetworkAPI.SendIngameChat(recipients.ToList<int>(), message);
        });
    }

    /// <summary>
    /// Sends a pre-defined in-game "insult" or taunt to a specific list of recipients.
    /// </summary>
    /// <param name="recipients">A Lua table (array) of player IDs who should receive the insult.</param>
    /// <param name="insult">The ID of the insult to send.</param>
    [LuaApiExport("SendIngameChatInsult")]
    public static void SendIngameChatInsult(LuaTable recipients, int insult)
    {
        LogHelper.Verbose("Sending ingame chat insult");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            GameNetworkAPI.SendIngameChatInsult(recipients.ToList<int>(), insult);
        });
    }

    /// <summary>
    /// Sends a Remote Procedure Call (RPC) packet to all other players.
    /// </summary>
    /// <param name="functionName">The name of the global Lua function to be called on receiving clients.</param>
    /// <param name="functionArguments">An optional Lua table of arguments to pass to the function.</param>
    /// <param name="instantMessage">If true, sends the packet immediately, bypassing the game's normal network queue.</param>
    [LuaApiExport("SendRpcToAll")]
    public static void SendRpcToAll(string functionName, LuaTable? functionArguments, bool instantMessage = false)
    {
        LogHelper.Verbose($"Sending RPC to all players, func={functionName}");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment())
                return;

            GameNetworkAPI.SendPacketToAll(new LuaNetworkRPCPacket()
            {
                FromPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                FunctionName = functionName,
                FunctionArgsTable = functionArguments?.ToDictionary<string, string>() ?? null
            }, (short)CustomNetworkPacketType.LuaRPC, instantMessage);
        });
    }

    /// <summary>
    /// Sends a Remote Procedure Call (RPC) packet to a specific player.
    /// </summary>
    /// <param name="playerId">The ID of the target player.</param>
    /// <param name="functionName">The name of the global Lua function to be called on the receiving client.</param>
    /// <param name="functionArguments">An optional Lua table of arguments to pass to the function.</param>
    [LuaApiExport("SendRpcToPlayerId")]
    public static void SendRpcToPlayerId(int playerId, string functionName, LuaTable? functionArguments)
    {
        LogHelper.Verbose($"Sending RPC to playerid={playerId}, func={functionName}");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment())
                return;

            GameNetworkAPI.SendPacketToPlayerId(playerId, new LuaNetworkRPCPacket()
            {
                FromPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                FunctionName = functionName,
                FunctionArgsTable = functionArguments?.ToDictionary<string, string>() ?? null
            }, (short)CustomNetworkPacketType.LuaRPC);
        });
    }

    /// <summary>
    /// Sends a generic data table to all other players.
    /// </summary>
    /// <param name="table">The Lua table to send. It will be serialized and can be received via the "OnReceiveTablePacket" hook.</param>
    /// <param name="instantMessage">If true, sends the packet immediately, bypassing the game's normal network queue.</param>
    [LuaApiExport("SendTableToAll")]
    public static void SendTableToAll(LuaTable table, bool instantMessage = false)
    {
        LogHelper.Verbose("Sending table to all players");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment())
                return;

            GameNetworkAPI.SendPacketToAll(new LuaNetworkTablePacket()
            {
                FromPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                PacketTable = table.ToDictionary<string, string>()
            }, (short)(CustomNetworkPacketType.LuaTable), instantMessage);
        });
    }

    /// <summary>
    /// Sends a generic data table to a specific player.
    /// </summary>
    /// <param name="playerId">The ID of the target player.</param>
    /// <param name="table">The Lua table to send. It will be serialized and can be received via the "OnReceiveTablePacket" hook.</param>
    /// <param name="instantMessage">If true, sends the packet immediately, bypassing the game's normal network queue.</param>
    [LuaApiExport("SendTableToPlayerId")]
    public static void SendTableToPlayerId(int playerId, LuaTable table, bool instantMessage = false)
    {
        LogHelper.Verbose($"Sending table to playerid={playerId}");
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (!GameNetworkAPI.IsNetworkedEnvironment())
                return;

            GameNetworkAPI.SendPacketToPlayerId(playerId, new LuaNetworkTablePacket()
            {
                FromPlayerId = GamePlayerManagerAPI.Instance.GetLocalPlayerId(),
                PacketTable = table.ToDictionary<string, string>()
            }, (short)(CustomNetworkPacketType.LuaTable));
        });
    }
}
