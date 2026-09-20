using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    /// <summary>
    /// This hook makes it possible to have a multiplayer game with only AIs (aside from the player himself)
    /// </summary>
    internal ILHook? front_multiplayer_update;
    public void FRONT_Multiplayer_Update_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.After,
            x => x.Match(OpCodes.Sub),
            x => x.Match(OpCodes.Ldloc_S),
            x => x.Match(OpCodes.Bne_Un_S),
            x => x.Match(OpCodes.Ldc_I4_0),
            x => x.Match(OpCodes.Stloc_S)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }
        c.Index -= 2;
        c.Remove();
        c.Emit(OpCodes.Ldc_I4_1);

        LogHelper.Verbose($"IL={ctx.ToString()}");
    }

    /// <summary>
    /// This hook introduces some logic changes for the button event handler in the FRONT_Multiplayer XAML View.
    /// - Custom AI Join sound support (Asset API)
    /// - Custom AI Leave sound support (Asset API)
    /// </summary>
    internal ILHook? front_multiplayer_buttonclicked;
    public void FRONT_Multiplayer_ButtonClicked_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.After,
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Ldloc_S),
            x => x.Match(OpCodes.Ldloc_S),
            x => x.Match(OpCodes.Callvirt),
            x => x.Match(OpCodes.Stloc_S)))
        {
            LogHelper.Error($"Target 1 not found!");
            return;
        }

        // Join
        VariableDefinition memberLocal = ctx.Body.Variables[58];
        c.Emit(OpCodes.Ldarg_0);
        c.Emit(OpCodes.Ldloc_S, memberLocal);
        c.EmitDelegate(static (FRONT_Multiplayer instance, Platform_Multiplayer.MPLobbyMember member) =>
        {
            try
            {
                if (MyAudioManager.Instance.isSpeechPlaying(3))
                    return;

                if (GameAIManagerAPI.Instance.IsSupportedCustomLord(member.customLordName) && (GameAIManagerAPI.Instance.TryGetJoinAudio(member.customLordName, out string? customAudioPath)))
                {
                    LogHelper.Debug($"Member: {member.customLordName}, customAudio: [{customAudioPath}]");
                    MyAudioManager.Instance.PlaySpeech(3, "*", customAudioPath, true, false, false, false);
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Exception during on join logic for custom ai");
            }
        });
        c.Index = 0;
        
        if (!c.TryGotoNext(MoveType.Before,
            x => x.Match(OpCodes.Brfalse_S),
            x => x.Match(OpCodes.Call),
            x => x.Match(OpCodes.Ldc_I4_3),
            x => x.Match(OpCodes.Callvirt),
            x => x.Match(OpCodes.Brtrue_S),
            x => x.Match(OpCodes.Ldloc_S),
            x => x.Match(OpCodes.Ldfld),
            x => x.Match(OpCodes.Call)))
        {
            LogHelper.Error($"Target 2 not found!");
            return;
        }

        // Leave
        VariableDefinition memberLocal2 = ctx.Body.Variables[15];
        c.Emit(OpCodes.Ldarg_0);
        c.Emit(OpCodes.Ldloc_S, memberLocal2);
        c.EmitDelegate(static (FRONT_Multiplayer instance, Platform_Multiplayer.MPLobbyMember member) =>
        {
            try
            {
                if (MyAudioManager.Instance.isSpeechPlaying(3))
                    return;

                if (GameAIManagerAPI.Instance.IsSupportedCustomLord(member.customLordName) && (GameAIManagerAPI.Instance.TryGetLeaveAudio(member.customLordName, out string? customAudioPath)))
                {
                    LogHelper.Debug($"Member: {member.customLordName}, customAudio: [{customAudioPath}]");
                    MyAudioManager.Instance.PlaySpeech(3, "*", customAudioPath, true, false, false, false);
                }
            } 
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Exception during on leave logic for custom ai");
            }
        });
        
        LogHelper.Verbose($"IL={ctx.ToString()}");
    }
}
