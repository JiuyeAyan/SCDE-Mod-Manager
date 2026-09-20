using R3;
using SHCDESE.API.Components.Archive;
using SHCDESE.Detours;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Extensions;
using SHCDESE.GameGlobals;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using UnityEngine;

namespace SHCDESE.API;

/// <summary>
/// Thread-safe singleton API for managing all game audio operations.
/// Handles the boundary between the simulation thread and Unity's main thread.
/// </summary>
/// <remarks>
/// <para><b>Thread Safety:</b> All public methods are thread-safe and can be called from any thread.</para>
/// <para><b>Architecture:</b> This API acts as a facade over Unity's audio system and the legacy engine,
/// automatically dispatching operations to the Unity main thread when necessary.</para>
/// </remarks>
[LuaApiNamespace("Sound")]
public sealed class GameSoundManagerAPI
{
    private static readonly Lazy<GameSoundManagerAPI> _lazy = new(() => new GameSoundManagerAPI());
    public static GameSoundManagerAPI Instance => _lazy.Value;

    public const string CUSTOM_SOUND_ID_PREFIX = "se://";
    private const int INVALID_SOUND_ID = -1;

    // Legacy engine pointer
    private readonly IntPtr _soundManager;

    // Thread-safe collections for cleanup tracking
    private readonly ConcurrentBag<int> _temporarySoundIds = new();
    private readonly ConcurrentBag<int> _temporaryAmbientIds = new();

    // Thread-safe cache: relative path -> engine sound ID
    private readonly ConcurrentDictionary<string, int> _assetPathToSoundId = new(StringComparer.OrdinalIgnoreCase);

    // Initialization flag
    private int _initialized = 0;

    // Lock for SFXManager list operations
    private readonly object _sfxManagerLock = new();

    /// <summary>
    /// Suppresses played/queued speech sounds.
    /// </summary>
    internal static bool _suppressSpeech = false;

    /// <summary>
    /// Suppresses played/queued messages.
    /// </summary>
    internal static bool _suppressMessages = false;

    private GameSoundManagerAPI()
    {
        _soundManager = (IntPtr)GameGlobalsManager.Instance.GameSoundManagerVA;

        LogHelper.Information($"_soundManager: {_soundManager.ToString("X16")}");
    }

    /// <summary>
    /// Initializes event subscribers. Should be called once during plugin startup.
    /// Thread-safe: Can be called multiple times, only initializes once.
    /// </summary>
    public static void InitializeSubscribers()
    {
        // Thread-safe single initialization
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information("Setting up GameSoundManagerAPI event subscribers");
        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    /// <summary>
    /// Cleans up all temporary sounds and ambients when a map is unloaded.
    /// Executes on Unity main thread.
    /// </summary>
    private static void OnUnloadMap(MapUnloadEventArgs args)
    {
        Instance.Unload(clearCache: false);
    }

    internal void Unload(bool clearCache = false)
    {
        LogHelper.Information("Unloading sounds");

        while (_temporarySoundIds.TryTake(out int soundId))
        {
            TryRemoveSoundInternal(soundId);
        }

        while (_temporaryAmbientIds.TryTake(out int ambientId))
        {
            TryRemoveAmbientInternal(ambientId);
        }
        _assetPathToSoundId.Clear();

        LogHelper.Information("Unloading sounds complete");
    }

    /// <summary>
    /// Suppresses any queued speech sounds.
    /// </summary>
    [LuaApiExport("SetSuppressSpeech")]
    public void SetSuppressSpeech(bool suppress = true)
    {
        _suppressSpeech = suppress;
    }

    /// <summary>
    /// Checks if suppressing any queued speech sounds.
    /// </summary>
    /// <returns>Suppressed</returns>
    [LuaApiExport("GetSuppressSpeech")]
    public bool GetSuppressSpeech() => _suppressSpeech;

    /// <summary>
    /// Suppresses any queued messages.
    /// </summary>
    [LuaApiExport("SetSuppressMessages")]
    public void SetSuppressMessages(bool suppress = true)
    {
        _suppressMessages = suppress;
    }

    /// <summary>
    /// Checks if suppressing any queued messages.
    /// </summary>
    /// <returns>Suppressed</returns>
    [LuaApiExport("GetSuppressMessages")]
    public bool GetSuppressMessages() => _suppressMessages;

    /// <summary>
    /// Plays a mono WAV sound using the legacy Crusader audio engine.
    /// Thread-safe: Can be called from any thread.
    /// </summary>
    /// <param name="filePath">Absolute path to a mono, 44.1kHz, 16-bit PCM WAV file.</param>
    /// <returns>True if the file exists and playback was initiated.</returns>
    [LuaApiExport("PlayLegacy")]
    public bool TryPlayEngineMonoSound(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            LogHelper.Warning("TryPlayEngineMonoSound: filePath is null or empty");
            return false;
        }

        if (!File.Exists(filePath))
        {
            LogHelper.Error($"TryPlayEngineMonoSound: File not found: [{filePath}]");
            return false;
        }

        try
        {
            BulkSoundDetours.c_game_soundmanager_play_mono_sound!(_soundManager, filePath);
            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error playing legacy sound: [{filePath}]");
            return false;
        }
    }

    /// <summary>
    /// Plays a sound by ID from the game's sound list.
    /// Thread-safe: Automatically dispatches to Unity main thread.
    /// </summary>
    [LuaApiExport("PlayFromId")]
    public void PlayUnitySoundEx(int soundId, float volumeOffset = 1f, float pan = 0f, bool unstoppable = false)
    {
        if (soundId < 0)
        {
            LogHelper.Warning($"Invalid sound ID: {soundId}");
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                SFXManager.instance.playSound(soundId, volumeOffset, pan, unstoppable);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error playing sound ID {soundId}");
            }
        });
    }

    /// <summary>
    /// Plays a sound using the eSFX enum.
    /// Thread-safe: Automatically dispatches to Unity main thread.
    /// </summary>
    [LuaApiExport("PlayFromEnum")]
    public void PlayUnitySound(Enums.eSFX soundId, float volumeOffset = 1f, float pan = 0f, bool unstoppable = false)
    {
        PlayUnitySoundEx((int)soundId, volumeOffset, pan, unstoppable);
    }

    /// <summary>
    /// Plays a registered sound (built-in or custom) by its ID.
    /// Thread-safe: Automatically dispatches to Unity main thread.
    /// </summary>
    [LuaApiExport("Play")]
    public void PlayUnitySFX(int soundId, float volumeOffset = 1f, float pan = 0f, bool unstoppable = false)
    {
        if (soundId < 0)
        {
            LogHelper.Warning($"Invalid sound ID: {soundId}");
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                List<SFXManager.sh1_sound> playList = SFXManager.instance.play_list;

                // Bounds check
                if (soundId >= playList.Count)
                {
                    LogHelper.Error($"Sound ID {soundId} out of range (max: {playList.Count - 1})");
                    return;
                }

                AudioClip? clip = playList[soundId].clip;
                if (clip == null)
                {
                    LogHelper.Error($"AudioClip is null for sound ID {soundId}");
                    return;
                }

                MyAudioManager.Instance.playSFX(clip, volumeOffset, pan, unstoppable);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error playing sound ID {soundId}");
            }
        });
    }

    /// <summary>
    /// Plays a sound at a unit's screen position with optional distance-based volume modulation.
    /// Thread-safe: Automatically dispatches to Unity main thread.
    /// </summary>
    [LuaApiExport("PlayAtUnit")]
    public void PlayUnitySFXAtUnit(int unitId, int soundId, float volume = 1f, bool unstoppable = false, bool distMod = false)
    {
        if (soundId < 0)
        {
            LogHelper.Warning($"Invalid sound ID: {soundId}");
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            try
            {
                // Get unit position
                Vector3? pos = GameUnitManagerAPI.Instance.GetCurrentUnityPosition(unitId).ToUnityVector3Safe();

                float pan = 0f;
                float distVolumeMod = 1f;

                if (pos.HasValue && Camera.main != null)
                {
                    // Calculate stereo pan from viewport position
                    Vector3 viewportPos = Camera.main.WorldToViewportPoint(pos.Value);

                    // Map viewport X (0-1) to pan (-1 to 1)
                    if (viewportPos.z > 0) // Only if in front of camera
                    {
                        pan = Mathf.Clamp((viewportPos.x - 0.5f) * 2f, -1f, 1f);
                    }

                    // Optional distance-based volume
                    if (distMod && Camera.main != null)
                    {
                        float distance = Vector3.Distance(Camera.main.transform.position, pos.Value);
                        const float maxDistance = 50f;
                        distVolumeMod = Mathf.Clamp01(1f - (distance / maxDistance));
                    }
                }

                float finalVolume = volume * distVolumeMod;
                PlayUnitySFX(soundId, finalVolume, pan, unstoppable);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Error playing sound at unit {unitId}");
            }
        });
    }

    /// <summary>
    /// Registers a sound from the map archive with the SFX system.
    /// Thread-safe: Result delivered via callback on Unity main thread.
    /// </summary>
    /// <param name="archivePath">Relative path within the archive (e.g., "sounds/mysound.wav").</param>
    /// <param name="onCompleteCallback">Invoked with the resulting sound ID (or -1 on failure) on the Unity main thread.</param>
    /// <param name="initialVolume">Default volume for this sound.</param>
    /// <param name="initialPosition">Audio position/priority value.</param>
    /// <param name="temporary">If true, will be auto-removed on map unload.</param>
    [LuaApiExport("RegisterFromArchiveAsync")]
    public void RegisterSoundFromArchive(string archivePath, Action<int> onCompleteCallback, float initialVolume = 1f, int initialPosition = 64, bool temporary = true)
    {
        if (string.IsNullOrWhiteSpace(archivePath))
        {
            LogHelper.Warning("archivePath is null or empty");
            onCompleteCallback?.Invoke(INVALID_SOUND_ID);
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            int result = RegisterSoundFromArchiveInternal(archivePath, initialVolume, initialPosition, temporary);
            onCompleteCallback?.Invoke(result);
        });
    }

    /// <summary>
    /// Internal helper for sound registration. MUST be called on Unity main thread.
    /// </summary>
    private int RegisterSoundFromArchiveInternal(string archivePath, float initialVolume, int initialPosition, bool temporary)
    {
        try
        {
            string normalizedPath = archivePath.Replace('\\', '/');
            AudioClip? clip = null;

            // Primary: try the embedded map archive (archive-mod path)
            MapArchive? archive = GameMapArchiveManagerAPI.Instance.GetMapArchive();
            if (archive != null)
            {
                GameAssetManagerAPI.Instance.TryLoadAudioClipFromArchive(archive, normalizedPath, out clip, useCache: true);
            }

            // Fallback: try the file index (asset-mod / Override folder path)
            if (clip == null)
            {
                GameAssetManagerAPI.Instance.TryLoadAudioClip(normalizedPath, out clip, useCache: true);
            }

            if (clip == null)
            {
                LogHelper.Warning($"Failed to load audio clip: [{normalizedPath}]");
                return INVALID_SOUND_ID;
            }

            int soundId = RegisterAudioClipInternal(clip, initialVolume, initialPosition);

            if (soundId != INVALID_SOUND_ID)
            {
                if (temporary)
                    _temporarySoundIds.Add(soundId);

                LogHelper.Information($"Registered sound from archive: [{normalizedPath}] -> ID {soundId}");
            }

            return soundId;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error registering sound from archive: [{archivePath}]");
            return INVALID_SOUND_ID;
        }
    }


    /// <summary>
    /// Removes a registered sound from the system.
    /// Thread-safe: Result delivered via callback on Unity main thread.
    /// </summary>
    /// <param name="soundId">The ID of the sound to remove.</param>
    /// <param name="onCompleteCallback">Invoked with true if successfully removed, false otherwise.</param>
    public void TryRemoveSound(int soundId, Action<bool> onCompleteCallback)
    {
        if (soundId < 0)
        {
            onCompleteCallback?.Invoke(false);
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            bool result = TryRemoveSoundInternal(soundId);
            onCompleteCallback?.Invoke(result);
        });
    }

    /// <summary>
    /// Internal sound removal. MUST be called on Unity main thread.
    /// </summary>
    private bool TryRemoveSoundInternal(int soundId)
    {
        try
        {
            lock (_sfxManagerLock)
            {
                List<SFXManager.sh1_sound> playList = SFXManager.instance.play_list;

                if (soundId < playList.Count)
                {
                    // Cleanup the AudioClip if needed
                    SFXManager.sh1_sound sound = playList[soundId];
                    if (sound.clip != null)
                    {
                        // Don't destroy built-in clips, only custom ones
                        if (sound.clip.name.StartsWith(CUSTOM_SOUND_ID_PREFIX, StringComparison.OrdinalIgnoreCase))
                        {
                            UnityEngine.Object.Destroy(sound.clip);
                        }
                    }

                    playList.RemoveAt(soundId);
                    LogHelper.Debug($"Removed sound ID {soundId}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error removing sound {soundId}");
        }

        return false;
    }

    /// <summary>
    /// Gets or registers a sound from the asset override system.
    /// MUST be called on the Unity main thread. Use <see cref="GetSoundIdFromAssetAsync"/> from background threads.
    /// </summary>
    /// <param name="relativePath">Path relative to Override folder (e.g., "Sounds/hitmarker.ogg").</param>
    /// <param name="initialVolume">Default volume if registering for the first time.</param>
    /// <returns>Sound ID, or -1 if the file doesn't exist.</returns>
    public int GetSoundIdFromAsset(string relativePath, float initialVolume = 1f)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
            return INVALID_SOUND_ID;

        string normalizedPath = relativePath.Replace('\\', '/');

        // Fast path: Check cache
        if (_assetPathToSoundId.TryGetValue(normalizedPath, out int cachedId))
            return cachedId;

        // Already on Unity main thread — load and register directly
        try
        {
            if (GameAssetManagerAPI.Instance.TryLoadAudioClip(normalizedPath, out AudioClip? clip, useCache: true) && clip != null)
            {
                int newId = RegisterAudioClipInternal(clip, initialVolume, 64);

                if (newId != INVALID_SOUND_ID)
                {
                    _assetPathToSoundId[normalizedPath] = newId;
                    LogHelper.Information($"Auto-registered asset: [{normalizedPath}] -> ID {newId}");
                    return newId;
                }
            }
            else
            {
                LogHelper.Warning($"Asset not found: [{relativePath}]");
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error loading asset: [{relativePath}]");
        }

        return INVALID_SOUND_ID;
    }

    /// <summary>
    /// Async version for Lua. Gets or registers a sound from assets.
    /// Thread-safe: Result delivered via callback on Unity main thread.
    /// </summary>
    [LuaApiExport("GetIdFromAssetAsync")]
    public void GetSoundIdFromAssetAsync(string relativePath, NLua.LuaFunction onCompleteCallback, float initialVolume = 1f)
    {
        if (onCompleteCallback == null)
        {
            LogHelper.Warning("callback is null");
            return;
        }

        if (string.IsNullOrWhiteSpace(relativePath))
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() => onCompleteCallback.Call(INVALID_SOUND_ID));
            return;
        }

        string normalizedPath = relativePath.Replace('\\', '/');

        // Fast path: Return cached immediately
        if (_assetPathToSoundId.TryGetValue(normalizedPath, out int cachedId))
        {
            UnityMainThreadDispatcher.Instance.Enqueue(() => onCompleteCallback.Call(cachedId));
            return;
        }

        // Slow path: Load on Unity thread
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            int resultId = GetSoundIdFromAsset(normalizedPath, initialVolume);
            onCompleteCallback.Call(resultId);
        });
    }

    /// <summary>
    /// Plays a sound directly from a file path in the Override directory.
    /// Thread-safe: Automatically handles registration and playback.
    /// </summary>
    [LuaApiExport("PlayFile")]
    public void PlaySoundFromFile(string relativePath, float volumeOffset = 1f, float pan = 0f, bool unstoppable = false)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            int soundId = GetSoundIdFromAsset(relativePath);
            if (soundId != INVALID_SOUND_ID)
            {
                PlayUnitySFX(soundId, volumeOffset, pan, unstoppable);
            }
        });
    }

    /// <summary>
    /// Core registration method. MUST be called on Unity main thread.
    /// Registers an AudioClip with the SFXManager.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RegisterAudioClipInternal(AudioClip clip, float initialVolume, int initialPosition)
    {
        try
        {
            lock (_sfxManagerLock)
            {
                SFXManager.sh1_sound newSound = new SFXManager.sh1_sound
                {
                    volume = initialVolume,
                    position = initialPosition,
                    clip = clip
                };

                SFXManager.instance.play_list.Add(newSound);
                int newId = SFXManager.instance.play_list.Count - 1;

                LogHelper.Debug($"Registered AudioClip '{clip.name}' as ID {newId}");
                return newId;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Failed to register AudioClip internally");
            return INVALID_SOUND_ID;
        }
    }

    /// <summary>
    /// Registers an ambient sound group (multiple sound variants).
    /// Thread-safe: Result delivered via callback on Unity main thread.
    /// </summary>
    /// <param name="firstBufferNo">Sound ID of the first sound in the group.</param>
    /// <param name="maxVariants">Number of variant sounds in this group.</param>
    /// <param name="temporary">If true, auto-removed on map unload.</param>
    /// <param name="onCompleteCallback">Invoked with the ambient group ID (or -1 on failure) on the Unity main thread.</param>
    [LuaApiExport("RegisterAmbientGroup")]
    public void RegisterAmbient(int firstBufferNo, int maxVariants, bool temporary, Action<int> onCompleteCallback)
    {
        if (firstBufferNo < 0 || maxVariants <= 0)
        {
            LogHelper.Warning($"Invalid ambient parameters: firstBufferNo={firstBufferNo}, maxVariants={maxVariants}");
            onCompleteCallback?.Invoke(INVALID_SOUND_ID);
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            int result = RegisterAmbientInternal(firstBufferNo, maxVariants, temporary);
            onCompleteCallback?.Invoke(result);
        });
    }

    /// <summary>
    /// Internal ambient registration. MUST be called on Unity main thread.
    /// </summary>
    private int RegisterAmbientInternal(int firstBufferNo, int maxVariants, bool temporary)
    {
        try
        {
            lock (_sfxManagerLock)
            {
                // Validate that the first buffer exists
                if (firstBufferNo >= SFXManager.instance.play_list.Count)
                {
                    LogHelper.Error($"firstBufferNo {firstBufferNo} exceeds play_list size {SFXManager.instance.play_list.Count}");
                    return INVALID_SOUND_ID;
                }

                SFXManager.sh1_sound_effect newAmbient = new SFXManager.sh1_sound_effect
                {
                    first_buffer_no = firstBufferNo,
                    max_variants = maxVariants,
                    variants_loaded = 0,
                    last_variant_played = 0
                };

                SFXManager.instance.ambient_list.Add(newAmbient);
                int newId = SFXManager.instance.ambient_list.Count - 1;

                if (temporary)
                {
                    _temporaryAmbientIds.Add(newId);
                }

                LogHelper.Information($"Registered ambient group: ID {newId}, firstBuffer={firstBufferNo}, variants={maxVariants}");
                return newId;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, "Error registering ambient group");
            return INVALID_SOUND_ID;
        }
    }

    /// <summary>
    /// Removes an ambient sound group.
    /// Thread-safe: Result delivered via callback on Unity main thread.
    /// </summary>
    /// <param name="ambientId">The ID of the ambient group to remove.</param>
    /// <param name="onCompleteCallback">Invoked with true if successfully removed, false otherwise.</param>
    public void TryRemoveAmbient(int ambientId, Action<bool> onCompleteCallback)
    {
        if (ambientId < 0)
        {
            onCompleteCallback?.Invoke(false);
            return;
        }

        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            bool result = TryRemoveAmbientInternal(ambientId);
            onCompleteCallback?.Invoke(result);
        });
    }

    /// <summary>
    /// Internal ambient removal. MUST be called on Unity main thread.
    /// </summary>
    private bool TryRemoveAmbientInternal(int ambientId)
    {
        try
        {
            lock (_sfxManagerLock)
            {
                List<SFXManager.sh1_sound_effect> ambientList = SFXManager.instance.ambient_list;

                if (ambientId < ambientList.Count)
                {
                    ambientList.RemoveAt(ambientId);
                    LogHelper.Debug($"Removed ambient group {ambientId}");
                    return true;
                }
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Error removing ambient {ambientId}");
        }

        return false;
    }

    /// <summary>
    /// Gets current statistics about registered sounds.
    /// Thread-safe: Result delivered via callback on Unity main thread.
    /// </summary>
    public void GetStatistics(Action<(int totalSounds, int temporarySounds, int ambientGroups, int temporaryAmbients, int cachedAssets)> onCompleteCallback)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            lock (_sfxManagerLock)
            {
                onCompleteCallback?.Invoke((
                    SFXManager.instance.play_list.Count,
                    _temporarySoundIds.Count,
                    SFXManager.instance.ambient_list.Count,
                    _temporaryAmbientIds.Count,
                    _assetPathToSoundId.Count
                ));
            }
        });
    }

    /// <summary>
    /// Clears the asset path cache. Useful for debugging or reloading assets.
    /// Thread-safe.
    /// </summary>
    public void ClearAssetCache()
    {
        _assetPathToSoundId.Clear();
        LogHelper.Information("Asset path cache cleared");
    }
}