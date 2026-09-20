using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using Serilog;
using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Logging;
using Steamworks;
using System;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // Platform_Multiplayer: ProcessMessage
    // This hook makes it possible to intercept packets and allow lua to send and receive custom ones
    //
    internal ILHook? platform_multiplayer_processMessage;
    internal void Platform_Multiplayer_ProcessMessage_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        //
        // Custom Packet Support
        //
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Switch),
            x => x.Match(OpCodes.Br),
            x => x.Match(OpCodes.Ldarg_3),
            x => x.Match(OpCodes.Brfalse_S),
            x => x.Match(OpCodes.Ldc_I4_0)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }

        // This static delegate works like the following:
        // If returns true: the packet has been handled, and the method should return as well (caller)
        // on false: this is a packet that we arent interested in.
        c.Emit(OpCodes.Ldarg_1);
        c.Emit(OpCodes.Ldarg_2);
        c.EmitDelegate(static (Platform_Multiplayer.MPData data, Platform_Multiplayer.MPGameMember? fromMember) =>
        {
            Log.Verbose($"Platform_Multiplayer_ProcessMessage_ILHook: data={data.data.Length} bytes");
            CustomNetworkPacketType packetType = (CustomNetworkPacketType)data.packetType;

            if (data.packetType < (short)CustomNetworkPacketType.CustomPacketStart)
            {
                return false; // Not a custom packet, let the game handle it.
            }

            try
            {
                CSteamID? sender = fromMember != null ? (CSteamID)fromMember.steamID : null;
                return GameNetworkAPI.Instance.HandleRawPacket(data.packetType, data.data, sender);
            } 
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error during packet deserialization, srcType={packetType}");
            }
            return false;
        });
        ILLabel labelContinue = c.DefineLabel();
        c.Emit(OpCodes.Brfalse, labelContinue);
        c.Emit(OpCodes.Pop);
        c.Emit(OpCodes.Ldc_I4_1);
        c.Emit(OpCodes.Ret);
        c.MarkLabel(labelContinue);
    }

    //
    // Game Lobby Isolation
    //
    internal ILHook? platform_multiplayer_CreateLobbyResult;
    internal void Platform_Multiplayer_CreateLobbyResult_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.After,
           x => x.Match(OpCodes.Ldloc_1),
           x => x.Match(OpCodes.Ldstr),
           x => x.Match(OpCodes.Ldstr),
           x => x.Match(OpCodes.Call),
           x => x.Match(OpCodes.Pop),
           x => x.Match(OpCodes.Ldarg_0)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.Emit(OpCodes.Ldloc_1);
        c.EmitDelegate(static (CSteamID cSteamId) => {
            Log.Information("Platform_Multiplayer_CreateLobbyResult_ILHook: Lobby created");
            SteamMatchmaking.SetLobbyData(cSteamId, GameNetworkAPI.LOBBY_IDENTIFIER_TOKEN, "true");

            string modHash = GameNetworkAPI.ComputeActiveModHash();
            SteamMatchmaking.SetLobbyData(cSteamId, GameNetworkAPI.LOBBY_MOD_HASH_TOKEN, modHash);
            Log.Information($"Platform_Multiplayer_CreateLobbyResult_ILHook: Set mod hash: {modHash}");

            string modMetadata = GameNetworkAPI.SerializeActiveModMetadata();
            if (!string.IsNullOrEmpty(modMetadata)
                && !SteamMatchmaking.SetLobbyData(cSteamId, GameNetworkAPI.LOBBY_MOD_LIST_TOKEN, modMetadata))
            {
                Log.Warning("Platform_Multiplayer_CreateLobbyResult_ILHook: Failed to set active mod metadata");
            }

            string prevVersion = SteamMatchmaking.GetLobbyData(cSteamId, "version");
            if (!string.IsNullOrEmpty(prevVersion))
            {
                SteamMatchmaking.SetLobbyData(cSteamId, "version", prevVersion + "_se");
            }
        });

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    internal ILHook? platform_multiplayer_JoinLobbyResult;
    internal void Platform_Multiplayer_JoinLobbyResult_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
           x => x.Match(OpCodes.Ldstr),
           x => x.Match(OpCodes.Ldloc_1),
           x => x.Match(OpCodes.Ldstr),
           x => x.Match(OpCodes.Call),
           x => x.Match(OpCodes.Call),
           x => x.Match(OpCodes.Brfalse)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        string prevVersion = (string)ctx.Instrs[c.Index].Operand;
        ctx.Instrs[c.Index].Operand = prevVersion + "_se";

        // Combine the game's version check with the detailed mod compatibility check.
        // A false result follows the game's existing leave-lobby path.
        c.Index += 5;
        c.Emit(OpCodes.Ldloc_1);
        c.EmitDelegate(static (CSteamID lobbyId) => GameNetworkAPI.CanJoinLobby(lobbyId));
        c.Emit(OpCodes.And);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    internal ILHook? platform_multiplayer_GetLobbies;
    internal void Platform_Multiplayer_GetLobbies_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.Before,
           x => x.Match(OpCodes.Ldstr),
           x => x.Match(OpCodes.Ldc_I4_0),
           x => x.Match(OpCodes.Call),
           x => x.Match(OpCodes.Ldarg_1),
           x => x.Match(OpCodes.Ldc_I4_1)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        string prevVersion = (string)ctx.Instrs[c.Index].Operand;
        ctx.Instrs[c.Index].Operand = prevVersion + "_se";

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    internal ILHook? platform_multiplayer_RequestLobbyListResult;
    internal void Platform_Multiplayer_RequestLobbyListResult_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        // Get false label target
        ILLabel falseLabel = ctx.DefineLabel();
        if (!c.TryGotoNext(MoveType.Before,
           x => x.Match(OpCodes.Ldloc_1),
           x => x.Match(OpCodes.Ldc_I4_1),
           x => x.Match(OpCodes.Add),
           x => x.Match(OpCodes.Stloc_1),
           x => x.Match(OpCodes.Ldloc_1)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.MarkLabel(falseLabel);
        c.Index = 0;

        // Check #2
        if (!c.TryGotoNext(MoveType.Before,
           x => x.Match(OpCodes.Brfalse_S),
           x => x.Match(OpCodes.Ldloc_0),
           x => x.Match(OpCodes.Ldloc_3),
           x => x.Match(OpCodes.Callvirt),
           x => x.Match(OpCodes.Ldloc_1)))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }
        //c.Emit(OpCodes.Pop);
        c.Emit(OpCodes.Ldloc_3);
        c.EmitDelegate(static (Platform_Multiplayer.MPLobby lobby) =>
        {
            if (string.IsNullOrEmpty(SteamMatchmaking.GetLobbyData(lobby.id, GameNetworkAPI.LOBBY_IDENTIFIER_TOKEN)))
            {
                Log.Debug($"RequestLobbyListResult: lobby {lobby.id} rejected (no SE token)");
                return false;
            }

            Log.Debug($"RequestLobbyListResult: lobby {lobby.id} accepted");
            return true;
        });
        c.Emit(OpCodes.And);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
