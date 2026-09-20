using R3;
using SHCDESE.EventAPI;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SHCDESE.API;

/// <summary>
/// Manages the creation, update, and destruction of "after-image" visual effects for game units.
/// </summary>
[LuaApiNamespace("Unit")]
public class GameAfterImageManager
{
 
    private static readonly Lazy<GameAfterImageManager> lazy = new(() => new GameAfterImageManager());
    public static GameAfterImageManager Instance => lazy.Value;
    private GameAfterImageManager() 
    {

    }

    private int _initialized = 0;

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        UnitR3EventHooks.OnUnitDelete.Observable.Subscribe(OnUnitDelete);
        UnitR3EventHooks.OnUnitUnityVisualInterpolate.Observable.Subscribe(OnUnitUnityVisualInterpolate);
        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    internal void Unload()
    {
        RemoveAll();
    }

    private static void OnUnloadMap(EventAPI.MapLoader.MapUnloadEventArgs e)
    {
        LogHelper.Information("Unloading active effects");
        Instance.Unload();
    }

    private static void OnUnitDelete(EventAPI.Units.UnitDeleteEventArgs e)
    {
        if (e.Phase == EventHookPhase.Pre)
            return;

        Instance.RemoveEffect((int)e.UnitId);
    }

    private static void OnUnitUnityVisualInterpolate(EventAPI.Units.UnitUnityVisualInterpolateEventArgs e)
    {
        GameAfterImageManager.Instance.UpdateEffectForChimp(e.Chimp);
    }

    /// <summary>
    /// Holds the user-defined parameters for an after-image effect.
    /// </summary>
    private class EffectParameters
    {
        public float SpawnRate;
        public int MaxImages;
        public float FadeSpeed;
        public float DriftSpeed;
        public Vector3 DriftDirection;

        // Floaty motion properties for the ghosts
        public bool IsFloatyEffectActive;
        public float FloatAmplitude;
        public float FloatSpeed;
    }

    /// <summary>
    /// Holds the runtime data required to render the effect, like the pool of sprites.
    /// </summary>
    private class RuntimeData
    {
        public float Timer;
        public bool IsPoolInitialized;
        public GameObject PoolContainer; // Parent object for all ghosts to ensure proper cleanup.
        public readonly List<SpriteRenderer> Ghosts = new();
        public readonly List<float> Alphas = new();
        public readonly List<float> FloatTimers = new();
    }

    /// <summary>
    /// Main dictionary that tracks active effects, keyed by the unit's ID.
    /// </summary>
    private readonly Dictionary<int, (EffectParameters, RuntimeData)> _activeEffects = new();

    /// <summary>
    /// Removes all current active effects.
    /// </summary>
    [LuaApiExport("RemoveAllAfterImageEffects")]
    public void RemoveAll()
    {
        List<int> keys = new List<int>(_activeEffects.Keys);
        foreach (int unitId in keys)
        {
            RemoveEffect(unitId);
        }
        _activeEffects.Clear();
    }

    /// <summary>
    /// Adds or updates an after-image effect for a specific unit.
    /// </summary>
    [LuaApiExport("AddAfterImageEffect")]
    public unsafe void AddEffect(int unitId, float spawnRate, int maxImages, float fadeSpeed, float driftSpeed, System.Numerics.Vector3 driftDirection)
    {
        if (unitId <= 0) 
            return;

        if (_activeEffects.ContainsKey(unitId))
        {
            RemoveEffect(unitId);
        }

        UnityEngine.Vector3 uVec3 = *(UnityEngine.Vector3*)&driftDirection;
        EffectParameters parameters = new EffectParameters
        {
            SpawnRate = spawnRate,
            MaxImages = Math.Max(1, maxImages), // Ensure at least one image.
            FadeSpeed = fadeSpeed,
            DriftSpeed = driftSpeed,
            DriftDirection = uVec3.normalized,
            IsFloatyEffectActive = false
        };

        _activeEffects[unitId] = (parameters, new RuntimeData());
    }

    /// <summary>
    /// Removes the after-image effect from a unit and cleans up its associated GameObjects.
    /// </summary>
    [LuaApiExport("RemoveAfterImageEffect")]
    public void RemoveEffect(int unitId)
    {
        if (_activeEffects.TryGetValue(unitId, out (EffectParameters, RuntimeData) effect))
        {
            // Destroy the container GameObject, which will also destroy all its children (the ghosts).
            UnityMainThreadDispatcher.Instance.Enqueue(() =>
            {
                if (effect.Item2.PoolContainer != null)
                {
                    UnityEngine.Object.Destroy(effect.Item2.PoolContainer);
                }
            });
            _activeEffects.Remove(unitId);
        }
    }

    /// <summary>
    /// Enables the floating effect of an existing after-image affected unit.
    /// </summary>
    [LuaApiExport("AddFloatyMotionToAfterImage")]
    public void AddFloatyMotionToEffect(int unitId, float amplitude, float speed)
    {
        if (_activeEffects.TryGetValue(unitId, out (EffectParameters, RuntimeData) effect))
        {
            effect.Item1.IsFloatyEffectActive = true;
            effect.Item1.FloatAmplitude = amplitude;
            effect.Item1.FloatSpeed = speed;
        }
    }

    /// <summary>
    /// Removes the floating effect from a existing after-image affected unit.
    /// </summary>
    [LuaApiExport("Unit_RemoveFloatyMotionFromAfterImage")]
    public void RemoveFloatyMotionFromEffect(int unitId)
    {
        if (_activeEffects.TryGetValue(unitId, out var effect))
        {
            effect.Item1.IsFloatyEffectActive = false;
        }
    }


    /// <summary>
    /// This is the main update logic.
    /// NOTE: THIS IS RUN FROM THE UNITY MAIN THREAD
    /// </summary>
    public void UpdateEffectForChimp(Chimp this_chimp)
    {
        if (this_chimp == null || !_activeEffects.TryGetValue(this_chimp.objectID, out (EffectParameters, RuntimeData) effect))
        {
            return;
        }

        (EffectParameters parameters, RuntimeData? runtimeData) = effect;

        // Initialize the object pool on the first run.
        if (!runtimeData.IsPoolInitialized)
        {
            InitializePool(this_chimp, runtimeData, parameters.MaxImages);
        }

        // --- Update and fade existing ghosts ---
        UpdateAndFadeGhosts(runtimeData, parameters);

        // --- Spawn a new ghost based on the timer ---
        SpawnNewGhost(this_chimp, runtimeData, parameters);
    }

    private void InitializePool(Chimp sourceChimp, RuntimeData runtimeData, int maxImages)
    {
        runtimeData.PoolContainer = new GameObject($"Afterimage_Container_{sourceChimp.objectID}");

        for (int i = 0; i < maxImages; i++)
        {
            GameObject go = new GameObject($"Afterimage_{i}");
            go.transform.SetParent(runtimeData.PoolContainer.transform);

            SpriteRenderer sr = go.AddComponent<SpriteRenderer>();
            sr.sortingLayerID = sourceChimp.sprRenderer.sortingLayerID;
            sr.enabled = false;
            runtimeData.Ghosts.Add(sr);
            runtimeData.Alphas.Add(0f);

            runtimeData.FloatTimers.Add(0f);
        }
        runtimeData.IsPoolInitialized = true;
    }



    private void UpdateAndFadeGhosts(RuntimeData runtimeData, EffectParameters parameters)
    {
        for (int i = 0; i < runtimeData.Ghosts.Count; i++)
        {
            SpriteRenderer sr = runtimeData.Ghosts[i];
            if (!sr.enabled) 
                continue;

            // --- Fading Logic ---
            runtimeData.Alphas[i] -= Time.deltaTime * parameters.FadeSpeed;
            if (runtimeData.Alphas[i] <= 0f)
            {
                sr.enabled = false;
                continue;
            }

            Color c = sr.color;
            c.a = runtimeData.Alphas[i];
            sr.color = c;

            // --- Motion Logic ---
            Vector3 currentPosition = sr.transform.position;

            // Apply linear drift
            Vector3 driftThisFrame = parameters.DriftDirection * (parameters.DriftSpeed * Time.deltaTime);
            Vector3 newPosition = currentPosition + driftThisFrame;

            // Apply optional floaty motion
            if (parameters.IsFloatyEffectActive)
            {
                // Increment this specific ghost's timer
                runtimeData.FloatTimers[i] += Time.deltaTime;

                // To prevent the floaty motion from being cumulative, we need a baseline.
                // The easiest way is to calculate the floaty "velocity" and add it.
                float floatYVelocity = parameters.FloatAmplitude * parameters.FloatSpeed
                                       * Mathf.Cos(runtimeData.FloatTimers[i] * parameters.FloatSpeed);

                newPosition.y += floatYVelocity * Time.deltaTime;
            }

            // Commit the final position
            sr.transform.position = newPosition;
        }
    }

    private void SpawnNewGhost(Chimp sourceChimp, RuntimeData runtimeData, EffectParameters parameters)
    {
        runtimeData.Timer += Time.deltaTime;
        if (runtimeData.Timer < parameters.SpawnRate) 
            return;

        runtimeData.Timer = 0f;

        // Find the first disabled ghost in the pool to reuse.
        int idx = -1;
        for (int i = 0; i < runtimeData.Ghosts.Count; i++)
        {
            if (!runtimeData.Ghosts[i].enabled)
            {
                idx = i;
                break;
            }
        }

        // If all ghosts are active, recycle the first one (the oldest).
        if (idx == -1) 
            idx = 0;

        SpriteRenderer sr = runtimeData.Ghosts[idx];

        // Copy all visual properties from the source unit.
        sr.sprite = sourceChimp.sprRenderer.sprite;
        sr.color = sourceChimp.sprRenderer.color;
        sr.sortingOrder = sourceChimp.sprRenderer.sortingOrder - 1; // Render behind the main unit
        sr.transform.position = sourceChimp.gameObject.transform.position;
        sr.transform.rotation = sourceChimp.gameObject.transform.rotation;
        sr.transform.localScale = sourceChimp.gameObject.transform.localScale;
        sr.enabled = true;

        runtimeData.Alphas[idx] = 1f;
        runtimeData.FloatTimers[idx] = UnityEngine.Random.Range(0f, 100f);
    }
}