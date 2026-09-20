using NoesisApp;
using SHCDESE.BepInEx.Bootstrap;
using SHCDESE.Logging;
using SHCDESE.UI;
using SHCDESE.ViewModels;
using System;
using System.Threading;
using UnityEngine;

namespace SHCDESE.ManagedHooks.Callbacks;

internal static class NoesisCallbacks
{
    private static int _lateInstallLogged;

    internal static void Install(bool afterNoesisInitialization = false)
    {
        MediaElement.SetCreateMediaPlayerCallback(new CreateMediaPlayerCallback(CreateMediaPlayer), null);

        if (afterNoesisInitialization && Interlocked.Exchange(ref _lateInstallLogged, 1) == 0)
        {
            LogHelper.Information("Reinstalled SHCDESE media-player factory after Noesis initialization.");
        }
    }

    public static MediaPlayer CreateMediaPlayer(MediaElement mediaElement, Uri uri, object user)
    {
        try
        {
            if (Application.isPlaying)
            {
                MainMenuViewModel? viewModel = Plugin.ViewModel;

                if (mediaElement?.Name == "SELogoVideo" && viewModel?.LogoVideoEnabled == false)
                {
                    LogHelper.Debug("Ignoring logo media-player creation because LogoVideoEnabled is false.");
                    return null;
                }

                if (mediaElement != null &&
                    viewModel?.LogoVideoEnabled == true &&
                    (mediaElement.Name == "SELogoVideo" ||
                     viewModel?.OwnsLogoVideoElement(mediaElement) == true ||
                     viewModel?.IsConfiguredLogoVideoSource(uri) == true))
                {
                    bool staticEffectEnabled = viewModel?.LogoStaticEffectEnabled ?? true;
                    bool scanlineEffectEnabled = viewModel?.LogoScanlineEffectEnabled ?? true;
                    float opacity = viewModel?.LogoVideoOpacity ?? 0.85f;
                    bool loopVideo = viewModel?.ShouldLoopCurrentLogoVideo ?? true;
                    Action? onOpened = viewModel == null ? null : new Action(viewModel.NotifyLogoVideoOpened);
                    Action? onEnded = viewModel == null ? null : new Action(viewModel.NotifyLogoVideoEnded);
                    Action<Exception>? onFailed = viewModel == null ? null : new Action<Exception>(viewModel.NotifyLogoVideoFailed);

                    LogHelper.Debug($"Creating analog logo media player for [{uri}] (loop={loopVideo}, ownerName=[{mediaElement.Name}])");
                    return new AnalogLogoMediaPlayer(uri.ToString(), staticEffectEnabled, scanlineEffectEnabled, opacity, loopVideo, onOpened, onEnded, onFailed);
                }

                // Do not touch this; uri.GetPath() breaks absolute filepaths.
                return new NoesisMediaPlayer(uri.ToString());
            }
            return null;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Exception in noesis media player callback");
        }
        return null;
    }
}
