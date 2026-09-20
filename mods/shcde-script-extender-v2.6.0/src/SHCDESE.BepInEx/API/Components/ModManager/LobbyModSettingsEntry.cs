using BepInEx;
using Noesis;
using SHCDESE.Logging;
using System.IO;

namespace SHCDESE.API.Components.ModManager;

public class LobbyModSettingsEntry
{
    public BaseUnityPlugin Plugin { get; }
    public string Name { get; }
    public string OptionBannerPath { get; }
    public object ViewModel { get; }
    public FrameworkElement View { get; }

    public LobbyModSettingsEntry(BaseUnityPlugin plugin, string name, object vm, FrameworkElement view)
    {
        Plugin = plugin;
        Name = name;
        OptionBannerPath = FindOptionBanner(plugin);
        ViewModel = vm;
        View = view;
    }

    /// <summary>
    /// Resolves the optional Override/Assets/GUI/Sprites/%MOD_GUID%_option_banner.png banner path.
    /// </summary>
    private static string FindOptionBanner(BaseUnityPlugin plugin)
    {
        string guid = plugin.Info.Metadata.GUID;
        if (string.IsNullOrWhiteSpace(guid))
            return string.Empty;

        string fileName = guid + "_option_banner.png";
        string relativePath = "Assets/GUI/Sprites/" + fileName;

        bool indexed = GameAssetManagerAPI.Instance.GetModifiedFilePath(relativePath, out _);
        if (!indexed)
        {
            LogHelper.Verbose($"Option banner for plugin {plugin.Info.Metadata.Name} ({guid}) not found in indexed assets.");
        }

        string? pluginDirectory = System.IO.Path.GetDirectoryName(plugin.Info.Location);
        bool existsBesidePlugin = pluginDirectory != null && File.Exists(System.IO.Path.Combine(pluginDirectory, "Override", "Assets", "GUI", "Sprites", fileName));

        return indexed || existsBesidePlugin ? "/Assets/GUI/Sprites/" + System.IO.Path.GetFileNameWithoutExtension(fileName) : string.Empty;
    }
}
