using Noesis;
using NoesisApp;
using SHCDESE.API;
using SHCDESE.Logging;
using System;
using UnityEngine;
using UnityEngine.Video;

namespace SHCDESE.UI;

/// <summary>
/// Video player used only by the script-extender logo MediaElement.
/// It renders the decoded Unity video through the attributed analog-video shader into a RenderTexture before exposing that texture to Noesis.
/// </summary>
internal sealed class AnalogLogoMediaPlayer : MediaPlayer
{
    internal const string ShaderName = "SHCDESE/LogoAnalogVideo";
    internal const string ShaderBundlePath = "AssetBundles/shcdese_logoanalogvideo_bundle";

    private static UnityEngine.Shader? _resolvedShader;
    private static bool _bundleLoadAttempted;

    private readonly GameObject _gameObject;
    private readonly VideoPlayer _videoPlayer;
    private readonly AnalogLogoRenderDriver _renderDriver;
    private readonly bool _staticEffectEnabled;
    private readonly bool _scanlineEffectEnabled;
    private readonly float _opacity;
    private readonly bool _loopVideo;
    private readonly Action? _onOpened;
    private readonly Action? _onEnded;
    private readonly Action<Exception>? _onFailed;
    private TextureSource? _textureSource;
    private RenderTexture? _effectTexture;
    private Material? _effectMaterial;
    private bool _keepPlaying;
    private bool _playbackRequested = true;
    private bool _waitingForControlFrame;
    private bool _firstFrameReceived;
    private bool _endNotificationSent;
    private long _lastObservedFrame = -1;
    private double _lastObservedTime = -1.0;
    private float _lastProgressRealtime;
    private bool _effectFrameLogged;
    private bool _closed;

    public AnalogLogoMediaPlayer(string uri, bool staticEffectEnabled, bool scanlineEffectEnabled, float opacity, bool loopVideo, Action? onOpened, Action? onEnded, Action<Exception>? onFailed)
    {
        _staticEffectEnabled = staticEffectEnabled;
        _scanlineEffectEnabled = scanlineEffectEnabled;
        _opacity = Mathf.Clamp01(opacity);
        _loopVideo = loopVideo;
        _onOpened = onOpened;
        _onEnded = onEnded;
        _onFailed = onFailed;

        _gameObject = new GameObject("SHCDESE Logo MediaPlayer");
        _videoPlayer = _gameObject.AddComponent<VideoPlayer>();
        _renderDriver = _gameObject.AddComponent<AnalogLogoRenderDriver>();
        _renderDriver.RenderFrame = UpdatePlaybackFrame;

        _renderDriver.enabled = true;
        _videoPlayer.renderMode = VideoRenderMode.APIOnly;

        if (uri.StartsWith("/", StringComparison.Ordinal) && uri.Contains(":"))
            uri = uri.Substring(1);

        if (uri.EndsWith("*", StringComparison.Ordinal))
            uri = uri.TrimEnd('*');

        VideoClip clip = VideoProvider.instance.GetVideoClip(uri);
        if (clip != null)
        {
            _videoPlayer.clip = clip;
            _videoPlayer.source = VideoSource.VideoClip;
        }
        else
        {
            _videoPlayer.url = uri;
            _videoPlayer.source = VideoSource.Url;
        }

        _videoPlayer.prepareCompleted += OnMediaOpened;
        _videoPlayer.errorReceived += OnMediaFailed;
        _videoPlayer.loopPointReached += OnMediaEnded;
        _videoPlayer.isLooping = _loopVideo;

        _keepPlaying = true;
        _videoPlayer.Prepare();
    }

    public override uint Width => _effectTexture != null ? (uint)_effectTexture.width : _videoPlayer.width;
    public override uint Height => _effectTexture != null ? (uint)_effectTexture.height : _videoPlayer.height;
    public override bool CanPause => true;
    public override bool HasAudio => _videoPlayer.audioTrackCount > 0;
    public override bool HasVideo => true;
    public override double Duration => _videoPlayer.length;

    public override double Position
    {
        get => _videoPlayer.time;
        set => _videoPlayer.time = value;
    }

    public override float SpeedRatio
    {
        get => _videoPlayer.playbackSpeed;
        set => _videoPlayer.playbackSpeed = value;
    }

    public override float Volume
    {
        get => HasAudio ? _videoPlayer.GetDirectAudioVolume(0) : 0.5f;
        set
        {
            if (HasAudio)
                _videoPlayer.SetDirectAudioVolume(0, value);
        }
    }

    public override bool IsMuted
    {
        get => HasAudio && _videoPlayer.GetDirectAudioMute(0);
        set
        {
            if (HasAudio)
                _videoPlayer.SetDirectAudioMute(0, value);
        }
    }

    public override ImageSource TextureSource => _textureSource!;

    public override void Play()
    {
        _playbackRequested = true;
        _keepPlaying = true;
        _videoPlayer.Play();
    }

    public override void Pause()
    {
        _playbackRequested = false;
        _keepPlaying = false;
        _videoPlayer.Pause();
    }

    public override void Stop()
    {
        _playbackRequested = false;
        _keepPlaying = false;
        _waitingForControlFrame = true;
        _videoPlayer.time = 0.0;
        _videoPlayer.Play();
    }

    public override void Close()
    {
        if (_closed)
            return;

        _closed = true;
        _playbackRequested = false;
        _videoPlayer.prepareCompleted -= OnMediaOpened;
        _videoPlayer.errorReceived -= OnMediaFailed;
        _videoPlayer.loopPointReached -= OnMediaEnded;
        _videoPlayer.frameReady -= OnFrameReady;
        _videoPlayer.sendFrameReadyEvents = false;
        _videoPlayer.Stop();
        _renderDriver.enabled = false;
        _renderDriver.RenderFrame = null;

        if (_effectTexture != null)
        {
            RenderTexture.active = null;
            _effectTexture.Release();
            UnityEngine.Object.Destroy(_effectTexture);
            _effectTexture = null;
        }

        if (_effectMaterial != null)
        {
            UnityEngine.Object.Destroy(_effectMaterial);
            _effectMaterial = null;
        }

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(_videoPlayer);
            UnityEngine.Object.Destroy(_gameObject);
        }
    }

    private void OnMediaOpened(VideoPlayer source)
    {
        _firstFrameReceived = false;
        _endNotificationSent = false;
        _lastObservedFrame = -1;
        _lastObservedTime = -1.0;
        _lastProgressRealtime = Time.realtimeSinceStartup;
        LogHelper.Debug($"Prepared logo video ({_videoPlayer.width}x{_videoPlayer.height}, fps={_videoPlayer.frameRate:0.###}, frames={_videoPlayer.frameCount}, duration={_videoPlayer.length:0.###}s).");

        UnityEngine.Shader? shader = ResolveShader();
        if (shader != null && shader.isSupported)
        {
            int width = Math.Max(1, (int)_videoPlayer.width);
            int height = Math.Max(1, (int)_videoPlayer.height);

            _effectMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };

            if (!_effectMaterial.HasProperty("_Opacity") ||
                !_effectMaterial.HasProperty("_UseSourceAlpha") ||
                !_effectMaterial.HasProperty("_SourceAspect") ||
                !_effectMaterial.HasProperty("_SrcBlend") ||
                !_effectMaterial.HasProperty("_DstBlend"))
            {
                LogHelper.Error($"Logo shader [{ShaderName}] is an outdated compiled version; rebuild [{ShaderBundlePath}] from the included shader source. Using clean video.");
                UnityEngine.Object.Destroy(_effectMaterial);
                _effectMaterial = null;
            }
            else
            {
                _effectMaterial.SetFloat("_StaticStrength", _staticEffectEnabled ? 1.0f : 0.0f);
                _effectMaterial.SetFloat("_ScanlineStrength", _scanlineEffectEnabled ? 1.0f : 0.0f);
                _effectMaterial.SetFloat("_Opacity", _opacity);
                _effectMaterial.SetFloat("_UseSourceAlpha", 0.0f);
                _effectMaterial.SetFloat("_SourceAspect", (float)width / height);

                _effectMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.One);
                _effectMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.Zero);

                int effectSize = Math.Min(width, height);
                _effectTexture = new RenderTexture(effectSize, effectSize, 0, RenderTextureFormat.ARGB32)
                {
                    name = "SHCDESE Logo Analog Video",
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp,
                    hideFlags = HideFlags.HideAndDontSave
                };
                _effectTexture.Create();
                RenderEffectFrame();
                _renderDriver.enabled = true;
                _textureSource = new TextureSource(_effectTexture);
            }
        }

        if (_textureSource == null)
        {
            if (shader == null || !shader.isSupported)
            {
                LogHelper.Warning($"Logo shader [{ShaderName}] was not loaded or is unsupported; using clean video.");
            }
            _textureSource = new TextureSource(_videoPlayer.texture);
        }

        if (_videoPlayer.audioTrackCount > 0)
            _videoPlayer.EnableAudioTrack(0, true);

        RaiseMediaOpened();
        _onOpened?.Invoke();

        _waitingForControlFrame = true;
        _videoPlayer.sendFrameReadyEvents = true;
        _videoPlayer.frameReady += OnFrameReady;
        _videoPlayer.Play();
    }

    private static UnityEngine.Shader? ResolveShader()
    {
        if (_resolvedShader != null)
            return _resolvedShader;

        UnityEngine.Shader? shader = UnityEngine.Shader.Find(ShaderName);
        if (shader != null)
        {
            _resolvedShader = shader;
            LogHelper.Information($"Resolved logo shader [{ShaderName}] through Shader.Find.");
            return _resolvedShader;
        }

        foreach (UnityEngine.Shader loadedShader in Resources.FindObjectsOfTypeAll<UnityEngine.Shader>())
        {
            if (loadedShader != null && loadedShader.name == ShaderName)
            {
                _resolvedShader = loadedShader;
                LogHelper.Information($"Resolved already-loaded logo shader [{ShaderName}].");
                return _resolvedShader;
            }
        }

        if (!_bundleLoadAttempted)
        {
            _bundleLoadAttempted = true;

            if (!GameAssetManagerAPI.Instance.GetModifiedFilePath(ShaderBundlePath, out _))
            {
                LogHelper.Warning($"Optional logo shader bundle [{ShaderBundlePath}] is not registered; clean-video fallback will be used.");
                return null;
            }

            AssetBundle? bundle = GameBundleManagerAPI.Instance.LoadBundle(ShaderBundlePath);
            if (bundle != null)
            {
                foreach (UnityEngine.Shader bundledShader in bundle.LoadAllAssets<UnityEngine.Shader>())
                {
                    if (bundledShader != null && bundledShader.name == ShaderName)
                    {
                        _resolvedShader = bundledShader;
                        LogHelper.Information($"Loaded logo shader [{ShaderName}] from [{ShaderBundlePath}] (supported={bundledShader.isSupported}).");
                        return _resolvedShader;
                    }
                }

                LogHelper.Error($"Bundle [{ShaderBundlePath}] loaded, but it does not contain shader [{ShaderName}].");
            }
        }

        return null;
    }

    internal static void PreloadShader()
    {
        _ = ResolveShader();
    }

    private void OnFrameReady(VideoPlayer source, long frameIndex)
    {
        if (!_firstFrameReceived)
        {
            _firstFrameReceived = true;
            LogHelper.Debug($"Decoded first logo-video frame [{frameIndex}] (playing={_videoPlayer.isPlaying}, time={_videoPlayer.time:0.###}s).");
        }

        if (_waitingForControlFrame)
        {
            _waitingForControlFrame = false;
            if (!_keepPlaying)
            {
                LogHelper.Warning("Logo video received a pause request before its first decoded frame.");
                _videoPlayer.Pause();
            }
            _keepPlaying = false;
        }

        CheckForPlaybackEnd(frameIndex);
    }

    private void UpdatePlaybackFrame()
    {
        RenderEffectFrame();
        CheckForPlaybackEnd(_videoPlayer.frame);
    }

    private void CheckForPlaybackEnd(long frameIndex)
    {
        if (_loopVideo || _endNotificationSent || !_firstFrameReceived || !_playbackRequested)
            return;

        double playbackTime = _videoPlayer.time;
        if (frameIndex > _lastObservedFrame || playbackTime > _lastObservedTime + 0.001)
        {
            _lastObservedFrame = frameIndex;
            _lastObservedTime = playbackTime;
            _lastProgressRealtime = Time.realtimeSinceStartup;
        }

        bool finalFrame = _videoPlayer.frameCount > 0 && frameIndex >= (long)_videoPlayer.frameCount - 1;

        double frameDuration = _videoPlayer.frameRate > 0.0 ? 1.0 / _videoPlayer.frameRate : 0.05;
        bool finalTime = _videoPlayer.length > 0.0 && playbackTime >= _videoPlayer.length - Math.Max(0.1, frameDuration * 2.0);
        bool stoppedAfterProgress = !_videoPlayer.isPlaying && Time.realtimeSinceStartup - _lastProgressRealtime >= 0.5f;

        if (finalFrame || finalTime || stoppedAfterProgress)
        {
            NotifyMediaEnded();
        }
    }

    private void RenderEffectFrame()
    {
        if (_effectMaterial == null || _effectTexture == null || _videoPlayer.texture == null)
            return;

        Graphics.Blit(_videoPlayer.texture, _effectTexture, _effectMaterial);

        if (!_effectFrameLogged)
        {
            _effectFrameLogged = true;
            LogHelper.Debug($"Rendering logo video through shader [{ShaderName}].");
        }
    }

    private void OnMediaEnded(VideoPlayer source)
    {
        NotifyMediaEnded();
    }

    private void NotifyMediaEnded()
    {
        if (_endNotificationSent)
            return;

        _endNotificationSent = true;
        _playbackRequested = false;
        LogHelper.Debug($"Logo video ended (frame={_videoPlayer.frame}/{_videoPlayer.frameCount}, time={_videoPlayer.time:0.###}/{_videoPlayer.length:0.###}s).");
        _renderDriver.InvokeNextFrame(() =>
        {
            RaiseMediaEnded();
            _onEnded?.Invoke();
        });
    }

    private void OnMediaFailed(VideoPlayer source, string message)
    {
        Exception error = new Exception(message);
        _renderDriver.InvokeNextFrame(() =>
        {
            RaiseMediaFailed(error);
            _onFailed?.Invoke(error);
        });
    }
}
