using SHCDESE.API.Components.Sprite;
using SHCDESE.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace SHCDESE.API;

/// <summary>
/// Manages full texture atlas overrides for GM sprite groups.
/// Mod authors register an AtlasOverrideDefinition; this class applies it during the ApplyRuntimeOverrides phase after game sprites are loaded.
/// </summary>
public sealed class GameAtlasManagerAPI
{
    private static readonly Lazy<GameAtlasManagerAPI> _lazy = new(() => new GameAtlasManagerAPI());
    public static GameAtlasManagerAPI Instance => _lazy.Value;

    // Keyed by GmFileName (e.g. "body_bedouin_healer"), last-write-wins for conflicts.
    private readonly Dictionary<string, AtlasOverrideDefinition> _registry = new(StringComparer.OrdinalIgnoreCase);

    private GameAtlasManagerAPI() { }

    internal void Unload()
    {
        _registry.Clear();
    }

    // -----------------------------------------------------------------------
    // Public API
    // -----------------------------------------------------------------------

    /// <summary>
    /// Registers a full atlas override for a GM sprite group.
    /// Must be called before sprite loading completes (i.e. during plugin Awake/Start).
    /// </summary>
    public void RegisterAtlasOverride(AtlasOverrideDefinition def)
    {
        if (def == null) throw new ArgumentNullException(nameof(def));
        if (string.IsNullOrEmpty(def.GmFileName)) throw new ArgumentException("GmFileName is required.");
        if (string.IsNullOrEmpty(def.AtlasTexturePath) && def.AtlasTextureResource == null)
            throw new ArgumentException("AtlasTexturePath or an indexed atlas resource is required.");
        if (string.IsNullOrEmpty(def.JsonPath) && def.JsonResource == null)
            throw new ArgumentException("JsonPath or an indexed JSON resource is required.");

        _registry[def.GmFileName] = def;
        LogHelper.Information($"Registered atlas override for [{def.GmFileName}] ({def.GmFileID})");
    }

    /// <summary>
    /// Convenience overload: auto-discovers atlas.png, atlas_m.png and atlas.json from a folder named after the GM file (e.g. "Override/Atlas/body_bedouin_healer/").
    /// </summary>
    public void RegisterAtlasOverrideFromFolder(string gmFileName, Enums.GM gmFileID, string folderPath)
    {
        if (!Directory.Exists(folderPath))
        {
            LogHelper.Warning($"Atlas folder not found: [{folderPath}]");
            return;
        }

        string atlasPath = Path.Combine(folderPath, "atlas.png");
        if (!File.Exists(atlasPath))
            atlasPath = Path.Combine(folderPath, "atlas.dds");
        string maskPath = Path.Combine(folderPath, "atlas_m.png");
        if (!File.Exists(maskPath))
            maskPath = Path.Combine(folderPath, "atlas_m.dds");
        string jsonPath = Path.Combine(folderPath, "atlas.json");

        if (!File.Exists(atlasPath) || !File.Exists(jsonPath))
        {
            LogHelper.Warning($"Missing atlas.png/atlas.dds or atlas.json in [{folderPath}]. Skipping.");
            return;
        }

        RegisterAtlasOverride(new AtlasOverrideDefinition
        {
            GmFileName = gmFileName,
            GmFileID = gmFileID,
            AtlasTexturePath = atlasPath,
            MaskTexturePath = File.Exists(maskPath) ? maskPath : null,
            JsonPath = jsonPath
        });
    }

    // -----------------------------------------------------------------------
    // Internal: called from GameSpriteManagerAPI.ApplyRuntimeOverrides
    // -----------------------------------------------------------------------

    internal void ApplyAll(spriteLoader loaderInstance)
    {
        if (_registry.Count == 0) return;

        LogHelper.Information($"Applying {_registry.Count} atlas override(s)...");

        foreach (AtlasOverrideDefinition def in _registry.Values)
        {
            try
            {
                ApplySingle(loaderInstance, def);
            }
            catch (Exception ex)
            {
                LogHelper.Error(ex, $"Failed to apply atlas for [{def.GmFileName}]");
            }
        }
    }

    // -----------------------------------------------------------------------
    // Private implementation
    // -----------------------------------------------------------------------

    private void ApplySingle(spriteLoader loaderInstance, AtlasOverrideDefinition def)
    {
        string atlasPath = def.AtlasTexturePath;
        if (def.AtlasTextureResource != null && !def.AtlasTextureResource.TryGetPhysicalPath(out atlasPath))
            return;

        // 1. Load atlas texture
        Texture2D atlasTex = LoadAtlasTexture(atlasPath);
        if (atlasTex == null)
            return;

        // 2. Load mask texture (optional)
        Texture2D maskTex = null;
        string? maskPath = def.MaskTexturePath;
        if (def.MaskTextureResource != null)
            def.MaskTextureResource.TryGetPhysicalPath(out maskPath);

        if (!string.IsNullOrEmpty(maskPath))
        {
            maskTex = LoadAtlasTexture(maskPath);
            if (maskTex == null)
                LogHelper.Warning($"Mask could not be loaded for [{def.GmFileName}]. Material validation will determine whether the override is safe to apply.");
        }

        // 3. Parse frame JSON
        string jsonPath = def.JsonPath;
        if (def.JsonResource != null && !def.JsonResource.TryGetPhysicalPath(out jsonPath))
            return;

        List<AtlasFrameData> frames = AtlasJsonParser.Parse(jsonPath, out SpriteMaterialMode? jsonMaterialMode, out bool invalidJsonMaterialMode);
        if (frames == null || frames.Count == 0)
        {
            LogHelper.Warning($"No frames parsed for [{def.GmFileName}]");
            return;
        }
        if (invalidJsonMaterialMode)
        {
            LogHelper.Warning($"The atlas override for [{def.GmFileName}] will not be applied because its material mode is invalid.");
            return;
        }

        int fileID = (int)def.GmFileID;
        if (fileID < 0 || fileID >= loaderInstance.gmSprites.Length || fileID >= loaderInstance.gmAltSprites.Length || fileID >= loaderInstance.gmMaterials.Length)
        {
            LogHelper.Warning($"GM file ID [{fileID}] for [{def.GmFileName}] is outside the vanilla sprite arrays. Skipping.");
            return;
        }

        // 4. Resolve frame positions against the already-built vanilla arrays.
        // This preserves composite GM files such as GM_NEW_SEA, where one named sprite group starts at a non-zero offset in the shared array.
        Sprite[]? originalMain = loaderInstance.gmSprites[fileID];
        Sprite[]? originalAlt = loaderInstance.gmAltSprites[fileID];
        Dictionary<string, int> existingIndices = BuildExistingIndexMap(originalMain, originalAlt);
        int groupOffset = DetermineGroupOffset(originalMain, originalAlt, def.GmFileName, def.DashFormat);
        List<(AtlasFrameData Frame, int Index)> targetFrames = new(frames.Count);
        int maxTargetIndex = -1;

        foreach (AtlasFrameData frame in frames)
        {
            int parsedIndex = ParseFrameIndex(frame.Name, def.GmFileName, def.DashFormat);
            if (parsedIndex < 0)
            {
                LogHelper.Warning($"Ignoring atlas frame [{frame.Name}] because it does not match GM group [{def.GmFileName}].");
                continue;
            }

            int targetIndex = existingIndices.TryGetValue(frame.Name, out int vanillaIndex) ? vanillaIndex : parsedIndex + groupOffset;
            if (targetIndex < 0)
            {
                LogHelper.Warning($"Ignoring atlas frame [{frame.Name}] because its resolved index is negative.");
                continue;
            }

            targetFrames.Add((frame, targetIndex));
            if (targetIndex > maxTargetIndex)
                maxTargetIndex = targetIndex;
        }

        if (targetFrames.Count == 0)
        {
            LogHelper.Warning($"No parseable frame indices for [{def.GmFileName}]");
            return;
        }

        // 5. Resolve and validate the material before mutating any vanilla arrays.
        // A bad explicit configuration must leave the game untouched.
        SpriteMaterialMode requestedMode = def.MaterialMode != SpriteMaterialMode.Auto ? def.MaterialMode : jsonMaterialMode ?? SpriteMaterialMode.Auto;
        SpriteMaterialMode resolvedMode = requestedMode;
        if (resolvedMode == SpriteMaterialMode.Auto)
        {
            if (maskTex == null)
            {
                // Preserve the pre-existing, documented no-mask behaviour.
                resolvedMode = SpriteMaterialMode.Plain;
            }
            else if (!SpriteMaterialUtility.TryDetectVanillaMode(loaderInstance, fileID, out resolvedMode, out _))
            {
                resolvedMode = SpriteMaterialMode.TeamColour;
                LogHelper.Warning($"Could not determine the vanilla material for [{def.GmFileName}]. Falling back to TeamColour because the atlas supplies a mask.");
            }
        }

        if (resolvedMode != SpriteMaterialMode.Plain && maskTex == null)
        {
            LogHelper.Warning($"Cannot apply [{def.GmFileName}] with material [{resolvedMode}] without atlas_m.png. The vanilla atlas will be preserved.");
            return;
        }

        if (resolvedMode == SpriteMaterialMode.Plain && maskTex != null)
            LogHelper.Warning($"Atlas [{def.GmFileName}] resolved to Plain; atlas_m.png will be ignored.");

        int originalMainLength = originalMain?.Length ?? 0;
        int originalAltLength = originalAlt?.Length ?? 0;
        int arraySize = Math.Max(maxTargetIndex + 1, Math.Max(originalMainLength, originalAltLength));

        // 6. Allocate new arrays and copy every vanilla entry.
        // The replacement atlas may grow a group, but it must never shorten one.
        Sprite[] newMain = new Sprite[arraySize];
        Sprite[] newAlt = new Sprite[arraySize];

        if (originalMain != null)
            Array.Copy(originalMain, newMain, originalMain.Length);
        if (originalAlt != null)
            Array.Copy(originalAlt, newAlt, originalAlt.Length);

        // 7. Slice all valid sprites before committing anything.
        // If Unity rejects a frame, the remaining valid frames can still be applied and every omitted/invalid position retains its vanilla sprite.
        List<(string Name, Sprite Sprite)> dictionaryUpdates = new(targetFrames.Count);
        foreach ((AtlasFrameData frame, int idx) in targetFrames)
        {
            if (!IsValidTextureRect(frame.TextureRect, atlasTex))
            {
                LogHelper.Warning($"Ignoring atlas frame [{frame.Name}] because rect [{frame.TextureRect}] is outside atlas [{atlasTex.width}x{atlasTex.height}].");
                continue;
            }

            Sprite s = Sprite.Create(
                atlasTex,
                frame.TextureRect,
                frame.Pivot,
                frame.PixelsPerUnit,
                extrude: 0,
                SpriteMeshType.FullRect
            );
            s.name = frame.Name;

            if (frame.IsAltFrame)
                newAlt[idx] = s;
            else
                newMain[idx] = s;

            dictionaryUpdates.Add((frame.Name, s));
        }

        if (dictionaryUpdates.Count == 0)
        {
            LogHelper.Warning($"No valid atlas frames were created for [{def.GmFileName}]. The vanilla atlas will be preserved.");
            return;
        }

        if (!SpriteMaterialUtility.TryBuildMaterials(loaderInstance, fileID, resolvedMode, maskTex,
                def.GmFileName, out Material[]? materials, out string shaderName))
            return;

        UnityEngine.Object.DontDestroyOnLoad(atlasTex);
        if (maskTex != null)
            UnityEngine.Object.DontDestroyOnLoad(maskTex);

        // 8. Commit all related state together.
        loaderInstance.gmSprites[fileID] = newMain;
        loaderInstance.gmAltSprites[fileID] = newAlt;

        // Do not replace gmMaterials[fileID]: partial-atlas fallback sprites still use the vanilla mask atlas.
        // Replacement sprites are associated with their custom material by instance ID and applied by the existing SpriteMapping hook.
        foreach ((string name, Sprite sprite) in dictionaryUpdates)
        {
            loaderInstance.allSprites[name] = sprite;
            GameSpriteManagerAPI.Instance.RegisterCustomMaterials(sprite.GetInstanceID(), materials!, resolvedMode);
        }

        // 9. Colour palette
        if (def.ColourOverride != null && def.ColourOverride.Length == 10)
            loaderInstance.gmColors[fileID] = def.ColourOverride;
        else if (def.ColourOverride != null)
            LogHelper.Warning($"Ignoring colour override for [{def.GmFileName}]: expected exactly 10 colours.");

        LogHelper.Information($"Applied [{def.GmFileName}]: {dictionaryUpdates.Count} frames sliced, material={resolvedMode}, shader=[{shaderName}], mask={(maskTex != null && resolvedMode != SpriteMaterialMode.Plain ? "yes" : "no")}, arraySize={arraySize}, vanillaMain={originalMainLength}, vanillaAlt={originalAltLength}, offset={groupOffset}");
    }

    private static Dictionary<string, int> BuildExistingIndexMap(Sprite[]? main, Sprite[]? alt)
    {
        Dictionary<string, int> indices = new(StringComparer.OrdinalIgnoreCase);
        AddExistingIndices(main, indices);
        AddExistingIndices(alt, indices);
        return indices;
    }

    private static void AddExistingIndices(Sprite[]? sprites, Dictionary<string, int> indices)
    {
        if (sprites == null)
            return;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite != null && !string.IsNullOrEmpty(sprite.name))
                indices[sprite.name] = i;
        }
    }

    private static int DetermineGroupOffset(Sprite[]? main, Sprite[]? alt, string gmFileName, bool dashFormat)
    {
        if (TryFindGroupOffset(main, gmFileName, dashFormat, out int offset) || TryFindGroupOffset(alt, gmFileName, dashFormat, out offset))
            return offset;

        return 0;
    }

    private static bool TryFindGroupOffset(Sprite[]? sprites, string gmFileName, bool dashFormat, out int offset)
    {
        offset = 0;
        if (sprites == null)
            return false;

        for (int i = 0; i < sprites.Length; i++)
        {
            Sprite sprite = sprites[i];
            if (sprite == null)
                continue;

            int parsedIndex = ParseFrameIndex(sprite.name, gmFileName, dashFormat);
            if (parsedIndex >= 0)
            {
                offset = i - parsedIndex;
                return true;
            }
        }

        return false;
    }

    private static bool IsValidTextureRect(Rect rect, Texture2D texture) =>
        rect.width > 0f && rect.height > 0f && rect.x >= 0f && rect.y >= 0f &&
        rect.xMax <= texture.width && rect.yMax <= texture.height;

    /// <summary>
    /// Parses the frame index from a sprite name.
    /// Handles dash format: 
    /// "body_bedouin_healer-86" -> 86
    /// "body_bedouin_healer-86x" -> 86 (alt frame)
    /// </summary>
    private static int ParseFrameIndex(string spriteName, string gmFileName, bool dashFormat)
    {
        string prefix = string.Empty;
        string suffix = string.Empty;
        int idx = 0;

        if (dashFormat)
        {
            prefix = gmFileName + "-";
            if (!spriteName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return -1;
            suffix = spriteName.Substring(prefix.Length).TrimEnd('x');
            return int.TryParse(suffix, out idx) ? idx : -1;
        }

        prefix = gmFileName + " ";
        if (!spriteName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return -1;

        // 3-digit zero-padded: "tile_land8 000"
        suffix = spriteName.Substring(prefix.Length).TrimEnd('x');
        return int.TryParse(suffix, out idx) ? idx : -1;

    }

    private static Texture2D LoadAtlasTexture(string absolutePath)
    {
        try
        {
            byte[] bytes = File.ReadAllBytes(absolutePath);
            Texture2D tex;
            if (SHCDESE.Extensions.TextureExtensions.IsDds(bytes))
            {
                tex = SHCDESE.Extensions.TextureExtensions.LoadDds(bytes);
                if (tex == null)
                {
                    LogHelper.Warning($"DDS load failed for [{absolutePath}]");
                    return null;
                }
            }
            else
            {
                tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                if (!tex.LoadImage(bytes))
                {
                    LogHelper.Warning($"LoadImage failed for [{absolutePath}]");
                    return null;
                }
            }
            tex.filterMode = FilterMode.Point;
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.name = Path.GetFileNameWithoutExtension(absolutePath);
            return tex;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to load texture: [{absolutePath}]");
            return null;
        }
    }

    private static readonly Dictionary<string, (Enums.GM FileID, bool DashFormat)> _gmFileNameToEnum = new(StringComparer.OrdinalIgnoreCase)
    {
        // Body sprites: all dash format
        { "body_fighting_monk",        (Enums.GM.GM_BODY_FIGHTING_MONK,         true) },
        { "body_temple_guard",         (Enums.GM.GM_BODY_TEMPLE_GUARD,          true) },
        { "body_knight",               (Enums.GM.GM_BODY_KNIGHT,                true) },
        { "body_ladder_bearer",        (Enums.GM.GM_BODY_LADDERMAN,             true) },
        { "body_pikeman",              (Enums.GM.GM_BODY_PIKEMAN,               true) },
        { "body_pitch_worker",         (Enums.GM.GM_BODY_PITCHWORKER,           true) },
        { "body_tunnelor",             (Enums.GM.GM_BODY_TUNNELER,              true) },
        { "body_armourer",             (Enums.GM.GM_BODY_ARMOURER,              true) },
        { "body_healer",               (Enums.GM.GM_BODY_HEALER,                true) },
        { "body_ballista",             (Enums.GM.GM_BODY_BALLISTA,              true) },
        { "body_battering_ram",        (Enums.GM.GM_BODY_BATTERING_RAM,         true) },
        { "body_catapult",             (Enums.GM.GM_BODY_CATAPULT,              true) },
        { "body_mangonel",             (Enums.GM.GM_BODY_MANGONEL,              true) },
        { "body_siege_tower",          (Enums.GM.GM_BODY_SIEGE_TOWER,           true) },
        { "body_trebutchet",           (Enums.GM.GM_BODY_TREBUCHET,             true) },
        { "body_lion",                 (Enums.GM.GM_BODY_LION,                  true) },
        { "body_rabbit",               (Enums.GM.GM_BODY_RABBIT,                true) },
        { "body_fireman",              (Enums.GM.GM_BODY_FIREMAN,               true) },
        { "body_animal_burning_big",   (Enums.GM.GM_BODY_ANIMAL_BURNING_BIG,   true) },
        { "body_animal_burning_small", (Enums.GM.GM_BODY_ANIMAL_BURNING_SMALL, true) },
        { "body_archer",               (Enums.GM.GM_BODY_ARCHER,                true) },
        { "body_baker",                (Enums.GM.GM_BODY_BAKER,                 true) },
        { "body_blacksmith",           (Enums.GM.GM_BODY_BLACKSMITH,            true) },
        { "body_boy",                  (Enums.GM.GM_BODY_BOY,                   true) },
        { "body_brewer",               (Enums.GM.GM_BODY_BREWER,                true) },
        { "body_chicken",              (Enums.GM.GM_BODY_CHICKEN,               true) },
        { "body_chicken_brown",        (Enums.GM.GM_BODY_CHICKEN_BROWN,         true) },
        { "body_crossbowman",          (Enums.GM.GM_BODY_CROSSBOWMAN,           true) },
        { "body_cow",                  (Enums.GM.GM_BODY_COW,                   true) },
        { "body_deer",                 (Enums.GM.GM_BODY_DEER,                  true) },
        { "body_dog",                  (Enums.GM.GM_BODY_DOG,                   true) },
        { "body_drunkard",             (Enums.GM.GM_BODY_DRUNKARD,              true) },
        { "body_farmer",               (Enums.GM.GM_BODY_FARMER,                true) },
        { "body_fire_eater",           (Enums.GM.GM_BODY_FIREEATER,             true) },
        { "body_fletcher",             (Enums.GM.GM_BODY_FLETCHER,              true) },
        { "body_ghost",                (Enums.GM.GM_BODY_GHOST,                 true) },
        { "body_girl",                 (Enums.GM.GM_BODY_GIRL,                  true) },
        { "body_horse_trader",         (Enums.GM.GM_BODY_TRADER_HORSE,          true) },
        { "body_hunter",               (Enums.GM.GM_BODY_HUNTER,                true) },
        { "body_innkeeper",            (Enums.GM.GM_BODY_INNKEEPER,             true) },
        { "body_iron_miner",           (Enums.GM.GM_BODY_IRONMINER,             true) },
        { "body_jester",               (Enums.GM.GM_BODY_JESTER,                true) },
        { "body_juggler",              (Enums.GM.GM_BODY_JUGGLER,               true) },
        { "body_lady",                 (Enums.GM.GM_BODY_LADY,                  true) },
        { "body_lord",                 (Enums.GM.GM_BODY_LORD,                  true) },
        { "body_maceman",              (Enums.GM.GM_BODY_MACEMAN,               true) },
        { "body_man_burning",          (Enums.GM.GM_BODY_BURNING_MAN,           true) },
        { "body_miller",               (Enums.GM.GM_BODY_MILLER,                true) },
        { "body_mother",               (Enums.GM.GM_BODY_MOTHER,                true) },
        { "body_ox",                   (Enums.GM.GM_BODY_OXCART,                true) },
        { "body_peasant",              (Enums.GM.GM_BODY_PEASANT,               true) },
        { "body_poleturner",           (Enums.GM.GM_BODY_POLETURNER,            true) },
        { "body_priest",               (Enums.GM.GM_BODY_PRIEST,                true) },
        { "body_shield",               (Enums.GM.GM_BODY_SHIELD,                true) },
        { "body_siege_engineer",       (Enums.GM.GM_BODY_SIEGE_ENGINEER,        true) },
        { "body_spearman",             (Enums.GM.GM_BODY_SPEARMAN,              true) },
        { "body_stonemason",           (Enums.GM.GM_BODY_STONEMASON,            true) },
        { "body_swordsman",            (Enums.GM.GM_BODY_SWORDSMAN,             true) },
        { "body_tanner",               (Enums.GM.GM_BODY_TANNER,                true) },
        { "body_trader",               (Enums.GM.GM_BODY_TRADER,                true) },
        { "body_woodcutter",           (Enums.GM.GM_BODY_WOODCUTTER,            true) },
        { "body_arab_shortbow",        (Enums.GM.GM_BODY_ARAB_BOW,              true) },
        { "body_arab_assasin",         (Enums.GM.GM_BODY_ARAB_ASSASIN,          true) },
        { "body_horse_archer",         (Enums.GM.GM_BODY_ARAB_HORSE,            true) },
        { "body_horse_archer_top",     (Enums.GM.GM_BODY_ARAB_HORSEMAN,         true) },
        { "body_arab_grenadier",       (Enums.GM.GM_BODY_ARAB_GRENADIER,        true) },
        { "body_sapper",               (Enums.GM.GM_BODY_BEDOUIN_SAPPER,        true) },
        { "body_bedouin_healer",       (Enums.GM.GM_BODY_BEDOUIN_HEALER,        true) },
        { "body_eunuch",               (Enums.GM.GM_BODY_BEDOUIN_EUNUCH,        true) },
        { "body_ambusher",             (Enums.GM.GM_BODY_BEDOUIN_AMBUSHER,      true) },
        { "body_arab_slave",           (Enums.GM.GM_BODY_ARAB_SLAVE,            true) },
        { "body_arab_slinger",         (Enums.GM.GM_BODY_ARAB_SLINGER,          true) },
        { "body_arab_swordsman",       (Enums.GM.GM_BODY_ARAB_SWORDSMAN,        true) },
        { "body_arab_ballista",        (Enums.GM.GM_BODY_ARAB_BALLISTA,         true) },
        { "body_wolf",                 (Enums.GM.GM_BODY_WOLF,                  true) },
        { "body_saladin",              (Enums.GM.GM_BODY_ARABIC_LORD,           true) },
        { "body_camel",                (Enums.GM.GM_BODY_CAMEL,                 true) },
        { "body_camel_lancer",         (Enums.GM.GM_BODY_BEDOUIN_CAMEL_LANCER,  true) },
        { "body_skirmisher",           (Enums.GM.GM_BODY_BEDOUIN_SKIRMISHER,    true) },
        { "body_heavy_camel",          (Enums.GM.GM_BODY_BEDOUIN_HEAVY_CAMEL,   true) },
        { "body_demolisher",           (Enums.GM.GM_BODY_BEDOUIN_DEMOLISHER,    true) },
        { "body_goat",                 (Enums.GM.GM_BODY_GOAT,                  true) },
        { "body_hyena",                (Enums.GM.GM_BODY_HYENA,                 true) },
        { "body_crocodile",            (Enums.GM.GM_BODY_CROCODILE,             true) },
        { "body_bedouin_lord",         (Enums.GM.GM_BODY_BEDOUIN_LORD,          true) },
        { "body_imam",                 (Enums.GM.GM_BODY_IMAM,                  true) },
        { "body_scribe_lord",          (Enums.GM.GM_BODY_SCRIBE_LORD,           true) },
        { "body_lord_female",          (Enums.GM.GM_BODY_LORD_FEMALE,           true) },
        { "body_bessie",               (Enums.GM.GM_BODY_LORD_BESSY,            true) },
        { "body_arab_lord_female",     (Enums.GM.GM_BODY_ARABIC_LORD_FEMALE,    true) },
        { "body_bedouin_lord_female",  (Enums.GM.GM_BODY_BEDOUIN_LORD_FEMALE,   true) },
        { "body_fire",                 (Enums.GM.GM_BODY_FIRE,                  true) },
        { "body_fire2",                (Enums.GM.GM_BODY_FIRE2,                 true) },
        { "body_crow",                 (Enums.GM.GM_BODY_CROW,                  true) },
        { "body_missile",              (Enums.GM.GM_BODY_MISSILE,               true) },
        { "body_missile_2",            (Enums.GM.GM_BODY_MISSILE_2,             true) },
        { "body_missile_cow",          (Enums.GM.GM_BODY_MISSILE_COW,           true) },
        { "body_missile_firepot",      (Enums.GM.GM_BODY_MISSILE_FIREPOT,       true) },
        { "body_seagull",              (Enums.GM.GM_BODY_SEAGULL,               true) },
        { "body_splash",               (Enums.GM.GM_BODY_SPLASH,                true) },
        { "body_steam",                (Enums.GM.GM_BODY_STEAM,                 true) },
        { "body_gate",                 (Enums.GM.GM_BODY_GATE,                  true) },
        { "body_tent",                 (Enums.GM.GM_BODY_TENT,                  true) },
        { "body_disease",              (Enums.GM.GM_BODY_DISEASE,               true) },
        { "body_brazier",              (Enums.GM.GM_BODY_BRAZIER,               true) },
        { "body_javelin",              (Enums.GM.GM_BODY_JAVELIN,               true) },
        { "body_condor",               (Enums.GM.GM_BODY_CONDOR,                true) },
        { "body_info",                 (Enums.GM.GM_BODY_INFO,                  true) },
        { "anim_crusader_flag",        (Enums.GM.GM_ANIM_CRUSADER_FLAG,         true) },
        { "anim_arab_flag",            (Enums.GM.GM_ANIM_ARAB_FLAG,             true) },
        { "assasin_rope",              (Enums.GM.GM_ASSASIN_ROPE,               false) },
        { "tree_cactii",               (Enums.GM.GM_TREE_CACTII,                true) },
        // Trees: dash format
        { "Tree_Oak",                  (Enums.GM.GM_TREE_OAK,                   true) },
        { "tree_pine",                 (Enums.GM.GM_TREE_PINE,                  true) },
        { "tree_shrub1",               (Enums.GM.GM_TREE_SHRUB1,                true) },
        { "tree_shrub2",               (Enums.GM.GM_TREE_SHRUB2,                true) },
        { "tree_apple",                (Enums.GM.GM_TREE_APPLE,                 true) },
        { "tree_birch",                (Enums.GM.GM_TREE_BIRCH,                 true) },
        { "Tree_Chestnut",             (Enums.GM.GM_TREE_CHESTNUT,              true) },
        // Animations: space format
        { "anim_armourer",             (Enums.GM.GM_ARMOURER_ANIMS,             true) },
        { "anim_baker",                (Enums.GM.GM_WORKSHOP_BAKER_ANIMS,       true) },
        { "anim_blacksmith",           (Enums.GM.GM_WORKSHOP_SMITH_ANIMS,       true) },
        { "anim_boiled_oil",           (Enums.GM.GM_OIL_ANIMS,                  true) },
        { "anim_brewer",               (Enums.GM.GM_WORKSHOP_BREW_ANIMS,        true) },
        { "anim_buildings2",           (Enums.GM.GM_BUILDING_ANIMS2,            false) },
        { "anim_castle",               (Enums.GM.GM_CASTLE_ANIMS,               false) },
        { "anim_chopping_block",       (Enums.GM.GM_ANIM_CHOPPING_BLOCK,        true) },
        { "anim_dancing_bear",         (Enums.GM.GM_ANIM_DANCING_BEAR,          true) },
        { "anim_dog_cage",             (Enums.GM.GM_ANIM_DOG_CAGE,              true) },
        { "anim_drawbridge",           (Enums.GM.GM_DRAWBRIDGE_ANIMS,           true) },
        { "anim_ducking_stool",        (Enums.GM.GM_ANIM_DUNKING_STOOL,         true) },
        { "anim_dungeon",              (Enums.GM.GM_ANIM_DUNGEON,               true) },
        { "anim_farmer",               (Enums.GM.GM_FARMER_ANIMS,               true) },
        { "anim_flags",                (Enums.GM.GM_FLAG_ANIMS,                 true) },
        { "anim_flag_small",           (Enums.GM.GM_ANIM_FLAG_SMALL,            true) },
        { "anim_fletcher",             (Enums.GM.GM_FLETCHER_ANIMS,             true) },
        { "anim_gallows",              (Enums.GM.GM_GALLOWS_ANIMS,              true) },
        { "anim_gibbet",               (Enums.GM.GM_ANIM_GIBBET,                true) },
        { "anim_goods",                (Enums.GM.GM_GOODS_ANIMS,                false) },
        { "anim_heads",                (Enums.GM.GM_ANIM_HEADS,                 true) },
        { "anim_healer",               (Enums.GM.GM_ANIM_HEALER,                true) },
        { "anim_hunter",               (Enums.GM.GM_HUNTER_ANIMS,               true) },
        { "anim_inn",                  (Enums.GM.GM_ANIM_INN,                   true) },
        { "anim_iron_miner",           (Enums.GM.GM_MINE_ANIMS,                 true) },
        { "anim_killing_pits",         (Enums.GM.GM_ANIM_KILLING_PITS,          true) },
        { "anim_market",               (Enums.GM.GM_ANIM_MARKET,                true) },
        { "anim_maypole",              (Enums.GM.GM_MAYPOLE_ANIMS,              true) },
        { "anim_pitch_dugout",         (Enums.GM.GM_PITCH_ANIMS,                true) },
        { "anim_poleturner",           (Enums.GM.GM_WORKSHOP_POLE_ANIMS,        true) },
        { "anim_quarry",               (Enums.GM.GM_QUARRY_ANIMS,               true) },
        { "anim_rack",                 (Enums.GM.GM_ANIM_RACK,                  true) },
        { "anim_shields",              (Enums.GM.GM_SHEILD_ANIMS,               true) },
        { "anim_stables",              (Enums.GM.GM_STABLE_ANIMS,               true) },
        { "anim_stake",                (Enums.GM.GM_ANIM_STAKE,                 true) },
        { "anim_stocks",               (Enums.GM.GM_ANIM_STOCKS,                true) },
        { "anim_tanner",               (Enums.GM.GM_WORKSHOP_TANNER_ANIMS,      true) },
        { "anim_tunnelors_guild",      (Enums.GM.GM_ANIM_TUNNELERS_GUILD,       false) },
        { "anim_tunnels",              (Enums.GM.GM_ANIM_TUNNELS,               true) },
        { "anim_whitecaps_wave",       (Enums.GM.GM_ANIM_WHITECAPS,             true) },
        { "anim_windmill",             (Enums.GM.GM_WINDMILL_ANIMS,             true) },
        { "anim_woodcutter",           (Enums.GM.GM_WOODCUTTER_ANIMS,           true) },
        { "body_missile_fire",         (Enums.GM.GM_ANIM_MISSILE_FIRE,          true) },
        // Tiles: space format
        { "tile_land8",                (Enums.GM.GM_LAND,                       false) },
        { "tile_sea8",                 (Enums.GM.GM_SEA,                        false) },
        { "tile_buildings1",           (Enums.GM.GM_BUILDINGS1,                 false) },
        { "tile_buildings2",           (Enums.GM.GM_BUILDINGS2,                 false) },
        { "tile_workshops",            (Enums.GM.GM_WORKSHOPS,                  false) },
        { "tile_land3",                (Enums.GM.GM_MISC_LAND,                  false) },
        { "tile_farmland",             (Enums.GM.GM_FARMLAND,                   false) },
        { "tile_goods",                (Enums.GM.GM_GOODS,                      false) },
        { "tile_churches",             (Enums.GM.GM_CHURCHS,                    false) },
        { "tile_castle",               (Enums.GM.GM_CASTLES,                    false) },
        { "tile_land_macros",          (Enums.GM.GM_MACRO_LAND,                 false) },
        { "tile_rocks8",               (Enums.GM.GM_ROCKS,                      false) },
        { "tile_land_and_stones",      (Enums.GM.GM_LAND_AND_STONES,            false) },
        { "killing_pits",              (Enums.GM.GM_KILLING_PITS,               false) },
        { "pitch_ditches",             (Enums.GM.GM_PITCH_DITCHES,              false) },
        { "tile_ruins",                (Enums.GM.GM_TILE_RUINS,                 false) },
        { "tile_sea_new_01",           (Enums.GM.GM_NEW_SEA,                    false) },
        { "tile_sea_shore",            (Enums.GM.GM_NEW_SEA,                    false) },
        { "tile_flatties",             (Enums.GM.GM_TILE_FLATTIES,              false) },
        { "tile_chevrons",             (Enums.GM.GM_PILLARS,                    false) },
        { "tile_cliffs",               (Enums.GM.GM_CLIFFS,                     false) },
        { "tile_walls",                (Enums.GM.GM_WALLS,                      false) },
        { "rock_chips",                (Enums.GM.GM_ROCK_CHIPS,                 false) },
        { "oil_dropped",               (Enums.GM.GM_BODY_OIL,                   false) },
        { "cracks",                    (Enums.GM.GM_CRACKS,                     false) },
        { "puff of smoke",             (Enums.GM.GM_PUFF_OF_SMOKE,              true) },
        { "blast3",                    (Enums.GM.GM_BLAST,                      true) },
        { "smoke-30x30",               (Enums.GM.GM_SMOKE_ANIMS,                true) },
        { "float_pop_circ-1",          (Enums.GM.GM_FLOAT_POP_CIRC,            true) },
        { "float_pop_circ-2",          (Enums.GM.GM_FLOAT_POP_CIRC_2,          true) },
        { "floats",                    (Enums.GM.GM_FLOATS,                     false) },
        { "floats_new",                (Enums.GM.GM_FLOATS_NEW,                 true) },
        { "cursors",                   (Enums.GM.GM_CURSORS,                    true) },
    };

    public static bool TryGetGMEnum(string gmFileName, out Enums.GM gmFileID, out bool dashFormat)
    {
        if (_gmFileNameToEnum.TryGetValue(gmFileName, out (Enums.GM FileID, bool DashFormat) entry))
        {
            gmFileID = entry.FileID;
            dashFormat = entry.DashFormat;
            return true;
        }
        gmFileID = default;
        dashFormat = false;
        return false;
    }
}
