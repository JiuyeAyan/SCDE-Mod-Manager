using Noesis;
using NoesisApp;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API;
using SHCDESE.Logging;
using SHCDESE.ManagedHooks.Callbacks;
using SHCDESE.NoesisUtil;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;

namespace SHCDESE.ViewModels;

/// <summary>
/// Represents the possible states of the update check.
/// </summary>
public enum UpdateCheckState
{
    Idle,
    Checking,
    UpToDate,
    UpdateAvailable,
    Failed
}

public class MainMenuViewModel : INotifyPropertyChanged, INoesisElementBindingAware, IDisposable
{
    // -------------------------------------------------------------------------
    // Visibility / Logo
    // -------------------------------------------------------------------------

    private Visibility _isSEMenuVisible = Visibility.Visible;

    public Visibility IsSEMenuVisible
    {
        get => _isSEMenuVisible;
        set
        {
            if (_isSEMenuVisible == value)
                return;

            _isSEMenuVisible = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSEMenuVisible)));
        }
    }

    /// <summary>The first animated logo frame, switched to its Christmas variant during December.</summary>
    public string LogoFrame1Source { get; }

    /// <summary>The second animated logo frame, switched to its Christmas variant during December.</summary>
    public string LogoFrame2Source { get; }

    /// <summary>The third animated logo frame, switched to its Christmas variant during December.</summary>
    public string LogoFrame3Source { get; }

    // -------------------------------------------------------------------------
    // video playlist / analog-video showcase
    // -------------------------------------------------------------------------

    private const string LOGO_VIDEO_ELEMENT_NAME = "SELogoVideo";
    private static readonly HashSet<string> SupportedLogoVideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".webm",
        ".mp4"
    };

    private readonly List<Uri> _logoVideos;
    private readonly Random _captionRandom = new Random();
    private readonly double _pokemonCaptionChance;
    private MediaElement? _logoVideoElement;
    private Uri? _logoVideoSource;
    private int _logoVideoIndex = -1;
    private int _consecutiveVideoFailures;
    private bool _videoDisabled;
    private bool _advanceScheduled;
    private bool _advanceAfterFailure;
    private bool _disposed;
    private Visibility _pokemonCaptionVisibility = Visibility.Collapsed;

    /// <summary>The current video URI. The Noesis MediaElement plays it automatically.</summary>
    public Uri? LogoVideoSource
    {
        get => _logoVideoSource;
        private set
        {
            if (Equals(_logoVideoSource, value))
                return;

            _logoVideoSource = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogoVideoSource)));
        }
    }

    /// <summary>Shows the video layer only while a usable playlist is available.</summary>
    public Visibility LogoVideoVisibility =>
        LogoVideoEnabled && !_videoDisabled && _logoVideos.Count > 0 ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Whether the decorative logo video player is enabled.</summary>
    public bool LogoVideoEnabled { get; }

    /// <summary>Whether the decorative logo videos should be muted.</summary>
    public bool LogoVideoMuted { get; }

    /// <summary>Whether the shader's static, distortion, and vignette stages are enabled.</summary>
    public bool LogoStaticEffectEnabled { get; }

    /// <summary>Whether the shader's coarse and fine scanline stages are enabled.</summary>
    public bool LogoScanlineEffectEnabled { get; }

    /// <summary>Output alpha for the shader-processed video.</summary>
    public float LogoVideoOpacity { get; }

    /// <summary>Returns true when a Noesis callback belongs to the dedicated logo player.</summary>
    internal bool OwnsLogoVideoElement(MediaElement? element) => LogoVideoEnabled && element != null && Equals(element, _logoVideoElement);

    /// <summary>Rare caption below the logo.</summary>
    public Visibility PokemonCaptionVisibility
    {
        get => _pokemonCaptionVisibility;
        private set
        {
            if (_pokemonCaptionVisibility == value)
                return;

            _pokemonCaptionVisibility = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PokemonCaptionVisibility)));
        }
    }

    public MainMenuViewModel() : this(false, string.Empty, true, true, true, 0.85, 0.03)
    {

    }

    public MainMenuViewModel(string logoVideos, bool logoVideoMuted, bool enableStaticEffect, bool enableScanlineEffect, double logoVideoOpacity, double pokemonCaptionChance)
        : this(true, logoVideos, logoVideoMuted, enableStaticEffect, enableScanlineEffect, logoVideoOpacity, pokemonCaptionChance)
    {

    }

    /// <summary>
    /// Creates the main-menu model and parses a semicolon/newline-separated logo-video playlist.
    /// </summary>
    public MainMenuViewModel(bool logoVideoEnabled, string logoVideos, bool logoVideoMuted, bool enableStaticEffect, bool enableScanlineEffect, double logoVideoOpacity, double pokemonCaptionChance)
    {
        bool christmasTime = IsChristmasTime(DateTime.Now);
        LogoFrame1Source = GetLogoFrameSource(1, christmasTime);
        LogoFrame2Source = GetLogoFrameSource(2, christmasTime);
        LogoFrame3Source = GetLogoFrameSource(3, christmasTime);

        LogoVideoEnabled = logoVideoEnabled;
        _logoVideos = LogoVideoEnabled ? ParseLogoVideos(logoVideos) : new List<Uri>();
        LogoVideoMuted = logoVideoMuted;
        LogoStaticEffectEnabled = enableStaticEffect;
        LogoScanlineEffectEnabled = enableScanlineEffect;
        LogoVideoOpacity = (float)Math.Max(0.0, Math.Min(1.0, logoVideoOpacity));
        _pokemonCaptionChance = Math.Max(0.0, Math.Min(1.0, pokemonCaptionChance));

        if (LogoVideoEnabled && _logoVideos.Count > 0)
            AdvanceLogoVideo();
        else if (LogoVideoEnabled)
            RollPokemonCaption();
    }

    internal static bool IsChristmasTime(DateTime date) => date.Month == 12;

    private static string GetLogoFrameSource(int frame, bool christmasTime)
    {
        string christmasSuffix = christmasTime ? "_c" : string.Empty;
        return $"/Assets/GUI/Sprites/selogo{christmasSuffix}_{frame}.png";
    }

    private static List<Uri> ParseLogoVideos(string configuredPaths)
    {
        List<Uri> videos = new List<Uri>();
        if (string.IsNullOrWhiteSpace(configuredPaths))
            return videos;

        string[] paths = configuredPaths.Split([';', '\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (string configuredPath in paths)
        {
            string path = configuredPath.Trim().Replace('\\', '/').TrimEnd('*');
            if (path.Length == 0)
                continue;

            string extension = System.IO.Path.GetExtension(path);
            if (!SupportedLogoVideoExtensions.Contains(extension))
            {
                LogHelper.Warning($"Skipping unsupported logo video [{path}]. Use .webm or .mp4.");
                continue;
            }

            if (!Uri.TryCreate(path, UriKind.RelativeOrAbsolute, out Uri? uri))
            {
                LogHelper.Warning($"Skipping invalid logo video URI [{path}].");
                continue;
            }

            videos.Add(uri);
        }

        return videos;
    }

    private void AdvanceLogoVideo()
    {
        if (_disposed || !LogoVideoEnabled || _videoDisabled || _logoVideos.Count == 0)
            return;

        _logoVideoIndex = (_logoVideoIndex + 1) % _logoVideos.Count;
        RefreshLogoVideoSource();
        ApplyLogoVideoSource();
        RollPokemonCaption();
    }

    private void RefreshLogoVideoSource()
    {
        if (_logoVideoIndex < 0 || _logoVideoIndex >= _logoVideos.Count)
            return;

        Uri configuredUri = _logoVideos[_logoVideoIndex];
        if (!configuredUri.IsAbsoluteUri && GameAssetManagerAPI.Instance.TryResolveVideoPath(configuredUri.OriginalString.TrimStart('/', '\\'), out string absolutePath))
        {
            LogoVideoSource = new Uri(absolutePath, UriKind.Absolute);
            return;
        }

        LogoVideoSource = configuredUri;
    }

    private void ApplyLogoVideoSource()
    {
        if (_logoVideoElement == null)
            return;

        NoesisCallbacks.Install(afterNoesisInitialization: true);
        _logoVideoElement.Source = LogoVideoSource;
        if (LogoVideoSource != null)
        {
            LogHelper.Debug($"Playing logo video [{_logoVideoIndex + 1}/{_logoVideos.Count}]: [{LogoVideoSource}]");
        }
    }

    private void RollPokemonCaption()
    {
        PokemonCaptionVisibility = _pokemonCaptionChance > 0.0 && _captionRandom.NextDouble() < _pokemonCaptionChance ? Visibility.Visible : Visibility.Collapsed;
    }

    internal bool IsConfiguredLogoVideoSource(Uri? source)
    {
        if (!LogoVideoEnabled || source == null)
            return false;

        string candidate = NormalizeLogoVideoUri(source);
        if (LogoVideoSource != null && string.Equals(candidate, NormalizeLogoVideoUri(LogoVideoSource), StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (Uri configuredVideo in _logoVideos)
        {
            string configured = NormalizeLogoVideoUri(configuredVideo);
            if (string.Equals(candidate, configured, StringComparison.OrdinalIgnoreCase) || candidate.EndsWith("/" + configured, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string NormalizeLogoVideoUri(Uri source)
    {
        string value = source.IsAbsoluteUri && source.IsFile ? source.LocalPath : source.OriginalString;
        return Uri.UnescapeDataString(value).Replace('\\', '/').TrimStart('/').TrimEnd('*');
    }

    internal bool ShouldLoopCurrentLogoVideo => LogoVideoEnabled && _logoVideos.Count <= 1;

    internal void NotifyLogoVideoOpened()
    {
        if (!LogoVideoEnabled)
            return;

        _consecutiveVideoFailures = 0;
    }

    internal void SetLogoVideoPlaybackActive(bool active)
    {
        if (_disposed || !LogoVideoEnabled || _videoDisabled || _logoVideoElement == null || LogoVideoSource == null)
            return;


        _logoVideoElement.LoadedBehavior = MediaState.Manual;
        if (active)
        {
            _logoVideoElement.Play();
        }
        else
        {
            _logoVideoElement.Pause();
        }
    }

    internal void NotifyLogoVideoEnded()
    {
        if (!LogoVideoEnabled)
            return;

        if (_logoVideos.Count == 1)
        {
            RollPokemonCaption();
            return;
        }

        AdvanceLogoVideoAfterMediaEvent(afterFailure: false);
    }

    internal void NotifyLogoVideoFailed(Exception error)
    {
        if (!LogoVideoEnabled)
            return;

        LogHelper.Warning($"Logo video failed [{LogoVideoSource}]: {error.Message}");
        AdvanceLogoVideoAfterMediaEvent(afterFailure: true);
    }

    private void AdvanceLogoVideoAfterMediaEvent(bool afterFailure)
    {
        _advanceAfterFailure |= afterFailure;
        if (_advanceScheduled)
            return;

        _advanceScheduled = true;
        try
        {
            if (_disposed)
                return;

            bool failed = _advanceAfterFailure;
            _advanceAfterFailure = false;

            if (failed)
            {
                _consecutiveVideoFailures++;
                if (_consecutiveVideoFailures >= _logoVideos.Count)
                {
                    LogHelper.Warning("All configured logo videos failed; disabling the logo video layer for this session.");
                    _videoDisabled = true;
                    LogoVideoSource = null;
                    ApplyLogoVideoSource();
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LogoVideoVisibility)));
                    return;
                }
            }

            LogHelper.Debug($"Executing deferred logo playlist advance (current={_logoVideoIndex + 1}/{_logoVideos.Count}, failure={failed}).");
            AdvanceLogoVideo();
        }
        finally
        {
            _advanceScheduled = false;
        }
    }

    void INoesisElementBindingAware.OnNoesisElementBound(FrameworkElement element)
    {
        if (_disposed || !LogoVideoEnabled)
            return;

        MediaElement? mediaElement = GameXAMLManagerAPI.Instance.FindElementByName(element, LOGO_VIDEO_ELEMENT_NAME) as MediaElement;

        if (Equals(mediaElement, _logoVideoElement))
            return;

        DetachLogoVideoElement();
        _logoVideoElement = mediaElement;

        if (_logoVideoElement == null)
        {
            if (_logoVideos.Count > 0)
                LogHelper.Warning($"Could not find [{LOGO_VIDEO_ELEMENT_NAME}] in the main-menu logo.");
            return;
        }

        RefreshLogoVideoSource();
        ApplyLogoVideoSource();
    }

    private void DetachLogoVideoElement()
    {
        if (_logoVideoElement == null)
            return;

        _logoVideoElement = null;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DetachLogoVideoElement();
    }

    // -------------------------------------------------------------------------
    // Mod icons
    // -------------------------------------------------------------------------

    public ObservableCollection<MainMenuModLogoEntry> LoadedMods { get; } = new ObservableCollection<MainMenuModLogoEntry>();

    private double _modIconSize = 64;
    public double ModIconSize
    {
        get => _modIconSize;
        set
        {
            if (_modIconSize == value)
                return;

            _modIconSize = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ModIconSize)));
        }
    }

    // -------------------------------------------------------------------------
    // Update check
    // -------------------------------------------------------------------------


    /// <summary>
    /// GitLab releases API endpoint
    /// </summary>
    private const string GITLAB_RELEASES_URL = "https://gitlab.com/api/v4/projects/rawra-stronghold-crusader%2Fshcde-script-extender/releases?per_page=1";

    private UpdateCheckState _updateCheckState = UpdateCheckState.Idle;
    /// <summary>Current state of the background update check.</summary>
    public UpdateCheckState UpdateCheckState
    {
        get => _updateCheckState;
        private set
        {
            if (_updateCheckState == value)
                return;

            _updateCheckState = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateCheckState)));

            // Derived convenience properties
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsUpdateAvailable)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateBadgeVisibility)));
        }
    }

    private string _latestVersion = string.Empty;
    /// <summary>The latest version tag fetched from GitLab (e.g. "1.2.3").</summary>
    public string LatestVersion
    {
        get => _latestVersion;
        private set
        {
            if (_latestVersion == value)
                return;

            _latestVersion = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LatestVersion)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(UpdateBadgeText)));
        }
    }

    /// <summary>True when a newer version has been confirmed on GitLab.</summary>
    public bool IsUpdateAvailable => UpdateCheckState == UpdateCheckState.UpdateAvailable;

    /// <summary>
    /// Noesis Visibility for the update badge — visible only when an update is available.
    /// </summary>
    public Visibility UpdateBadgeVisibility => IsUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

    /// <summary>Label text shown on the update badge.</summary>
    public string UpdateBadgeText =>
        string.IsNullOrEmpty(LatestVersion)
            ? "Update available!"
            : $"v{LatestVersion} available!";

    // -------------------------------------------------------------------------
    // Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fires an async update check against GitLab and updates ViewModel properties
    /// when complete. Uses <see cref="ReleaseUpdateChecker"/> internally.
    /// </summary>
    /// <param name="currentVersion">
    /// The running version string from <c>Plugin.PLUGIN_VERSION</c> (e.g. "1.0.0").
    /// </param>
    public async Task CheckForUpdateAsync(string currentVersion)
    {
        UpdateCheckState = UpdateCheckState.Checking;

        try
        {
            string json = await ReleaseUpdateChecker.SendGetAsync(GITLAB_RELEASES_URL).ConfigureAwait(false);

            // The API returns a JSON array; grab the first (newest) releases tag_name.
            using JsonDocument doc = JsonDocument.Parse(json);
            JsonElement root = doc.RootElement;

            if (root.ValueKind != JsonValueKind.Array || root.GetArrayLength() == 0)
            {
                UpdateCheckState = UpdateCheckState.Failed;
                return;
            }

            // tag_name may be "v1.2.3" or "1.2.3", strip any leading 'v'
            string rawTag = root[0].GetProperty("tag_name").GetString() ?? string.Empty;
            string latestTag = rawTag.TrimStart('v', 'V');

            if (!Version.TryParse(latestTag, out Version latestVer))
            {
                UpdateCheckState = UpdateCheckState.Failed;
                return;
            }

            string currentClean = currentVersion.TrimStart('v', 'V');
            if (!Version.TryParse(currentClean, out Version currentVer))
            {
                UpdateCheckState = UpdateCheckState.Failed;
                return;
            }

            if (latestVer > currentVer)
            {
                LatestVersion = latestTag;
                UpdateCheckState = UpdateCheckState.UpdateAvailable;
            }
            else
            {
                UpdateCheckState = UpdateCheckState.UpToDate;
            }
        }
        catch
        {
            // Network failure, timeout, or malformed JSON: fail silently in-game
            UpdateCheckState = UpdateCheckState.Failed;
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Adds a mod icon to the main menu with full mod information
    /// </summary>
    /// <param name="iconPath">Path to the mod's icon</param>
    /// <param name="modInfo">Mod metadata containing name, version, website, etc.</param>
    /// <param name="avoidCenter">Whether to avoid placing the icon in the center safe zone</param>
    public void AddMod(string iconPath, ModInfo modInfo, bool avoidCenter = false)
    {
        double containerSize = 400;
        double iconSize = ModIconSize; // 64
        double spacing = 2;

        int cols = (int)((containerSize - spacing) / (iconSize + spacing));
        int currentIndex = LoadedMods.Count;

        if (avoidCenter)
        {
            // Center safe zone
            double centerSafeZone = 75;
            double centerX = containerSize / 2;
            double centerY = containerSize / 2;

            // Keep incrementing until we find a valid spot
            while (true)
            {
                int row = currentIndex / cols;
                int col = currentIndex % cols;

                double x = spacing + col * (iconSize + spacing);
                double y = spacing + row * (iconSize + spacing);

                bool inCenter = (x + iconSize > centerX - centerSafeZone) && (x < centerX + centerSafeZone) &&
                                (y + iconSize > centerY - centerSafeZone) && (y < centerY + centerSafeZone);

                if (!inCenter)
                {
                    AddModLogoEntry(iconPath, modInfo, x, y);
                    break;
                }

                currentIndex++;
            }
        }
        else
        {
            // Simple grid placement
            int row = currentIndex / cols;
            int col = currentIndex % cols;

            double x = spacing + col * (iconSize + spacing);
            double y = spacing + row * (iconSize + spacing);

            AddModLogoEntry(iconPath, modInfo, x, y);
        }
    }

    private void AddModLogoEntry(string iconPath, ModInfo? modInfo, double x, double y)
    {
        string author = modInfo?.Author ?? "Unknown";
        if (string.IsNullOrWhiteSpace(author))
            author = "Unknown";

        MainMenuModLogoEntry entry = new()
        {
            IconPath = iconPath,
            X = x,
            Y = y,
            ModName = modInfo?.Name ?? "Unknown Mod",
            AuthorLabel = $"By: {author}",
            Version = modInfo?.Version ?? "Unknown Version",
            Description = modInfo?.Description ?? string.Empty,
            Website = modInfo?.Website ?? string.Empty
        };

        LoadedMods.Add(entry);

        if (modInfo != null && !string.IsNullOrWhiteSpace(modInfo.VersionCheckUrl))
            _ = CheckModForUpdateAsync(entry, modInfo);
    }

    private async Task CheckModForUpdateAsync(MainMenuModLogoEntry entry, ModInfo modInfo)
    {
        try
        {
            string? latestVersion = await ReleaseUpdateChecker.GetNewerReleaseAsync(modInfo.Version, modInfo.VersionCheckUrl).ConfigureAwait(false);
            if (latestVersion == null)
                return;

            if (!_disposed)
                entry.UpdateAvailableText = $"New version available: v{latestVersion}";
        }
        catch (Exception ex)
        {
            LogHelper.Warning($"Could not check releases for mod [{modInfo.Name ?? modInfo.GUID ?? "Unknown"}]: {ex}");
        }
    }

    /// <summary>
    /// Legacy overload for backward compatibility
    /// </summary>
    public void AddMod(string iconPath, bool avoidCenter = false)
    {
        AddMod(iconPath, null, avoidCenter);
    }
}
