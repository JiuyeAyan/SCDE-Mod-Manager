using CrusaderDE;
using SHCDESE.API;
using SHCDESE.Interop;
using SHCDESE.Logging;
using System;
using System.Reflection;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // FatControler
    //
    internal ManagedDetour<fatControler_noesisGuiUpdateChecksInGameDelegate> fatControler_noesisGuiUpdateChecksInGame_hook;
    internal delegate void fatControler_noesisGuiUpdateChecksInGameDelegate(FatControler instance);
    internal void FatControler_NoesisGUIUpdateChecksInGame_Hook(FatControler instance)
    {
        fatControler_noesisGuiUpdateChecksInGame_hook.Trampoline(instance);
        try
        {
            // Monk-availability
            bool monkAvailable = GamePlayerManagerAPI.Instance.IsUnitAllowed(eChimps.CHIMP_TYPE_MONK);
            MainViewModel.Instance.HUDBuildingPanel.RefRecruitMonkButton.Visibility = monkAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
            //MainViewModel.Instance.HUDBuildingPanel.RefCathedralNoGoldMessage.Visibility = monkAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;

            // Engineer-availability
            bool engineerAvailable = GamePlayerManagerAPI.Instance.IsUnitAllowed(eChimps.CHIMP_TYPE_ENGINEER);
            MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButton.Visibility = engineerAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
            MainViewModel.Instance.HUDBuildingPanel.RefRecruitEngineerButtonX.Visibility = engineerAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
            
            // Ladder-availability
            bool ladderAvailable = GamePlayerManagerAPI.Instance.IsUnitAllowed(eChimps.CHIMP_TYPE_LADDERMAN);
            MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButton.Visibility = ladderAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
            MainViewModel.Instance.HUDBuildingPanel.RefRecruitLaddermanButtonX.Visibility = ladderAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;

            // Tunneler-availability
            bool tunnelerAvailable = GamePlayerManagerAPI.Instance.IsUnitAllowed(eChimps.CHIMP_TYPE_TUNNELER);
            MainViewModel.Instance.HUDBuildingPanel.RefRecruitTunellerButton.Visibility = tunnelerAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
            //MainViewModel.Instance.HUDBuildingPanel.RefTunnllersGuildNoGoldMessage.Visibility = tunnelerAvailable ? Noesis.Visibility.Visible : Noesis.Visibility.Hidden;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during noesis gui update checks");
        }
    }
}