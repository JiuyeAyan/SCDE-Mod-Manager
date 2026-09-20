using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // FrontendMenus
    //
    internal static ManagedDetour<frontendMenus_ClearUIPanels_Delegate> frontendMenus_ClearUIPanels_hook;
    internal delegate void frontendMenus_ClearUIPanels_Delegate(bool frontEndState = true, bool logo = true, bool clearAvatarCache = true);
    internal static void FrontendMenus_ClearUIPanels_Hook(bool frontEndState = true, bool logo = true, bool clearAvatarCache = true)
    {
        bool logoPlaybackActive = false;
        try
        {
            //LogHelper.Verbose($"frontEndState={frontEndState}, logo={logo}");
            if (Plugin.Instance.ShowLogo.Value)
            {
                Plugin.ViewModel.IsSEMenuVisible = logo ? Noesis.Visibility.Visible : Noesis.Visibility.Collapsed;
                logoPlaybackActive = frontEndState && logo;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error during clear ui panels");
        }
        frontendMenus_ClearUIPanels_hook.Trampoline(frontEndState, logo, clearAvatarCache);

        try
        {
            // Run after the game changes frontend visibility.
            Plugin.ViewModel?.SetLogoVideoPlaybackActive(logoPlaybackActive);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while updating logo-video playback for frontend visibility");
        }
    }

    internal static ManagedDetour<frontendMenus_OpenFrontEndMenus_Delegate> frontendMenus_OpenFrontEndMenus_hook;
    internal delegate void frontendMenus_OpenFrontEndMenus_Delegate();
    internal static void FrontendMenus_OpenFrontEndMenus_Hook()
    {
        frontendMenus_OpenFrontEndMenus_hook.Trampoline();

        try
        {
            // ClearUIPanels runs near the start of OpenFrontEndMenus, before the main-menu panel and its storyboard are restored.
            // Resume once more after that setup is complete so Noesis cannot leave the nested MediaElement paused.
            if (Plugin.Instance.ShowLogo.Value)
                Plugin.ViewModel?.SetLogoVideoPlaybackActive(active: true);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error while resuming logo video after opening frontend menus");
        }
    }
}
