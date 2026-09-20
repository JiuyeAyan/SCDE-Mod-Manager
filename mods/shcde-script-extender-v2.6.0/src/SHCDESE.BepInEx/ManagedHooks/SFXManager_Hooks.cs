using SHCDESE.API;
using SHCDESE.Logging;
using System;

namespace SHCDESE.ManagedHooks;

public partial class ManagedHookManager
{

    internal delegate void SFXManager_PlaySpeech_Delegate(SFXManager instance, int channel, string fullpath, float volume, bool ignoreSpeechMuting = false, bool ignorePauseState = false);
    internal static ManagedDetour<SFXManager_PlaySpeech_Delegate> sfxManager_PlaySpeech_hook;

    /// <summary>
    /// Hook for SFXManager.PlaySpeech - intercepts speech playback to apply custom audio.
    /// This hook runs on Unity's main thread.
    /// </summary>
    internal void SFXManager_PlaySpeech_Hook(SFXManager instance, int channel, string fullpath, float volume, bool ignoreSpeechMuting = false, bool ignorePauseState = false)
    {
        LogHelper.Debug($"channel={channel}, fullpath=[{fullpath}], vol={volume}, ignoreSpeechMuting={ignoreSpeechMuting}, ignorePauseState={ignorePauseState}");
        
        try
        {
            if (GameAssetManagerAPI.Instance.TryResolveAudioPath(fullpath, out string diskPath))
            {
                string normalizedPath = fullpath.ToLowerInvariant();
                bool isUnitSpeech = false;

                LogHelper.Information($"PlaySpeech managed to resolve to [{diskPath}]");
                MyAudioManager.Instance.PlaySpeech(channel, "_SE_", normalizedPath, true, isUnitSpeech, ignoreSpeechMuting, ignorePauseState);
                return;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error in PlaySpeech hook, falling back to original");
        }

        LogHelper.Verbose($"channel={channel}, fullpath=[{fullpath}], vol={volume}, ignoreSpeechMuting={ignoreSpeechMuting}, ignorePauseState={ignorePauseState} -- original");
        sfxManager_PlaySpeech_hook.Trampoline(instance, channel, fullpath, volume, ignoreSpeechMuting, ignorePauseState);
    }
}
