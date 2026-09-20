using SHCDESE.Logging;
using SHCDESE.NoesisUtil;
using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Input;

namespace SHCDESE.ViewModels;

public class MainMenuModLogoEntry : INotifyPropertyChanged
{
    public string IconPath { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    // Mod information for tooltip
    public string ModName { get; set; }
    public string AuthorLabel { get; set; }
    public string Version { get; set; }
    public string Description { get; set; }
    public string Website { get; set; }

    private string _updateAvailableText = string.Empty;

    /// <summary>Text displayed when this mod has a newer repository release.</summary>
    public string UpdateAvailableText
    {
        get => _updateAvailableText;
        internal set
        {
            if (_updateAvailableText == value)
                return;

            _updateAvailableText = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateAvailableText)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasUpdateAvailable)));
        }
    }

    // Helper properties for visibility
    public bool HasDescription => !string.IsNullOrWhiteSpace(Description);
    public bool HasWebsite => !string.IsNullOrWhiteSpace(Website);
    public bool HasUpdateAvailable => !string.IsNullOrWhiteSpace(UpdateAvailableText);

    // Command to open website
    public ICommand OpenWebsiteCommand { get; }

    public event PropertyChangedEventHandler PropertyChanged;

    public MainMenuModLogoEntry()
    {
        OpenWebsiteCommand = new RelayCommand(OpenWebsite, CanOpenWebsite);
    }

    private bool CanOpenWebsite()
    {
        return HasWebsite;
    }

    private void OpenWebsite()
    {
        if (!HasWebsite)
            return;

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = Website,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            LogHelper.Error($"Failed to open website: {ex.Message}");
        }
    }
}
