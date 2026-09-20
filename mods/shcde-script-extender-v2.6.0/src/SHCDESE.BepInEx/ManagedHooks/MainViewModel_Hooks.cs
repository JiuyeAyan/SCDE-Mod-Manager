using CrusaderDE;
using MonoMod.RuntimeDetour;
using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.Reflection;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // MainViewModel
    //
    internal Hook? mainViewModel_GetVersionString_hook;
    internal string MainViewModel_GetVersionString_Hook(MainViewModel instance)
    {
        return $"{MiscAPI.Instance.GetGameVersion()}: [SE: {Assembly.GetExecutingAssembly().GetName().Version}]";
    }

    internal static ManagedDetour<mainViewModel_SetVisibleState_Delegate> mainViewModel_SetVisibleState_hook;
    internal delegate void mainViewModel_SetVisibleState_Delegate(MainViewModel instance, bool state);
    internal void MainViewModel_SetVisibleState_Hook(MainViewModel instance, bool state)
    {
        mainViewModel_SetVisibleState_hook.Trampoline(instance, state);
        try
        {
            EventAPI.PlayerR3EventHooks.OnToggleIngameMenuVisibility.Raise(new EventAPI.Player.PlayerToggleIngameMenuVisibilityEventArgs(EventAPI.EventHookPhase.Post, state));
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during hide event");
        }
    }

    internal static ManagedDetour<mainViewModel_getAIFace_Delegate> mainViewModel_getAIFace_hook;
    internal delegate Noesis.ImageSource mainViewModel_getAIFace_Delegate(MainViewModel instance, int ai, bool allowExtendedRemapping = false);
    internal Noesis.ImageSource MainViewModel_GetAIFace_Hook(MainViewModel instance, int ai, bool allowExtendedRemapping = false)
    {
        // ai = Enums.AILords
        Noesis.ImageSource result = mainViewModel_getAIFace_hook.Trampoline(instance, ai, allowExtendedRemapping);

        try
        {
            Enums.AILords lord = (Enums.AILords)ai;
            if (!GameAIManagerAPI.Instance.GetSlotIndexByExtendedLordEnum(lord, out int internalLordId))
                return result;

            string lordName = GameAIManagerAPI.Instance.GetGenericSlotLordName(internalLordId);
            if (!GameAIManagerAPI.Instance.IsSupportedCustomLord(lordName))
                return result;

            if (GameAIManagerAPI.Instance.TryGetFace(lordName, out Noesis.ImageSource? imageSource) && imageSource != null)
            {
                return imageSource;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during getAIFace execution");
        }
        return result;
    }

}
