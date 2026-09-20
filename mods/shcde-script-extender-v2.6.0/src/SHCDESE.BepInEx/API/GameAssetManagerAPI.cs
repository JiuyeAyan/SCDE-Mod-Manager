using BepInEx;
using SHCDESE.API.Components.Archive;
using SHCDESE.API.Components.Assets;
using SHCDESE.API.Components.Sprite;
using SHCDESE.Extensions;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Reflection;
using System.Text.Json;
using UnityEngine;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-performance, centralized API for overriding game assets at runtime.
/// </summary>
/// <remarks>
/// <para>
/// This manager utilizes a <b>Pre-Indexed</b> architecture to minimize disk I/O.
/// When mods register a loose directory or packed source, it is enumerated once and a mapping of
/// normalized paths to lazy resources is built in memory.
/// </para>
/// </remarks>
public sealed class GameAssetManagerAPI
{
    private static readonly Lazy<GameAssetManagerAPI> _lazy = new(() => new GameAssetManagerAPI());
    public static GameAssetManagerAPI Instance => _lazy.Value;

    internal const string DEFAULT_OVERRIDE_RELATIVE_PATH = "Override";

    internal const string ATLAS_OVERRIDE_FOLDER = "Atlas";

    /// <summary>
    /// Describes the unique per-asset localization which operates from the
    /// Override folder. Not associatred with <see cref="GameTranslateAPI.LOCALE_FOLDER_PREFIX"/>
    /// </summary>
    internal const string LOCALE_FOLDER_PREFIX = "Locales";

    internal const string DEFAULT_LOCALE = "en-US";

    // Public overlay: normalized game path -> the last provider that supplied it.
    private readonly Dictionary<string, IndexedModResource> _fileIndex = new(StringComparer.OrdinalIgnoreCase);

    // Private mod namespace: GUID -> (mod-relative path -> resource).
    private readonly Dictionary<string, Dictionary<string, IndexedModResource>> _modFileIndexes = new(StringComparer.OrdinalIgnoreCase);

    // XAML target path -> patch resources in load order.
    private readonly Dictionary<string, List<IndexedModResource>> _patchIndex = new(StringComparer.OrdinalIgnoreCase);

    // Sparse sprite set used to avoid probing every vanilla sprite.
    private readonly Dictionary<string, IndexedModResource> _spriteOverrides = new(StringComparer.OrdinalIgnoreCase);

    // Sources remain open for lazy reads from .semod archives.
    private readonly List<IModResourceSource> _sources = new();

    // The Object Cache: Maps "Music/track.wav" (The Game's Request) -> AudioClip (The Mod's .ogg)
    private Dictionary<string, AudioClip> _audioClipCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Texture2D> _textureCache = new(StringComparer.OrdinalIgnoreCase);

    // Prevent re-scanning already registered plugins
    private readonly HashSet<string> _registeredDirectories = new(StringComparer.OrdinalIgnoreCase);

    public IEnumerable<string> GetRegisteredDirectories() => _registeredDirectories;

    public string CurrentLanguage { get; set; } = DEFAULT_LOCALE;

    private GameAssetManagerAPI()
    {
        _audioClipCache = new(StringComparer.OrdinalIgnoreCase);
        _textureCache = new(StringComparer.OrdinalIgnoreCase);
    }

    internal void Unload()
    {
        _fileIndex.Clear();
        _modFileIndexes.Clear();
        _patchIndex.Clear();
        _spriteOverrides.Clear();
        _audioClipCache.Clear();
        _textureCache.Clear();
        _registeredDirectories.Clear();
        foreach (IModResourceSource source in _sources)
            source.Dispose();
        _sources.Clear();
    }

    /// <summary>
    /// Represents the method signature for modifying text asset content during the loading process.
    /// </summary>
    /// <param name="relativePath">The relative path of the asset being processed (e.g., <c>"Assets/GUI/Views/MainView.xaml"</c>).</param>
    /// <param name="text">
    /// A reference to the loaded text content. Subscribers can modify or completely replace this string
    /// to apply patches, inject XML, or perform replacements.
    /// </param>
    public delegate void TextFileAssetProcessDelegate(string relativePath, ref string text);

    /// <summary>
    /// Occurs when a text asset (such as XAML or XML) has been read from disk but before it is returned to the game engine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Subscribe to this event to perform runtime modification of assets.
    /// This is the primary hook used by the internal <c>GameXAMLManagerAPI</c> to apply XML patches.
    /// </para>
    /// <para>
    /// <b>Note:</b> Because the text is passed by reference, modifications made by one subscriber
    /// are persistent and visible to subsequent subscribers in the invocation list.
    /// </para>
    /// </remarks>
    public event TextFileAssetProcessDelegate? OnTextFileAssetProcess;

    /// <summary>
    /// Retrieve the default asset overrie plugin for the calling mod
    /// </summary>
    /// <returns>Returns %MOD_PLUGIN_FOLDER%/DEFAULT_OVERRIDE_RELATIVE_PATH</returns>
    public string GetCallerDefaultOverrideAssetDirectory()
    {
        return Path.GetDirectoryName(Assembly.GetCallingAssembly().Location) + Path.DirectorySeparatorChar + DEFAULT_OVERRIDE_RELATIVE_PATH;
    }

    /// <inheritdoc cref="RegisterAssetProvider(PluginInfo, string)" />
    [Obsolete("Don't use this function. Asset Mods are now supported natively.")]
    public void RegisterAssetProvider(BaseUnityPlugin plugin, string directory = "") => RegisterAssetProvider(plugin.Info, directory);

    /// <summary>
    /// Registers a plugin asset directory into the central file index.
    /// For use with a PluginInfo.
    /// </summary>
    [Obsolete("Don't use this function. Asset Mods are now supported natively.")]
    public void RegisterAssetProvider(PluginInfo pluginInfo, string directory = "")
    {
        if (string.IsNullOrEmpty(directory))
        {
            directory = Path.Combine(Path.GetDirectoryName(pluginInfo.Location), DEFAULT_OVERRIDE_RELATIVE_PATH);
        }
        RegisterAssetProvider(pluginInfo.Metadata.GUID, directory);
    }

    /// <summary>
    /// Registers a plugin asset directory into the central file index.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This method recursively scans the target directory and maps all found files to their relative paths.
    /// </para>
    /// <para>
    /// If multiple mods provide the same file (e.g., <c>Sounds/Music/theme.ogg</c>), the last mod to register
    /// will overwrite the entry in the index (Last-Write-Wins).
    /// </para>
    /// </remarks>
    /// <param name="guid">The guid of the mod</param>
    /// <param name="directory">
    /// The absolute path to the override directory.
    /// If left empty, defaults to <c>%PLUGIN_DIR%/Override</c>.
    /// </param>
    public void RegisterAssetProvider(string guid, string directory)
    {
        if (_registeredDirectories.Contains(directory) || !Directory.Exists(directory))
            return;

        RegisterOverrideSource(guid, new DirectoryResourceSource(directory));
    }

    /// <summary>
    /// Indexes a complete mod source once and derives public overrides, private resources, patches, atlases, and sparse sprite overrides from that pass.
    /// </summary>
    internal bool RegisterModProvider(string guid, IModResourceSource source)
    {
        if (!_registeredDirectories.Add(source.ContainerPath))
        {
            source.Dispose();
            return false;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        int fileCount = 0;
        List<string> atlasDefinitions = new();
        Dictionary<string, IndexedModResource> modFiles = new(StringComparer.OrdinalIgnoreCase);

        try
        {
            foreach (string relativePath in source.EnumerateFiles())
            {
                IndexedModResource resource = new IndexedModResource(guid, relativePath, source);
                modFiles[relativePath] = resource;
                fileCount++;
            }

            foreach (KeyValuePair<string, IndexedModResource> entry in modFiles)
            {
                string relativePath = entry.Key;
                IndexedModResource resource = entry.Value;
                const string overridePrefix = DEFAULT_OVERRIDE_RELATIVE_PATH + "/";
                if (relativePath.StartsWith(overridePrefix, StringComparison.OrdinalIgnoreCase))
                {
                    IndexOverride(relativePath.Substring(overridePrefix.Length), resource, atlasDefinitions);
                    continue;
                }

                const string patchPrefix = GameXAMLManagerAPI.DEFAULT_XAML_PATCH_RELATIVE_PATH + "/";
                if (relativePath.StartsWith(patchPrefix, StringComparison.OrdinalIgnoreCase))
                {
                    string targetPath = relativePath.Substring(patchPrefix.Length);
                    if (!_patchIndex.TryGetValue(targetPath, out List<IndexedModResource>? patches))
                    {
                        patches = new List<IndexedModResource>();
                        _patchIndex[targetPath] = patches;
                    }
                    patches.Add(resource);
                }
            }

            _modFileIndexes[guid] = modFiles;
            _sources.Add(source);
            DiscoverAtlasOverrides(atlasDefinitions, source.DisplayName);
            stopwatch.Stop();
            LogHelper.Information($"Indexed {fileCount} files for [{guid}] from [{source.DisplayName}] in {stopwatch.ElapsedMilliseconds} ms.");
            return true;
        }
        catch (Exception ex)
        {
            source.Dispose();
            _registeredDirectories.Remove(source.ContainerPath);
            LogHelper.Error(ex, $"Failed to index mod source: {source.DisplayName}");
            return false;
        }
    }

    private void RegisterOverrideSource(string guid, IModResourceSource source)
    {
        if (!_registeredDirectories.Add(source.ContainerPath))
        {
            source.Dispose();
            return;
        }

        Stopwatch stopwatch = Stopwatch.StartNew();
        int fileCount = 0;
        List<string> atlasDefinitions = new();
        try
        {
            foreach (string relativePath in source.EnumerateFiles())
            {
                IndexOverride(relativePath, new IndexedModResource(guid, relativePath, source), atlasDefinitions);
                fileCount++;
            }

            _sources.Add(source);
            DiscoverAtlasOverrides(atlasDefinitions, source.DisplayName);
            stopwatch.Stop();
            LogHelper.Information($"Indexed {fileCount} override files for [{guid}] from [{source.DisplayName}] in {stopwatch.ElapsedMilliseconds} ms.");
        }
        catch (Exception ex)
        {
            source.Dispose();
            _registeredDirectories.Remove(source.ContainerPath);
            LogHelper.Error(ex, $"Failed to index override source: {source.DisplayName}");
        }
    }

    private void IndexOverride(string relativePath, IndexedModResource resource, List<string> atlasDefinitions)
    {
        _fileIndex[relativePath] = resource;

        const string spritePrefix = "Sprites/";
        if (relativePath.StartsWith(spritePrefix, StringComparison.OrdinalIgnoreCase) && relativePath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            string spriteName = relativePath.Substring(spritePrefix.Length, relativePath.Length - spritePrefix.Length - 4);
            if (spriteName.IndexOf('/') < 0 && !spriteName.EndsWith("_m", StringComparison.OrdinalIgnoreCase))
                _spriteOverrides[spriteName] = resource;
        }

        if (relativePath.StartsWith(ATLAS_OVERRIDE_FOLDER + "/", StringComparison.OrdinalIgnoreCase) && relativePath.EndsWith("/atlas.json", StringComparison.OrdinalIgnoreCase))
            atlasDefinitions.Add(relativePath);
    }

    /// <summary>
    /// Reads an optional material file belonging to the same provider as the winning sprite override. 
    /// Keeping both resources in one source prevents the material file from a lower-priority mod from leaking into another mod's sprite.
    /// </summary>
    internal bool TryGetSpriteMaterialMode(string spriteName, out SpriteMaterialMode mode, out bool invalidSidecar)
    {
        mode = SpriteMaterialMode.Auto;
        invalidSidecar = false;
        if (!_spriteOverrides.TryGetValue(spriteName, out IndexedModResource? spriteResource))
            return false;

        string extension = Path.GetExtension(spriteResource.Path);
        string sidecarPath = spriteResource.Path.Substring(0, spriteResource.Path.Length - extension.Length) + ".material.json";
        if (!spriteResource.Source.Contains(sidecarPath))
            return false;

        try
        {
            IndexedModResource sidecar = new(spriteResource.ModGuid, sidecarPath, spriteResource.Source);
            using JsonDocument document = JsonDocument.Parse(sidecar.ReadAllText(), new JsonDocumentOptions
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            });

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || 
                !TryGetPropertyIgnoreCase(root, "material", out JsonElement value) ||
                value.ValueKind != JsonValueKind.String ||
                !SpriteMaterialModeParser.TryParse(value.GetString(), out mode))
            {
                LogHelper.Warning($"Invalid sprite material sidecar [{sidecarPath}]. Expected a material value of Auto, Plain, TeamColour, or Foliage.");
                mode = SpriteMaterialMode.Auto;
                invalidSidecar = true;
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to read sprite material sidecar [{sidecarPath}]");
            mode = SpriteMaterialMode.Auto;
            invalidSidecar = true;
            return false;
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Helper to retrieve the absolute path of a registered asset file.
    /// </summary>
    /// <param name="relativePath">The relative path key.</param>
    /// <param name="absolutePath">The resolved absolute path on disk.</param>
    /// <returns><c>true</c> if found in the index; otherwise, <c>false</c>.</returns>
    public bool GetModifiedFilePath(string relativePath, out string absolutePath)
    {
        string normalizedKey = relativePath.Replace('\\', '/');
        if (_fileIndex.TryGetValue(normalizedKey, out IndexedModResource? resource))
            return resource.TryGetPhysicalPath(out absolutePath);

        absolutePath = string.Empty;
        return false;
    }

    internal bool TryGetModifiedResource(string relativePath, [NotNullWhen(true)] out IndexedModResource? resource)
    {
        string normalizedKey = relativePath.Replace('\\', '/');
        return _fileIndex.TryGetValue(normalizedKey, out resource);
    }

    internal IEnumerable<string> GetSpriteOverrideNames() => _spriteOverrides.Keys;

    internal IReadOnlyList<IndexedModResource> GetPatchesForPath(string relativePath)
    {
        string normalizedKey = relativePath.Replace('\\', '/').TrimStart('/');
        return _patchIndex.TryGetValue(normalizedKey, out List<IndexedModResource>? patches) ? patches : Array.Empty<IndexedModResource>();
    }

    internal IEnumerable<string> GetModFilePaths(string guid, string prefix = "")
    {
        if (!_modFileIndexes.TryGetValue(guid, out Dictionary<string, IndexedModResource>? files))
            yield break;

        string normalizedPrefix = prefix.Replace('\\', '/').TrimStart('/');
        foreach (string path in files.Keys)
        {
            if (normalizedPrefix.Length == 0 || path.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase))
                yield return path;
        }
    }

    public bool GetModFileTextContent(string guid, string relativePath, out string content)
    {
        if (TryGetModResource(guid, relativePath, out IndexedModResource? resource))
        {
            content = resource.ReadAllText();
            return true;
        }

        content = string.Empty;
        return false;
    }

    public bool GetModFileBinaryContent(string guid, string relativePath, [NotNullWhen(true)] out byte[]? content)
    {
        if (TryGetModResource(guid, relativePath, out IndexedModResource? resource))
        {
            content = resource.ReadAllBytes();
            return true;
        }

        content = null;
        return false;
    }

    internal bool TryGetModFilePath(string guid, string relativePath, out string physicalPath)
    {
        if (TryGetModResource(guid, relativePath, out IndexedModResource? resource))
            return resource.TryGetPhysicalPath(out physicalPath);

        physicalPath = string.Empty;
        return false;
    }

    private bool TryGetModResource(string guid, string relativePath, [NotNullWhen(true)] out IndexedModResource? resource)
    {
        resource = null;
        if (!ModResourcePath.TryNormalize(relativePath, out string normalized))
            return false;
        return _modFileIndexes.TryGetValue(guid, out Dictionary<string, IndexedModResource>? files)
            && files.TryGetValue(normalized, out resource);
    }

    // ---------------------------------------------------------------------------------------
    // TEXTURE HANDLING
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts to load a Texture2D from the registered mod assets.
    /// </summary>
    /// <remarks>
    /// Checks the memory cache first. If the texture is not cached, it attempts to resolve the path
    /// via the File Index (checking for <c>.png</c>, <c>.jpg</c>, <c>.tga</c> if no extension is provided).
    /// </remarks>
    /// <param name="relativePath">The relative URI or path of the texture (e.g., <c>Assets/GUI/image.png</c>).</param>
    /// <param name="texture">The loaded Texture2D if found; otherwise, <c>null</c>.</param>
    /// <returns><c>true</c> if a texture was loaded; otherwise, <c>false</c>.</returns>
    internal bool TryLoadTexture(string relativePath, [NotNullWhen(true)] out Texture2D? texture)
    {
        LogHelper.Verbose($"Trying to resolve texture path: [{relativePath}]");

        // Check Cache first
        string normalizedKey = relativePath.Replace('\\', '/');
        if (_textureCache.TryGetValue(normalizedKey, out texture))
        {
            return texture != null;
        }

        // Resolve Path
        if (!TryResolveTextureResource(normalizedKey, out IndexedModResource? resource))
        {
            return false;
        }

        // Load from the source without extracting packed resources.
        try
        {
            LogHelper.Debug($"Trying to load resource: [{resource.Path}]");
            texture = TextureExtensions.LoadTexture(resource.ReadAllBytes());
            if (texture != null)
            {
                texture.name = Path.GetFileName(relativePath);
                _textureCache[normalizedKey] = texture;
                return true;
            }
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to load texture resource [{resource.Path}]");
        }
        return false;
    }

    private bool TryResolveTextureResource(string key, [NotNullWhen(true)] out IndexedModResource? resource)
    {
        if (_fileIndex.TryGetValue(key, out resource)) return true;
        if (GetLocalizedResource(key, out resource)) return true;

        // Helper to check extensions if exact match fails
        if (!Path.HasExtension(key))
        {
            if (GetLocalizedResource(key + ".png", out resource)) return true;
            if (GetLocalizedResource(key + ".jpg", out resource)) return true;
            if (GetLocalizedResource(key + ".tga", out resource)) return true;
        }
        resource = null;
        return false;
    }

    /// <summary>
    /// Scans the file index for entries matching "Atlas/{gmFileName}/atlas.json"
    /// and auto-registers them with GameAtlasManagerAPI.
    /// Runs after the file index is populated, so no extra filesystem scan is needed.
    /// </summary>
    private void DiscoverAtlasOverrides(IEnumerable<string> atlasDefinitions, string sourceName)
    {
        const string atlasJsonSuffix = "/atlas.json";
        string atlasPrefix = ATLAS_OVERRIDE_FOLDER + "/";

        foreach (string relativeKey in atlasDefinitions)
        {
            try
            {
                // Extract the folder name: "Atlas/body_bedouin_healer/atlas.json" -> "body_bedouin_healer"
                string middle = relativeKey.Substring(atlasPrefix.Length);         // "body_bedouin_healer/atlas.json"
                int separator = middle.IndexOf('/');
                if (separator <= 0 || !middle.EndsWith(atlasJsonSuffix.Substring(1), StringComparison.OrdinalIgnoreCase))
                    continue;
                string gmFileName = middle.Substring(0, separator);                // "body_bedouin_healer"

                if (string.IsNullOrEmpty(gmFileName))
                    continue;

                if (!GameAtlasManagerAPI.TryGetGMEnum(gmFileName, out Enums.GM gmFileID, out bool dashFormat))
                {
                    LogHelper.Warning($"Unknown GM file name [{gmFileName}] in [{relativeKey}]. " +
                                      $"Folder name must match a known GM sprite group.");
                    continue;
                }

                // Resolve sibling paths from the index (atlas.png and optional atlas_m.png)
                string atlasTextureKey = atlasPrefix + gmFileName + "/atlas.png";
                string maskTextureKey = atlasPrefix + gmFileName + "/atlas_m.png";

                if (!_fileIndex.TryGetValue(atlasTextureKey, out IndexedModResource? atlasTexture))
                {
                    LogHelper.Warning($"Found atlas.json for [{gmFileName}] but no atlas.png at [{atlasTextureKey}]. Skipping.");
                    continue;
                }

                _fileIndex.TryGetValue(maskTextureKey, out IndexedModResource? maskTexture);

                if (!_fileIndex.TryGetValue(relativeKey, out IndexedModResource? jsonResource))
                    continue;

                GameAtlasManagerAPI.Instance.RegisterAtlasOverride(new AtlasOverrideDefinition
                {
                    GmFileName = gmFileName,
                    GmFileID = gmFileID,
                    DashFormat = dashFormat,
                    AtlasTexturePath = string.Empty,
                    MaskTexturePath = string.Empty,
                    JsonPath = string.Empty,
                    AtlasTextureResource = atlasTexture,
                    MaskTextureResource = maskTexture,
                    JsonResource = jsonResource,
                });

                LogHelper.Information($"Auto-registered atlas override for [{gmFileName}] from [{sourceName}]");
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Failed to register atlas resource [{relativeKey}] from [{sourceName}]");
            }
        }
    }

    // ---------------------------------------------------------------------------------------
    // VIDEO HANDLING
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts to resolve a video path from the registered mod assets.
    /// Handles extension swapping (e.g. game requests .webm, mod provides .mp4).
    /// </summary>
    /// <param name="relativePath">The relative path requested by the game (e.g., <c>Assets/Video/file.webm</c>).</param>
    /// <param name="absolutePath">The resolved absolute path on disk.</param>
    /// <returns><c>true</c> if an override was found; otherwise, <c>false</c>.</returns>
    internal bool TryResolveVideoPath(string relativePath, out string absolutePath)
    {
        LogHelper.Debug($"Trying to resolve video path: [{relativePath}]");
        string normalizedKey = relativePath.Replace('\\', '/');

        // Try Exact Match
        if (_fileIndex.TryGetValue(normalizedKey, out IndexedModResource? exact)
            && exact.TryGetPhysicalPath(out absolutePath)) return true;

        // Try Extension Swapping
        string keyWithoutExt = Path.ChangeExtension(normalizedKey, null);

        // Priority list for video formats
        string[] supportedExtensions = [".webm", ".mp4"];

        foreach (string ext in supportedExtensions)
        {
            if (_fileIndex.TryGetValue(keyWithoutExt + ext, out IndexedModResource? replacement)
                && replacement.TryGetPhysicalPath(out absolutePath))
            {
                return true;
            }
        }

        absolutePath = string.Empty;
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // AUDIO HANDLING
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts to load an AudioClip from the registered mod assets, utilizing fuzzy path matching and caching.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Path Resolution Logic:</b>
    /// <list type="number">
    /// <item>Checks the internal cache. If found, returns immediately.</item>
    /// <item>Searches the File Index for an exact match (e.g., <c>Music/track.wav</c>).</item>
    /// <item>If exact match fails, searches for supported replacements (e.g., <c>Music/track.ogg</c>, <c>Music/track.mp3</c>).</item>
    /// </list>
    /// </para>
    /// <para>
    /// The resulting AudioClip is cached using the <i>original requested path</i> as the key.
    /// This ensures subsequent requests for <c>.wav</c> instantly return the loaded <c>.ogg</c>.
    /// </para>
    /// </remarks>
    /// <param name="relativePath">The relative path requested by the game (e.g., <c>Music/track.wav</c>).</param>
    /// <param name="clip">The loaded AudioClip if found; otherwise, <c>null</c>.</param>
    /// <param name="useCache">Set to false for Speech/Music which the game unloads after playing.</param>
    /// <returns><c>true</c> if a replacement clip was loaded; otherwise, <c>false</c>.</returns>
    internal bool TryLoadAudioClip(string relativePath, out AudioClip? clip, bool useCache = true)
    {
        LogHelper.Debug($"Trying to resolve audioclip: [{relativePath}]");
        string cacheKey = relativePath.Replace('\\', '/');

        if (useCache)
        {
            if (_audioClipCache.TryGetValue(cacheKey, out clip))
            {
                return clip != null;
            }
        }

        // Resolve the physical file path in the Index
        if (TryResolveAudioResource(cacheKey, out IndexedModResource? resource))
        {
            try
            {
                LogHelper.Debug($"Resolved audio clip: [{cacheKey}] to: [{resource.Path}]");
                clip = AudioClipExtensions.LoadAudioClipFromBytes(resource.ReadAllBytes());

                if (clip != null)
                {
                    clip.name = Path.GetFileName(relativePath);
                    //_audioClipCache[cacheKey] = clip;
                    if (useCache)
                    {
                        _audioClipCache[cacheKey] = clip;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Failed to load audio resource [{resource.Path}]");
            }
        }
        clip = null;
        return false;
    }

    /// <summary>
    /// Looks up the file in the Index, handling extension swapping.
    /// </summary>
    internal bool TryResolveAudioPath(string relativePath, out string diskPath)
    {
        if (TryResolveAudioResource(relativePath, out IndexedModResource? resource))
            return resource.TryGetPhysicalPath(out diskPath);
        diskPath = string.Empty;
        return false;
    }

    private bool TryResolveAudioResource(string relativePath, [NotNullWhen(true)] out IndexedModResource? resource)
    {
        LogHelper.Debug($"Trying to resolve audio: [{relativePath}]");
        string normalizedKey = relativePath.Replace('\\', '/');

        if (GetLocalizedResource(normalizedKey, out resource)) return true;

        string pathWithoutExt = Path.ChangeExtension(normalizedKey, null);
        if (GetLocalizedResource(pathWithoutExt + ".ogg", out resource)) return true;
        if (GetLocalizedResource(pathWithoutExt + ".wav", out resource)) return true;

        resource = null;
        return false;
    }

    /// <summary>
    /// Attempts to load an AudioClip directly from a <see cref="MapArchive"/>, bypassing the file index.
    /// Supports extension swapping (e.g. the entry is stored as .ogg but the caller requests .wav).
    /// </summary>
    /// <param name="archive">The map archive to search within.</param>
    /// <param name="relativePath">The relative path of the audio entry inside the archive (e.g., <c>sounds/moneyout.ogg</c>).</param>
    /// <param name="clip">The loaded AudioClip if found; otherwise, <c>null</c>.</param>
    /// <param name="useCache">If true, results are stored in and retrieved from the audio clip cache.</param>
    /// <returns><c>true</c> if the clip was successfully loaded; otherwise, <c>false</c>.</returns>
    internal bool TryLoadAudioClipFromArchive(MapArchive archive, string relativePath, [NotNullWhen(true)] out AudioClip? clip, bool useCache = true)
    {
        clip = null;

        if (archive?.Archive == null)
            return false;

        string cacheKey = "archive://" + relativePath.Replace('\\', '/');

        if (useCache && _audioClipCache.TryGetValue(cacheKey, out clip))
            return clip != null;

        // Build a candidate list: exact path first, then extension swaps
        string pathWithoutExt = Path.ChangeExtension(relativePath.Replace('\\', '/'), null);
        string[] candidates =
        [
            relativePath.Replace('\\', '/'),
            pathWithoutExt + ".ogg",
            pathWithoutExt + ".wav"
        ];

        foreach (string candidate in candidates)
        {
            byte[]? bytes = archive.TryReadBinaryFile(candidate, ignoreCase: true);
            if (bytes == null || bytes.Length == 0)
                continue;

            try
            {
                clip = AudioClipExtensions.LoadAudioClipFromBytes(bytes);
                if (clip == null)
                    continue;

                clip.name = Path.GetFileName(candidate);
                LogHelper.Debug($"Loaded audio clip from archive: [{candidate}]");

                if (useCache)
                    _audioClipCache[cacheKey] = clip;

                return true;
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Failed to decode audio from archive entry [{candidate}]");
            }
        }

        return false;
    }


    /// <summary>
    /// Extracts the actual audio file path from Unity's constructed paths.
    /// This strips away the Unity-specific prefixes and special folder markers.
    /// This is a purely edge-case solving method.
    /// </summary>
    /// <remarks>
    /// Examples:
    /// "StreamingAssets/EnglishSpeech/_SE_/fx\speech\youregonnapay" -> "fx/speech/youregonnapay"
    /// "StreamingAssets/EnglishSpeech/AI/lordknox_angry" -> "AI/lordknox_angry"
    /// "StreamingAssets/Music/track.wav" -> "Music/track.wav"
    /// "Assets/GUI/Speech/AI/lordknox_angry" -> "AI/lordknox_angry"
    /// </remarks>
    internal string ExtractAudioPath(string fullPath)
    {
        // Normalize separators
        string normalized = fullPath.Replace('\\', '/');

        // List of prefixes to strip, in order of priority
        string[] prefixesToStrip =
        [
            "StreamingAssets/EnglishSpeech/_SE_/",  // Special case
            "StreamingAssets/EnglishSpeech/",       // English speech folder
            "Assets/GUI/Speech/",                   // Non-English speech folder
            "StreamingAssets/Music/",               // Music folder
            "StreamingAssets/"                      // Generic StreamingAssets
        ];

        foreach (string prefix in prefixesToStrip)
        {
            int index = normalized.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
            if (index >= 0)
            {
                return normalized.Substring(index + prefix.Length).TrimStart('/');
            }
        }

        // If none of the prefixes match, try to extract just the filename and relative path
        // by looking for common indicators like "Assets/", "Data/", etc.
        if (normalized.Contains("/Assets/"))
        {
            int idx = normalized.IndexOf("/Assets/");
            return normalized.Substring(idx + 1).TrimStart('/'); // Keep "Assets/..." if it's the only thing we have
        }

        // Last resort: return the path as-is, cleaned
        return normalized.TrimStart('/');
    }

    // ---------------------------------------------------------------------------------------
    // TEXT FILE HANDLING
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Retrieves the text content of a file (typically XAML or XML), applying any registered runtime patches.
    /// </summary>
    /// <param name="relativePath">The relative path of the file.</param>
    /// <param name="content">The string content of the file, potentially modified by <see cref="OnTextFileAssetProcess"/>.</param>
    /// <returns><c>true</c> if the file was found and read; otherwise, <c>false</c>.</returns>
    public bool GetModifiedFileTextContent(string relativePath, out string content)
    {
        if (TryGetModifiedResource(relativePath, out IndexedModResource? resource))
        {
            content = resource.ReadAllText();
            OnTextFileAssetProcess?.Invoke(relativePath, ref content);
            return true;
        }

        content = string.Empty;
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // BINARY FILE HANDLING
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Retrieves the binary content of a file.
    /// </summary>
    /// <param name="relativePath">The relative path of the file.</param>
    /// <param name="content">The binary content of the file.</param>
    /// <returns><c>true</c> if the file was found and read; otherwise, <c>false</c>.</returns>
    public bool GetFileBinaryContent(string relativePath, [NotNullWhen(true)] out byte[] content)
    {
        if (TryGetModifiedResource(relativePath, out IndexedModResource? resource))
        {
            content = resource.ReadAllBytes();
            return true;
        }

        content = null!;
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // LOCALE HANDLING
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves a relative path by checking Locale specific folders first, then English, then Global.
    /// </summary>
    public bool GetLocalizedFilePath(string relativePath, out string absolutePath)
    {
        if (GetLocalizedResource(relativePath, out IndexedModResource? resource))
            return resource.TryGetPhysicalPath(out absolutePath);

        absolutePath = string.Empty;
        return false;
    }

    private bool GetLocalizedResource(string relativePath, [NotNullWhen(true)] out IndexedModResource? resource)
    {
        string normalizedKey = relativePath.Replace('\\', '/');

        // Check Target Language (e.g., "Locales/de-DE/Sounds/Hello.wav")
        if (!string.IsNullOrEmpty(CurrentLanguage))
        {
            string langKey = $"{LOCALE_FOLDER_PREFIX}/{CurrentLanguage}/{normalizedKey}";
            if (_fileIndex.TryGetValue(langKey, out resource)) return true;
        }

        // Check Fallback Language (English): "Locales/en-US/Sounds/Hello.wav"
        if (CurrentLanguage != DEFAULT_LOCALE)
        {
            string enKey = $"{LOCALE_FOLDER_PREFIX}/{DEFAULT_LOCALE}/{normalizedKey}";
            if (_fileIndex.TryGetValue(enKey, out resource)) return true;
        }

        // Check Global/Root (Legacy support and shared assets) :"Sounds/Hello.wav"
        return _fileIndex.TryGetValue(normalizedKey, out resource);
    }
}
