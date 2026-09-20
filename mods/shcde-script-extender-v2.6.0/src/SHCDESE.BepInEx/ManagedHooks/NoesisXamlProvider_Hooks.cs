using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.IO;
using System.Text;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // NoesisXamlProvider
    //
    internal ManagedDetour<noesisXamlProvider_loadXamlDelegate> noesisXamlProvider_loadXaml_hook;
    internal delegate Stream noesisXamlProvider_loadXamlDelegate(NoesisXamlProvider instance, Uri uri);
    internal Stream NoesisXamlProvider_LoadXaml(NoesisXamlProvider instance, Uri uri)
    {
        try
        {
            if (GameAssetManagerAPI.Instance.GetModifiedFileTextContent(uri.ToString(), out string xaml))
            {
                LogHelper.Debug($"Loading local: {uri}");
                LogHelper.Verbose(xaml);
                return new MemoryStream(Encoding.UTF8.GetBytes(xaml));
            }
            LogHelper.Debug($"Loading original: {uri}");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error during noesis xml load");
        }
        return noesisXamlProvider_loadXaml_hook.Trampoline(instance, uri);
    }

}
