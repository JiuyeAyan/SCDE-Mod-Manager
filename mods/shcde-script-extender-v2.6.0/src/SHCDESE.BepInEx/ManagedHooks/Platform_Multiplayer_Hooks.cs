using SHCDESE.API;
using SHCDESE.API.Components.Network;
using SHCDESE.Logging;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // Platform_Multiplayer
    //
    internal ManagedDetour<platform_Multiplayer_ReceiveLobbyMessagesDelegate> platform_Multiplayer_receiveLobbyMessages_hook;
    internal delegate void platform_Multiplayer_ReceiveLobbyMessagesDelegate(Platform_Multiplayer instance);
    internal void Platform_Multiplayer_ReceiveLobbyMessages_Hook(Platform_Multiplayer instance)
    {
        try
        {
            // Drain channel 2 during the lobby phase, just like ReceiveGameMessages 
            // does during gameplay, but without the gameMembers != null guard.
            Platform_Multiplayer mp = Platform_Multiplayer.instance;
            if (mp == null || mp.activeLobby == null) return;

            IntPtr[] array = new IntPtr[200];
            int count = SteamNetworkingMessages.ReceiveMessagesOnChannel(2, array, array.Length);
            for (int i = 0; i < count; i++)
            {
                SteamNetworkingMessage_t msg = Marshal.PtrToStructure<SteamNetworkingMessage_t>(array[i]);
                byte[] raw = new byte[msg.m_cbSize];
                Marshal.Copy(msg.m_pData, raw, 0, raw.Length);
                CSteamID sender = msg.m_identityPeer.GetSteamID();
                SteamNetworkingMessage_t.Release(array[i]);

                Platform_Multiplayer.MPData data = Platform_Multiplayer.MPData.FromBytes(raw);
                if (data.packetType >= (short)CustomNetworkPacketType.CustomPacketStart)
                {
                    GameNetworkAPI.Instance.HandleRawPacket(data.packetType, data.data, sender);
                }
            }

            // Per-player updates can arrive before the host publishes its slot table. So annoyingly we have to do this (kinda)
            GameXAMLManagerAPI.Instance.FlushDeferredPerPlayerUpdates();
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during lobby message handling");
        }
        platform_Multiplayer_receiveLobbyMessages_hook.Trampoline(instance);
    }

    internal ManagedDetour<platform_Multiplayer_ChatHandleDelegate> platform_Multiplayer_ChatHandle_hook;
    internal delegate void platform_Multiplayer_ChatHandleDelegate(Platform_Multiplayer instance, string message, CSteamID Id);
    internal void Platform_Multiplayer_ChatHandle_Hook(Platform_Multiplayer instance, string message, CSteamID Id)
    {
        try
        {
            GameNetworkAPI.Instance.HandleLobbyChatMessage(message, Id);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during chat handling");
        }
        platform_Multiplayer_ChatHandle_hook.Trampoline(instance, message, Id);
    }

    internal ManagedDetour<platform_Multiplayer_SendLobbyChatMessageDelegate> platform_Multiplayer_SendLobbyChatMessage_hook;
    internal delegate void platform_Multiplayer_SendLobbyChatMessageDelegate(Platform_Multiplayer instance, string message);
    internal void Platform_Multiplayer_SendLobbyChatMessage_Hook(Platform_Multiplayer instance, string message)
    {
        try
        {
            GameNetworkAPI.Instance.HandleSendLobbyChatMessage(message);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during chat message sending");
        }
        platform_Multiplayer_SendLobbyChatMessage_hook.Trampoline(instance, message);
    }

    internal ManagedDetour<platform_Multiplayer_SendIngameChatDelegate> platform_Multiplayer_SendIngameChat_hook;
    internal delegate void platform_Multiplayer_SendIngameChatDelegate(Platform_Multiplayer instance, List<int> recipients, string message);
    internal void Platform_Multiplayer_SendIngameChat_Hook(Platform_Multiplayer instance, List<int> recipients, string message)
    {
        try
        {
            GameNetworkAPI.Instance.HandleSendInGameChatMessage(recipients, message);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during in-game chat message sending");
        }
        platform_Multiplayer_SendIngameChat_hook.Trampoline(instance, recipients, message);
    }

    internal ManagedDetour<platform_Multiplayer_SendCustomInfoToMemberDelegate> platform_Multiplayer_SendCustomInfoToMember_hook;
    internal delegate void platform_Multiplayer_SendCustomInfoToMemberDelegate(Platform_Multiplayer instance, Platform_Multiplayer.MPLobbyMember member);
    internal void Platform_Multiplayer_SendCustomInfoToMember_Hook(Platform_Multiplayer instance, Platform_Multiplayer.MPLobbyMember member)
    {
        try
        {
            GameNetworkAPI.Instance.HandleSendCustomInfoToMember(member);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during custom info sending");
        }
        platform_Multiplayer_SendCustomInfoToMember_hook.Trampoline(instance, member);
    }
}
