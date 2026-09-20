using R3;
using SHCDESE.EventAPI;
using SHCDESE.Extensions;
using SHCDESE.Logging;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace SHCDESE.API;

public class GameBundleManagerAPI
{
    private static readonly Lazy<GameBundleManagerAPI> _lazy = new(() => new GameBundleManagerAPI());
    public static GameBundleManagerAPI Instance => _lazy.Value;

    // Cache: Maps relative path (key) to Loaded Bundle
    private Dictionary<string, AssetBundle> _loadedBundles = new Dictionary<string, AssetBundle>(StringComparer.OrdinalIgnoreCase);

    // List of bundles to unload when the map changes (to free memory)
    private HashSet<string> _transientBundles = new HashSet<string>();

    private GameBundleManagerAPI()
    {

    }
    private int _initialized = 0;

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnUnloadMap);
    }

    internal void Unload()
    {
        UnloadTransientBundles();
    }

    private static void OnUnloadMap(EventAPI.MapLoader.MapUnloadEventArgs e)
    {
        LogHelper.Information("Unloading");
        Instance.Unload();
    }

    /// <summary>
    /// Unloads all bundles marked as transient (loaded with unloadOnMapChange=true).
    /// </summary>
    private void UnloadTransientBundles()
    {
        // Unity AssetBundle APIs MUST be called on the main thread.
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            if (_transientBundles.Count == 0)
                return;

            int count = 0;

            // Iterate over a copy or simply iterate the set since we clear it at the end
            foreach (string bundleKey in _transientBundles)
            {
                if (_loadedBundles.TryGetValue(bundleKey, out AssetBundle? bundle))
                {
                    // Remove from main cache
                    _loadedBundles.Remove(bundleKey);

                    if (bundle != null)
                    {
                        try
                        {
                            // This ensures we don't leak textures/meshes when the map closes.
                            bundle.Unload(unloadAllLoadedObjects: true);
                            count++;

                            LogHelper.Information($"Unloaded bundle: [{bundleKey}]");
                        }
                        catch (Exception ex)
                        {
                            LogHelper.Error(ex, $"Failed to unload bundle: [{bundleKey}]");
                        }
                    }
                }
            }

            // Clear the tracking list
            _transientBundles.Clear();

            if (count > 0)
                LogHelper.Information($"Unloaded {count} transient bundles.");
        });
    }

    /// <summary>
    /// Loads an AssetBundle from the Override directory (Synchronous).
    /// </summary>
    /// <param name="relativePath">e.g. "AssetBundles/MyMod_bundle"</param>
    /// <param name="unloadOnMapChange">If true, this bundle is unloaded when the map/mission ends.</param>
    public AssetBundle? LoadBundle(string relativePath, bool unloadOnMapChange = false)
    {
        string normalizedKey = relativePath.Replace('\\', '/');

        // Check Cache
        if (_loadedBundles.TryGetValue(normalizedKey, out AssetBundle? cachedBundle))
        {
            if (cachedBundle != null) 
                return cachedBundle;
            _loadedBundles.Remove(normalizedKey); // Handle null/unloaded entry
        }

        // Resolve Path via AssetManager
        if (!GameAssetManagerAPI.Instance.GetModifiedFilePath(normalizedKey, out string diskPath))
        {
            LogHelper.Error($"AssetBundle file not found in Index: [{relativePath}]");
            return null;
        }

        // Load
        try
        {
            AssetBundle bundle = AssetBundle.LoadFromFile(diskPath);
            if (bundle == null)
            {
                LogHelper.Error($"Failed to load AssetBundle from disk: [{diskPath}]");
                return null;
            }

            _loadedBundles[normalizedKey] = bundle;

            if (unloadOnMapChange)
                _transientBundles.Add(normalizedKey);

            LogHelper.Information($"Loaded AssetBundle: [{normalizedKey}]");
            return bundle;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Exception loading bundle: [{relativePath}]");
            return null;
        }
    }

    /// <summary>
    /// Loads an AssetBundle Asynchronously (Lua/Coroutine friendly).
    /// </summary>
    public void LoadBundleAsync(string relativePath, Action<AssetBundle?> onComplete, bool unloadOnMapChange = false)
    {
        UnityMainThreadDispatcher.Instance.Enqueue(() =>
        {
            UnityMainThreadDispatcher.Instance.StartCoroutine(LoadBundleCoroutine(relativePath, onComplete, unloadOnMapChange));
        });
    }

    private IEnumerator LoadBundleCoroutine(string relativePath, Action<AssetBundle?> onComplete, bool unloadOnMapChange)
    {
        string normalizedKey = relativePath.Replace('\\', '/');

        // Check Cache
        if (_loadedBundles.TryGetValue(normalizedKey, out AssetBundle? cachedBundle) && cachedBundle != null)
        {
            onComplete?.Invoke(cachedBundle);
            yield break;
        }

        // Resolve Path
        if (!GameAssetManagerAPI.Instance.GetModifiedFilePath(normalizedKey, out string diskPath))
        {
            LogHelper.Error($"AssetBundle not found: [{relativePath}]");
            onComplete?.Invoke(null);
            yield break;
        }

        // Async Load
        AssetBundleCreateRequest request = AssetBundle.LoadFromFileAsync(diskPath);
        yield return request;

        AssetBundle bundle = request.assetBundle;
        if (bundle != null)
        {
            _loadedBundles[normalizedKey] = bundle;
            if (unloadOnMapChange) 
                _transientBundles.Add(normalizedKey);
        }

        onComplete?.Invoke(bundle);
    }

    /// <summary>
    /// Unloads a specific bundle and frees memory.
    /// </summary>
    public void UnloadBundle(string relativePath, bool unloadAllLoadedObjects = true)
    {
        string normalizedKey = relativePath.Replace('\\', '/');
        if (_loadedBundles.TryGetValue(normalizedKey, out AssetBundle? bundle))
        {
            if (bundle != null)
            {
                bundle.Unload(unloadAllLoadedObjects);
            }
            _loadedBundles.Remove(normalizedKey);
            _transientBundles.Remove(normalizedKey);
        }
    }

    /// <summary>
    /// Helper to load a prefab/asset from a loaded bundle.
    /// </summary>
    public T? LoadAssetFromBundle<T>(string bundlePath, string assetName) where T : UnityEngine.Object
    {
        if (_loadedBundles.TryGetValue(bundlePath.Replace('\\', '/'), out AssetBundle? bundle) && bundle != null)
        {
            return bundle.LoadAsset<T>(assetName);
        }
        LogHelper.Warning($"Bundle not loaded or not found: [{bundlePath}]");
        return null;
    }

    /// <summary>
    /// Loads a material and ensures its shader is explicitly linked from the bundle.
    /// Only use if AssetBundle.LoadAsset{T}() is not working for some reason.
    /// </summary>
    public Material? LoadMaterialWithShader(string bundlePath, string materialName, string shaderName)
    {
        if (_loadedBundles.TryGetValue(bundlePath.Replace('\\', '/'), out AssetBundle? bundle) && bundle != null)
        {
            Material? mat = bundle.LoadAsset<Material>(materialName);
            Shader? shader = bundle.LoadAsset<Shader>(shaderName);

            if (mat != null)
            {
                if (shader != null)
                {
                    // Force the link
                    mat.shader = shader;

                    if (!shader.isSupported)
                    {
                        LogHelper.Warning($"Shader [{shaderName}] is loaded but .isSupported is FALSE on this hardware/game config.");
                    }
                }
                else
                {
                    LogHelper.Error($"Failed to load Shader [{shaderName}] from {bundlePath}");
                }
            }
            else
            {
                LogHelper.Error($"Failed to load Material [{materialName}] from {bundlePath}");
            }
            return mat;
        }
        return null;
    }
}