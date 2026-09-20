using SHCDESE.API;
using SHCDESE.API.Components.Workshop;
using SHCDESE.Logging;
using Steamworks;
using System;
using System.Collections.Generic;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // Platform_Workshop
    //
    internal ManagedDetour<platform_Workshop_GetListOfSubscribedItemsPaths_Delegate> platform_Workshop_GetlistOfSubscribedItemPaths_hook;
    internal delegate List<string> platform_Workshop_GetListOfSubscribedItemsPaths_Delegate(Platform_Workshop instance);

    /// <summary>
    /// Hook that intercepts GetListOfSubscribedItemsPaths to check for and download updates
    /// </summary>
    internal List<string> Platform_Workshop_GetListOfSubscribedItemsPaths_Hook(Platform_Workshop instance)
    {
        List<string> result = null!;

        try
        {
            result = platform_Workshop_GetlistOfSubscribedItemPaths_hook!.Trampoline(instance);

            WorkshopUpdateManager.Instance.CheckAndDownloadUpdatesInitial(result);
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during workshop update check");

            // Return original result even if update check fails
            if (result == null)
            {
                result = platform_Workshop_GetlistOfSubscribedItemPaths_hook!.Trampoline(instance);
            }
        }

        return result;
    }
}
