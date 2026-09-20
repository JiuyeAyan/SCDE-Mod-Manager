using SHCDESE.API.Components.Sprite;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

namespace SHCDESE.API;

internal class GameSpriteManagerAPI
{
    private static readonly Lazy<GameSpriteManagerAPI> _lazy = new(() => new GameSpriteManagerAPI());
    public static GameSpriteManagerAPI Instance => _lazy.Value;

    internal const string DEFAULT_OVERRIDE_SPRITES_PATH = "Sprites/";

    private GameSpriteManagerAPI()
    {
        // Create a 4x4 transparent texture to use as a default mask (No Team Color)
        _defaultEmptyMask = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color32[] pixels = new Color32[16];

        for (int i = 0; i < 16; i++)
            pixels[i] = new Color32(0, 0, 0, 0);

        _defaultEmptyMask.SetPixels32(pixels);
        _defaultEmptyMask.Apply();
    }

    private readonly struct CustomSpriteMaterial
    {
        internal CustomSpriteMaterial(Material[] materials, SpriteMaterialMode mode)
        {
            Materials = materials;
            Mode = mode;
        }

        internal Material[] Materials { get; }
        internal SpriteMaterialMode Mode { get; }
    }

    // Map Sprite InstanceID -> material and its rendering semantics.
    private readonly Dictionary<int, CustomSpriteMaterial> _customMaterialCache = new();

    // A blank mask (transparent) for sprites that don't provide one.
    private Texture2D _defaultEmptyMask;

    internal static void ApplyCustomMaterialColour(SpriteRenderer renderer, int file, int colour, int transparency, SpriteMaterialMode materialMode)
    {
        float alpha = (32 - transparency) / 32f;
        if (materialMode == SpriteMaterialMode.Plain)
        {
            Color plainColour = Color.white;
            plainColour.a = alpha;
            renderer.color = plainColour;
            return;
        }

        int paletteIndex = colour;
        if (file - 29 > 2 && file - 70 > 2 && file != 97 && colour >= 0 && colour <= 8)
            paletteIndex = SpriteMapping.remapColours[colour];

        if (file >= 0 && file < spriteLoader.instance.gmColors.Length)
        {
            Color[] palette = spriteLoader.instance.gmColors[file];
            if (palette != null && paletteIndex >= 0 && paletteIndex < palette.Length)
            {
                Color customColour = palette[paletteIndex];
                customColour.a = alpha;
                renderer.color = customColour;
                return;
            }
        }

        Color fallback = renderer.color;
        fallback.a = alpha;
        renderer.color = fallback;
    }
    public bool TryGetCustomMaterials(int spriteId, out Material[]? materials, out SpriteMaterialMode mode)
    {
        if (_customMaterialCache.TryGetValue(spriteId, out CustomSpriteMaterial entry))
        {
            materials = entry.Materials;
            mode = entry.Mode;
            return true;
        }

        materials = null;
        mode = SpriteMaterialMode.Auto;
        return false;
    }

    internal void RegisterCustomMaterials(int spriteId, Material[] materials, SpriteMaterialMode mode)
    {
        _customMaterialCache[spriteId] = new CustomSpriteMaterial(materials, mode);
    }

    /// <summary>
    /// This method is called automatically after the game finishes loading its vanilla sprites.
    /// </summary>
    internal void ApplyRuntimeOverrides(spriteLoader loaderInstance)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        if (loaderInstance == null)
        {
            LogHelper.Warning("spriteLoader instance is null. Cannot apply overrides.");
            return;
        }

        LogHelper.Information("Starting Sprite Override Injection...");

        // --- PHASE 1: Full atlas overrides (replaces entire GM sprite groups) ---
        // Must run BEFORE individual sprite overrides, so per-sprite overrides
        // can still win over atlas frames if both target the same sprite name.
        GameAtlasManagerAPI.Instance.ApplyAll(loaderInstance);

        // --- PHASE 2: Individual sprite overrides ---
        Dictionary<string, Sprite> allSprites = loaderInstance.allSprites;
        Dictionary<int, int> spriteToGmFile = BuildSpriteToGmFileMap(loaderInstance);
        int count = 0;
        int plainCount = 0;
        int teamColourCount = 0;
        int foliageCount = 0;
        int transparentMaskCount = 0;
        int ignoredMaskCount = 0;
        foreach (string spriteName in new List<string>(GameAssetManagerAPI.Instance.GetSpriteOverrideNames()))
        {
            if (!allSprites.TryGetValue(spriteName, out Sprite original))
            {
                LogHelper.Warning($"Sprite override [{spriteName}] does not match a loaded game sprite.");
                continue;
            }

            //LogHelper.Information($"Sprite=[{spriteName}]");
            if (TryGetOverrideTexture(spriteName, out Texture2D customTexture))
            {
                int fileID = spriteToGmFile.TryGetValue(original.GetInstanceID(), out int mappedFileID) ? mappedFileID : -1;
                bool hasExplicitMode = GameAssetManagerAPI.Instance.TryGetSpriteMaterialMode(spriteName, out SpriteMaterialMode requestedMode, out bool invalidMaterialSidecar);
                if (invalidMaterialSidecar)
                {
                    LogHelper.Warning($"Sprite override [{spriteName}] will not be applied because its material sidecar is invalid.");
                    continue;
                }

                SpriteMaterialMode resolvedMode = requestedMode;
                if (!hasExplicitMode || resolvedMode == SpriteMaterialMode.Auto)
                {
                    if (fileID >= 0 && SpriteMaterialUtility.TryDetectVanillaMode(loaderInstance, fileID, out SpriteMaterialMode vanillaMode, out _))
                    {
                        resolvedMode = vanillaMode;
                    }
                    else
                    {
                        // Unknown/non-GM sprites retain the legacy material as
                        // the least surprising compatibility fallback.
                        resolvedMode = SpriteMaterialMode.TeamColour;
                        LogHelper.Warning($"Could not determine the vanilla material for sprite [{spriteName}]. Falling back to TeamColour; add a .material.json sidecar to select explicitly.");
                    }
                }

                // Check for a matching mask file: "body_archer-001_m.png".
                Texture2D maskTexture = _defaultEmptyMask;
                bool hasCustomMask = TryGetOverrideTexture(spriteName + "_m", out Texture2D loadedMask);
                if (hasCustomMask)
                    maskTexture = loadedMask;

                bool ignoresCustomMask = resolvedMode == SpriteMaterialMode.Plain && hasCustomMask;
                bool usesTransparentMask = resolvedMode != SpriteMaterialMode.Plain && !hasCustomMask;

                if (!SpriteMaterialUtility.TryBuildMaterials(loaderInstance, fileID, resolvedMode, maskTexture, spriteName, out Material[]? customMaterials, out string shaderName))
                {
                    continue;
                }

                Sprite newSprite = CreateReplacementSprite(original, customTexture);
                RegisterCustomMaterials(newSprite.GetInstanceID(), customMaterials!, resolvedMode);
                allSprites[spriteName] = newSprite;
                count++;
                if (ignoresCustomMask)
                    ignoredMaskCount++;
                if (usesTransparentMask)
                    transparentMaskCount++;

                switch (resolvedMode)
                {
                    case SpriteMaterialMode.Plain:
                        plainCount++;
                        break;
                    case SpriteMaterialMode.Foliage:
                        foliageCount++;
                        break;
                    default:
                        teamColourCount++;
                        break;
                }

                LogHelper.Debug($"Sprite override [{spriteName}] resolved material={resolvedMode}, shader=[{shaderName}], gmFile={fileID}.");
            }
        }

        UpdateGMSpriteArrays(allSprites, loaderInstance);
        stopwatch.Stop();
        if (transparentMaskCount > 0)
            LogHelper.Warning($"{transparentMaskCount} TeamColour/Foliage sprite override(s) had no _m.png mask; " +
                              "transparent masks were used.");
        if (ignoredMaskCount > 0)
            LogHelper.Warning($"{ignoredMaskCount} Plain sprite override(s) supplied _m.png masks; those masks were ignored.");

        LogHelper.Information($"Replaced {count} sprites (Plain={plainCount}, TeamColour={teamColourCount}, Foliage={foliageCount}) in {stopwatch.ElapsedMilliseconds} ms.");
    }

    private static Dictionary<int, int> BuildSpriteToGmFileMap(spriteLoader loaderInstance)
    {
        Dictionary<int, int> result = new();
        for (int fileID = 0; fileID < loaderInstance.gmSprites.Length; fileID++)
        {
            AddSpriteArrayToGmFileMap(loaderInstance.gmSprites[fileID], fileID, result);
            if (fileID < loaderInstance.gmAltSprites.Length)
                AddSpriteArrayToGmFileMap(loaderInstance.gmAltSprites[fileID], fileID, result);
        }

        return result;
    }

    private static void AddSpriteArrayToGmFileMap(Sprite[]? sprites, int fileID, Dictionary<int, int> result)
    {
        if (sprites == null)
            return;

        foreach (Sprite sprite in sprites)
        {
            if (sprite != null)
                result[sprite.GetInstanceID()] = fileID;
        }
    }

    private bool TryGetOverrideTexture(string spriteName, out Texture2D texture)
    {
        if (GameAssetManagerAPI.Instance.TryLoadTexture(DEFAULT_OVERRIDE_SPRITES_PATH + spriteName + ".png", out texture))
        {
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Clamp;
            return true;
        }
        return false;
    }

    private Sprite CreateReplacementSprite(Sprite original, Texture2D newTexture)
    {
        // Calculate the pivot based on the original sprites relative pivot point.
        // Pivot is stored in pixels in the Sprite, but Sprite.Create needs 0.0-1.0 normalized coordinates.
        float pivotX = original.pivot.x / original.rect.width;
        float pivotY = original.pivot.y / original.rect.height;

        // If the new texture is a different size, we maintain the relative anchor.
        // E.g., if the pivot was at the feet (bottom-center), it stays at the bottom-center of the new image.

        Sprite newSprite = Sprite.Create(
            newTexture,
            new Rect(0, 0, newTexture.width, newTexture.height),
            new Vector2(pivotX, pivotY),
            original.pixelsPerUnit,
            0,
            SpriteMeshType.FullRect
        );

        newSprite.name = original.name;

        // Persist the texture so it isnt garbage collected
        UnityEngine.Object.DontDestroyOnLoad(newTexture);

        return newSprite;
    }

    private void UpdateGMSpriteArrays(Dictionary<string, Sprite> updatedDictionary, spriteLoader loaderInstance)
    {
        Sprite[][] gmSprites = loaderInstance.gmSprites;
        Sprite[][] gmAltSprites = loaderInstance.gmAltSprites;

        // Iterate the Main Array
        for (int i = 0; i < gmSprites.Length; i++)
        {
            if (gmSprites[i] == null) continue;

            for (int j = 0; j < gmSprites[i].Length; j++)
            {
                Sprite current = gmSprites[i][j];
                if (current != null && updatedDictionary.TryGetValue(current.name, out Sprite replacement))
                {
                    // Pointer Swap
                    if (current != replacement) // Don't swap if already swapped
                    {
                        gmSprites[i][j] = replacement;
                    }
                }
            }
        }

        // Iterate the Alt Array (used for "dashed" formats usually)
        for (int i = 0; i < gmAltSprites.Length; i++)
        {
            if (gmAltSprites[i] == null) continue;

            for (int j = 0; j < gmAltSprites[i].Length; j++)
            {
                Sprite current = gmAltSprites[i][j];
                if (current != null && updatedDictionary.TryGetValue(current.name, out Sprite replacement))
                {
                    if (current != replacement)
                    {
                        gmAltSprites[i][j] = replacement;
                    }
                }
            }
        }
    }
}
