using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // HUD_MPChatMessages
    //
    internal ManagedDetour<hud_MpChatMessages_ReceiveIngameChatDelegate> hud_MpChatMessages_ReceiveIngameChat_hook;
    internal delegate void hud_MpChatMessages_ReceiveIngameChatDelegate(HUD_MPChatMessages instance, string fromName, int fromPlayerID, string message, int duration = 20);
    internal void HUD_MPChatMessages_ReceiveIngameChat_Hook(HUD_MPChatMessages instance, string fromName, int fromPlayerID, string message, int duration = 20)
    {
        try
        {
            GameNetworkAPI.Instance.HandleInGameChatMessage(fromName, fromPlayerID, message, duration);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during in-game chat handling");
        }
        hud_MpChatMessages_ReceiveIngameChat_hook.Trampoline(instance, fromName, fromPlayerID, message, duration);
    }

}
