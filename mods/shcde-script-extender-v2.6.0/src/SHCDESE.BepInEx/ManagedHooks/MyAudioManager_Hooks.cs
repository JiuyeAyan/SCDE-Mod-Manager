using SHCDESE.API;
using SHCDESE.Logging;
using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using UnityEngine;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{
    //
    // MyAudioManager
    //
    internal ManagedDetour<myAudioManager_playSFXDelegate> myAudioManager_playSFX_hook;
    internal delegate void myAudioManager_playSFXDelegate(MyAudioManager instance, AudioClip sound, float volume, float pan = 0f, bool unstoppable = false, bool force = false);

    /// <summary>
    /// Hook for MyAudioManager.playSFX - intercepts SFX playback to apply custom audio clips.
    /// This hook runs on Unity's main thread.
    /// </summary>
    internal void MyAudioManager_PlaySFX_Hook(MyAudioManager instance, AudioClip sound, float volume, float pan = 0f, bool unstoppable = false, bool force = false)
    {
        try
        {
            if (sound == null)
            {
                LogHelper.Warning("PlaySFX called with null AudioClip");
                return;
            }

            // Try to load custom replacement based on clip name
            // useCache: true because SFX are typically played repeatedly
            if (GameAssetManagerAPI.Instance.TryLoadAudioClip(sound.name, out AudioClip? overrideClip, useCache: true))
            {
                LogHelper.Debug($"PlaySFX using custom audio: [{sound.name}]");
                myAudioManager_playSFX_hook.Trampoline(instance, overrideClip!, volume, pan, unstoppable, force);
                return;
            }

            LogHelper.Debug($"PlaySFX using original audio: [{sound.name}]");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error in PlaySFX hook for [{sound.name}], falling back to original");
        }
        myAudioManager_playSFX_hook.Trampoline(instance, sound, volume, pan, unstoppable, force);
    }

    internal ManagedDetour<myAudioManager_loadClipDelegate> myAudioManager_loadClip_hook;
    internal delegate Task<AudioClip> myAudioManager_loadClipDelegate(MyAudioManager instance, string path);

    /// <summary>
    /// Hook for MyAudioManager.LoadClip - intercepts audio clip loading for speech and music.
    /// This hook runs on Unity's main thread but is async.
    /// </summary>
    /// <remarks>
    /// IMPORTANT: MyAudioManager unloads speech/music clips immediately after use,
    /// so we CANNOT cache these clips (useCache: false).
    /// </remarks>
    internal async Task<AudioClip> MyAudioManager_LoadClip_Hook(MyAudioManager instance, string path)
    {
        try
        {
            string relativePath = GameAssetManagerAPI.Instance.ExtractAudioPath(path);
            LogHelper.Debug($"LoadClip attempting to load: [{relativePath}] (original: [{path}])");

            // Try to load custom audio
            // useCache: FALSE - Speech/Music are unloaded after playing.
            if (GameAssetManagerAPI.Instance.TryLoadAudioClip(relativePath, out AudioClip? overrideClip, useCache: false))
            {
                LogHelper.Information($"LoadClip using custom audio: [{relativePath}]");
                return overrideClip!;
            }
            LogHelper.Debug($"LoadClip using original audio: [{path}]");
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error in LoadClip hook for [{path}], falling back to original");
        }

        LogHelper.Debug($"LoadClip attempting to load original: [{path}]");
        return await myAudioManager_loadClip_hook.Trampoline(instance, path);
    }
}
