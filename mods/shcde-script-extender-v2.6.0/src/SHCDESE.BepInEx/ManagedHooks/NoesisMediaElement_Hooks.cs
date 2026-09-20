using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // MediaElement
    //

    internal ManagedDetour<noesisMediaElement_SetterDelegate> noesisMediaElement_Setter_hook;
    internal delegate void noesisMediaElement_SetterDelegate(NoesisApp.MediaElement instance, Uri value);
    internal void NoesisMediaElement_Setter_Hook(NoesisApp.MediaElement instance, Uri value)
    {
        Uri finalUri = value;

        try
        {
            if (value != null)
            {
                // Strip leading slashes
                string path = value.OriginalString.TrimStart('/', '\\');

                // Try resolve mod override first
                if (GameAssetManagerAPI.Instance.TryResolveVideoPath(path, out string absoluteOverridePath))
                {
                    LogHelper.Information($"Redirecting [{path}] to [{absoluteOverridePath}]");
                    finalUri = new Uri(absoluteOverridePath, UriKind.Absolute);
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception in MediaElement setter hook");
        }
        noesisMediaElement_Setter_hook.Trampoline(instance, finalUri);
    }
}
