using CrusaderDE;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Logging;
using System;
using System.Reflection;

namespace SHCDESE.ManagedHooks.ILHooks;

public partial class ILHooksManager
{
    //
    // HUD_InGameMenu: HUD_InGameMenu_ButtonIngameMenuFunction_ILHook
    //
    internal ILHook? hud_inGameMenu_ilhook;
    internal Mono.Cecil.MethodReference? _foundMethodRef;
    internal Hook? hud_inGameMenu_buttonIngameFunction_exitMapYesActionHook;
    internal delegate void HUD_IngameMenu_ButtonIngameFunction_ExitMapYesDelegate(object e);
    internal HUD_IngameMenu_ButtonIngameFunction_ExitMapYesDelegate hud_inGameMenu_buttonIngameFunction_exitMapYesActionTramp;
    public void HUD_InGameMenu_ButtonIngameMenuFunction_ILHook(ILContext ctx)
    {
        ILCursor c = new ILCursor(ctx);

        if (!c.TryGotoNext(MoveType.After,
            x => x.Match(OpCodes.Ldloc_3),
            x => x.Match(OpCodes.Ldsfld),
            x => x.Match(OpCodes.Dup),
            x => x.Match(OpCodes.Brtrue_S),
            x => x.Match(OpCodes.Pop),
            x => x.Match(OpCodes.Ldsfld),
            x => x.MatchLdftn(out _foundMethodRef)))
        {
            LogHelper.Error($"Target not found!");
            return;
        }

        MethodBase onExitCompilerMethod = _foundMethodRef.ResolveReflection();
        hud_inGameMenu_buttonIngameFunction_exitMapYesActionHook = new Hook(onExitCompilerMethod, static (object e) =>
        {
            try
            {
                // We need to do this, since the game doesnt actually unload the active map from the map natively
                // but for our case, it would be advantageous to think it has been unloaded.
                MapLoaderR3EventHooks.OnUnloadMap.Raise(new MapUnloadEventArgs(EventHookPhase.Pre, IntPtr.Zero));
            } 
            catch (Exception ex)
            {
                LogHelper.Error(ex, "Error while unloading map");
            }
            ILHooksManager.Instance.hud_inGameMenu_buttonIngameFunction_exitMapYesActionTramp(e);
        });
        hud_inGameMenu_buttonIngameFunction_exitMapYesActionTramp = hud_inGameMenu_buttonIngameFunction_exitMapYesActionHook.GenerateTrampoline<HUD_IngameMenu_ButtonIngameFunction_ExitMapYesDelegate>();
    }

}
