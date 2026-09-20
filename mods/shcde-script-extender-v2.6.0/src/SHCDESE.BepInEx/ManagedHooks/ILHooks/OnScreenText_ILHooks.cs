using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.GameGlobals;
using SHCDESE.Interop.Enums;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // OnScreenText
    //
    internal ILHook? onScreenText_UpdateOST_ilhook;
    public void OnScreenText_UpdateOST_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        ILLabel skipLabel = c.DefineLabel();

        // Find jump target
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Callvirt),
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Ldc_I4_S)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.MarkLabel(skipLabel);
        c.Index = 0;

        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Ldloc_2),
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Ldc_I4_0),
            x => x.Match(OpCodes.Ble_S)))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }

        c.Emit(OpCodes.Ldloc_2);
        c.EmitDelegate(static (OnScreenText.OST ost) =>
        {
            try
            {
                // msg=221
                // data1=221
                // data2=992
                LogHelper.Debug($"msg={ost.message}, data1={ost.data1}, data2={ost.data2}, data3={ost.data3}, data4={ost.data4}, data5={ost.data5}");
                if (GameAIManagerAPI.Instance.TryGetMessageTypeFromIndex(ost.data2, out AILordMessageType msgType))
                {
                    int playerId = GameGlobalsManager.LastMessageFromCharacterCached;
                    string lordName = GameAIManagerAPI.Instance.GetCustomAILordNameByPlayerId(playerId).ToLowerInvariant();
                    if (!GameAIManagerAPI.Instance.IsSupportedCustomLord(lordName))
                        return false;

                    if (!GameAIManagerAPI.Instance.TryGetCurrentSpeechText(lordName, out string? currentSpeech))
                        return false;

                    MainViewModel.instance.OST_Message_Bar_Text = currentSpeech;
                    return true;
                }
            } 
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Exception");
            }
            return false;
        });
        c.Emit(OpCodes.Brtrue, skipLabel);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
