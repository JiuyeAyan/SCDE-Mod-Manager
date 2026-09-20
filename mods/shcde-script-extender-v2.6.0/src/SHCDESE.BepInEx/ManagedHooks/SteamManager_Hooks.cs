using SHCDESE.EventAPI;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // Platform_Workshop
    //
    internal ManagedDetour<steamManager_Awake_Delegate> steamManager_Awake_hook;
    internal delegate void steamManager_Awake_Delegate(SteamManager instance);

    /// <summary>
    /// Hook that allows a callback for when steam becomes initialized
    /// </summary>
    internal void SteamManager_Awake_Hook(SteamManager instance)
    {
        try
        {
            SteamworksR3EventHooks.OnSteamworksInitialized.Raise(new EventAPI.Steamworks.SteamworksInitializedEventArgs(EventHookPhase.Pre));
            steamManager_Awake_hook!.Trampoline(instance);
            SteamworksR3EventHooks.OnSteamworksInitialized.Raise(new EventAPI.Steamworks.SteamworksInitializedEventArgs(EventHookPhase.Post));
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error steamworks init");
        }
    }
}
