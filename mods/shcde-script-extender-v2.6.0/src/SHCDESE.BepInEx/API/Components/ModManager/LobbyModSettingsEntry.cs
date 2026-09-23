using BepInEx;
using Noesis;
using SHCDESE.Logging;
using System.IO;

namespace SHCDESE.API.Components.ModManager;

public class LobbyModSettingsEntry
{
    public BaseUnityPlugin Plugin { get; }
    public string Name { get; }

    /// <summary>The optional image displayed while this tab is selected.</summary>
    public string OptionBannerPath { get; }

    /// <summary>The optional image displayed behind this tab at all times.</summary>
    public string OptionBannerBackgroundPath { get; }
    public object ViewModel { get; }
    public FrameworkElement View { get; }

    public LobbyModSettingsEntry(BaseUnityPlugin plugin, string name, object vm, FrameworkElement view)
    {
        Plugin = plugin;
        Name = name;
        OptionBannerPath = FindOptionBanner(plugin, "_option_banner.png");
        OptionBannerBackgroundPath = FindOptionBanner(plugin, "_option_banner_bg.png");
        ViewModel = vm;
        View = view;
    }

    /// <summary>
    /// Resolves an optional option-banner asset beneath Override/Assets/GUI/Sprites.
    /// </summary>
    private static string FindOptionBanner(BaseUnityPlugin plugin, string fileNameSuffix)
    {
        string guid = plugin.Info.Metadata.GUID;
        if (string.IsNullOrWhiteSpace(guid))
            return string.Empty;

        string fileName = guid + fileNameSuffix;
        string relativePath = "Assets/GUI/Sprites/" + fileName;

        bool indexed = GameAssetManagerAPI.Instance.GetModifiedFilePath(relativePath, out _);
        if (!indexed)
        {
            LogHelper.Verbose($"Option banner asset {fileName} for plugin {plugin.Info.Metadata.Name} ({guid}) not found in indexed assets.");
        }

        string? pluginDirectory = System.IO.Path.GetDirectoryName(plugin.Info.Location);
        bool existsBesidePlugin = pluginDirectory != null && File.Exists(System.IO.Path.Combine(pluginDirectory, "Override", "Assets", "GUI", "Sprites", fileName));

        return indexed || existsBesidePlugin ? "/Assets/GUI/Sprites/" + System.IO.Path.GetFileNameWithoutExtension(fileName) : string.Empty;
    }
}
