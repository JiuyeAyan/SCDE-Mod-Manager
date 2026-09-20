using CrusaderDE;
using SHCDESE.API;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.Buildings;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // HUD_Buildings
    //
    internal static ManagedDetour<HUD_Buildings_CTORDelegate> hud_Buildings_CTOR_hook;
    internal delegate void HUD_Buildings_CTORDelegate(HUD_Buildings instance);
    internal void HUD_Buildings_CTOR_Hook(HUD_Buildings instance)
    {
        hud_Buildings_CTOR_hook.Trampoline(instance);
        try
        {
            instance.RefBuildingZZZButtonOn.Click += RefBuildingZZZButtonOn_Click;
            instance.RefBuildingZZZButtonOff.Click += RefBuildingZZZButtonOff_Click;
        } 
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while executing initialize component");
        }
    }

    private static void RefBuildingZZZButtonOn_Click(object sender, Noesis.RoutedEventArgs args)
    {
        LogHelper.Debug($"Pause building button pressed");
        BuildingUIPauseEventArgs eventArgs = new(EventHookPhase.Pre);
        BuildingR3EventHooks.OnPause.Raise(eventArgs);
    }

    private static void RefBuildingZZZButtonOff_Click(object sender, Noesis.RoutedEventArgs args)
    {
        LogHelper.Debug($"Unpause building button pressed");
        BuildingUIUnpauseEventArgs eventArgs = new(EventHookPhase.Pre);
        BuildingR3EventHooks.OnUnpause.Raise(eventArgs);
    }
}
