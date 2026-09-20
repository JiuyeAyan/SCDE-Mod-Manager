using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // NoesisGUI
    //

    internal static ManagedDetour<NoesisGUI_LoadComponent_Delegate> noesisGUI_LoadComponent_Hook;
    internal delegate void NoesisGUI_LoadComponent_Delegate(object component, string filename);
    internal void NoesisGUI_LoadComponent_Hook(object component, string filename)
    {
        noesisGUI_LoadComponent_Hook.Trampoline(component, filename);
        try
        {
            if (GameXAMLManagerAPI.Instance != null)
            {
                LogHelper.Debug($"Loading component: {component.GetType().Name} for filename: [{filename}]");
                GameXAMLManagerAPI.Instance.InjectBindings(component);
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception during Noesis GUI LoadComponent");
        }
    }
}
