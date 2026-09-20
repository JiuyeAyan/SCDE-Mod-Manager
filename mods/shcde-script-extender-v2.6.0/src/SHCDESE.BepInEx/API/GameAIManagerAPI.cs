using CrusaderDE;
using RedBird.Core.Memory;
using SHCDESE.API.Components.AI;
using SHCDESE.API.Components.ModManager;
using SHCDESE.API.Components.Network;
using SHCDESE.API.LowLevel;
using SHCDESE.GameGlobals;
using SHCDESE.Interop;
using SHCDESE.Interop.Enums;
using SHCDESE.IO;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text.Json;
using UnityEngine;

namespace SHCDESE.API;

/// <summary>
/// Provides a high-level API for managing custom AI lords at runtime.
/// </summary>
/// <remarks>
/// <para>
/// Custom lords are loaded from folders discovered by <see cref="CustomisationFileManager"/>.
/// Each lord folder must contain a valid <c>info.json</c> and <c>lordmeta.json</c>. An optional
/// <c>init.lua</c> enables Lua-driven AI behavior run inside an isolated per-lord environment.
/// </para>
/// <para>
/// This class is a thread-safe singleton. Access it via <see cref="Instance"/>.
/// </para>
/// </remarks>
[LuaApiNamespace("Player")]
public sealed class GameAIManagerAPI
{
    private static readonly Lazy<GameAIManagerAPI> _lazy = new(() => new GameAIManagerAPI());
    public static GameAIManagerAPI Instance => _lazy.Value;

    // ---------------------------------------------------------------------------------------
    // Fields
    // ---------------------------------------------------------------------------------------

    private SimpleNativeArray<InternalAIC> _aicArray;

    /// <summary>
    /// Cache of the most recently spoken subtitle text per lord, keyed by lowercase internal name.
    /// Populated during <see cref="TryGetVideoAndAudio"/> and cleared when the lord speaks again.
    /// </summary>
    private readonly Dictionary<string, string> _lastSpokenTextCache;

    // Total number of distinct message types supported per lord in the game's translation table.
    private const int MessageTypeStride = 34;

    // ---------------------------------------------------------------------------------------
    // Public State
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Gets all registered custom lord entries, keyed by their lowercase internal name.
    /// </summary>
    /// <remarks>
    /// Populated at load time via <see cref="ProcessCustomLord"/>. Do not modify this dictionary directly.
    /// </remarks>
    public Dictionary<string, CustomLordEntry> LordDataDict { get; private set; }

    // ---------------------------------------------------------------------------------------
    // Constructor
    // ---------------------------------------------------------------------------------------

    private unsafe GameAIManagerAPI()
    {
        LordDataDict = new Dictionary<string, CustomLordEntry>();
        _lastSpokenTextCache = new Dictionary<string, string>();

        if (GameGlobalsManager.Instance.AILordManagerRVA == 0)
        {
            LogHelper.Error("AILordManagerRVA is null! This should never happen!");
            return;
        }

        LogHelper.Information($"Loading AIC Array");
        _aicArray = new SimpleNativeArray<InternalAIC>((byte*)GameGlobalsManager.Instance.AILordManagerRVA + (UInt64)CrusaderLibrary.Instance.LibraryModuleHandle, Enum.GetValues(typeof(Enums.AILords)).Length);
    }

    internal void Unload()
    {
        LordDataDict.Clear();
    }

    // ---------------------------------------------------------------------------------------
    // Internal: Lord Registration
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Processes a lord folder discovered by <see cref="CustomisationFileManager"/> and registers
    /// it as a <see cref="CustomLordEntry"/> if both <c>info.json</c> and <c>lordmeta.json</c> are valid.
    /// Also registers the lord's <c>Override/</c> directory as an asset provider.
    /// Example path:  H:\SteamLibrary\steamapps\workshop\content\3024040\3659458903\Lord Nox
    /// </summary>
    /// <param name="path">Absolute path to the lord's root folder.</param>
    /// <param name="lord">The raw <see cref="CustomisationFileManager.CustomLord"/> data from the game.</param>
    internal void ProcessCustomLord(string path, CustomisationFileManager.CustomLord lord)
    {
        LogHelper.Information($"Processing: [{path}]");

        string modInfoPath = Path.Combine(path, "info.json");
        string lordMetaPath = Path.Combine(path, "lordmeta.json");
        string luaInitPath = Path.Combine(path, "init.lua");

        if (!TryDeserializeJson<ModInfo>(modInfoPath, "info.json", out ModInfo? modInfo))
            return;

        LogHelper.Information($"Registering lord-based assets: [{modInfo.GUID}] - [{modInfo.Name}] by [{modInfo.Author}]");
        GameAssetModManager.Instance.RegisterAssetMod(path);

        if (!TryDeserializeJson<LordInfo>(lordMetaPath, "lordmeta.json", out LordInfo? lordInfo))
            return;

        string lordNameLower = lord.lordName.ToLower();
        if (LordDataDict.ContainsKey(lordNameLower))
        {
            LogHelper.Warning($"Lord [{lordNameLower}] is already registered; skipping duplicate.");
            return;
        }

        CustomLordEntry entry = BuildLordEntry(lordNameLower, lordInfo, luaInitPath);
        LordDataDict.Add(lordNameLower, entry);

    }

    /// <summary>
    /// Constructs a <see cref="CustomLordEntry"/> from parsed lord metadata,
    /// mapping message type strings to <see cref="AILordMessageType"/> enum values.
    /// </summary>
    private CustomLordEntry BuildLordEntry(string lordNameLower, LordInfo lordInfo, string luaInitPath)
    {
        CustomLordEntry entry = new CustomLordEntry
        {
            LordInfo = lordInfo,
            InternalName = lordNameLower,
            DisplayName = "not-set"
        };

        LogHelper.Information($"Added custom lord: [{lordNameLower}]");

        // Parse voice lines once at load time to catch invalid keys early.
        if (lordInfo.Messages != null)
        {
            foreach (KeyValuePair<string, List<LordMessageClip>> kvp in lordInfo.Messages)
            {
                if (Enum.TryParse(kvp.Key, ignoreCase: true, out AILordMessageType msgType))
                {
                    entry.VoiceLines[msgType] = kvp.Value;
                    LogHelper.Information($"Assigned lord [{lordNameLower}] message type [{msgType}] via key [{kvp.Key}]");
                }
                else
                {
                    LogHelper.Warning($"Lord [{lordNameLower}] has invalid message type key: [{kvp.Key}]");
                }
            }
        }

        if (File.Exists(luaInitPath))
        {
            entry.LuaInitPath = luaInitPath;
        }

        return entry;
    }

    // ---------------------------------------------------------------------------------------
    // Public: Display Name, Titles
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the localized title for the given lord at the specified slot index.
    /// </summary>
    /// <remarks>
    /// Title resolution falls back from the current game language to <c>en-US</c>.
    /// If the requested <paramref name="slotIndex"/> exceeds the number of titles, it wraps using modulo.
    /// </remarks>
    /// <param name="lordName">The internal lord name (case-insensitive).</param>
    /// <param name="lordTitle">The resolved title string. If not found <see cref="string.Empty"/> or not-set</param>
    /// <param name="slotIndex">Zero-based index into the lord's title list. Wraps if out of range.</param>
    /// <returns><see langword="true"/> if available; otherwise <see langword="false"/>.</returns>
    public bool TryGetLordTitle(string lordName, [NotNullWhen(true)] out string? lordTitle, int slotIndex = 0)
    {
        lordTitle = string.Empty;
        if (!LordDataDict.TryGetValue(lordName.ToLower(), out CustomLordEntry? cle))
        {
            LogHelper.Error($"Lord not found: [{lordName}]");
            return false;
        }

        // Resolve locale-specific titles on every call (language may change at runtime).
        string lang = GameAssetManagerAPI.Instance.CurrentLanguage;
        if (cle.LordInfo.LocalizedTitles != null)
        {
            if (cle.LordInfo.LocalizedTitles.TryGetValue(lang, out List<string>? locTitles))
                cle.Titles = locTitles;
            else if (cle.LordInfo.LocalizedTitles.TryGetValue(GameAssetManagerAPI.DEFAULT_LOCALE, out List<string>? enTitles))
                cle.Titles = enTitles;
        }

        if (cle.Titles == null || cle.Titles.Count == 0)
            return false;

        // Wrap index if it overflows the list.
        if (slotIndex >= cle.Titles.Count)
            slotIndex %= cle.Titles.Count;

        lordTitle = cle.Titles[slotIndex];
        if (string.IsNullOrEmpty(lordTitle) || lordTitle.Equals("not-set", StringComparison.Ordinal))
            return false;

        return true;
    }

    /// <summary>
    /// Returns the localized display name for the given lord.
    /// </summary>
    /// <remarks>
    /// Falls back from the current language to <c>en-US</c>. Returns <see langword="null"/> if the lord is not found.
    /// </remarks>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="displayName">The resolved display name, or <see langword="null"/> on failure.</param>
    /// <returns><see langword="true"/> if available; otherwise <see langword="false"/>.</returns>
    internal bool TryGetDisplayName(string name, out string? displayName)
    {
        displayName = null;
        if (!LordDataDict.TryGetValue(name.ToLower(), out CustomLordEntry? cle))
        {
            LogHelper.Error($"Lord not found: [{name}]");
            return false;
        }

        displayName = GetLocalizedText(cle.LordInfo.LocalizedDisplayName);
        if (displayName.Equals("not-set", StringComparison.Ordinal))
            return false;
        return true;
    }

    /// <summary>
    /// Resolves the descriptive texts for a custom lord in the current game language.
    /// </summary>
    /// <remarks>
    /// Each field falls back independently from the current game language to <c>en-US</c> and then
    /// to an empty string. Values are resolved on every call so runtime language changes are respected.
    /// </remarks>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="details">The resolved detail texts, or <see langword="null"/> if the lord is not registered.</param>
    /// <returns><see langword="true"/> if the custom lord was found; otherwise <see langword="false"/>.</returns>
    public bool TryGetLordDetails(string name, [NotNullWhen(true)] out LordDetails? details)
    {
        details = null;
        if (string.IsNullOrWhiteSpace(name) ||
            !LordDataDict.TryGetValue(name.ToLower(), out CustomLordEntry? entry))
        {
            return false;
        }

        LordInfo info = entry.LordInfo;
        details = new LordDetails
        {
            Description = GetOptionalLocalizedText(info.LocalizedDescription),
            DifficultyRating = GetOptionalLocalizedText(info.LocalizedDifficultyRating),
            FavouriteTroops = GetOptionalLocalizedText(info.LocalizedFavouriteTroops),
            Castles = GetOptionalLocalizedText(info.LocalizedCastles),
            PlayStyle = GetOptionalLocalizedText(info.LocalizedPlayStyle),
            FavouriteSaying = GetOptionalLocalizedText(info.LocalizedFavouriteSaying)
        };
        return true;
    }


    // ---------------------------------------------------------------------------------------
    // Public: Speech Text
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the subtitle text of the most recently played voice clip for the given lord.
    /// </summary>
    /// <param name="lordName">The internal lord name (case-insensitive).</param>
    /// <param name="currentSpeech">The cached subtitle string, or <see cref="string.Empty"/> if no clip has been played</param>
    /// <returns><see langword="true"/> if available; otherwise <see langword="false"/>.</returns>
    public bool TryGetCurrentSpeechText(string lordName, out string? currentSpeech)
    {
        currentSpeech = string.Empty;
        if (_lastSpokenTextCache.TryGetValue(lordName.ToLower(), out string? text))
        {
            currentSpeech = text;
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------------------------------
    // Public: Slot, Player ID Resolution
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves the internal lord name occupying a given generic AI slot (0–7).
    /// </summary>
    /// <remarks>
    /// <para>Resolution priority:</para>
    /// <list type="number">
    ///   <item>In-game skirmish <c>aivs</c> array</item>
    ///   <item>In-game multiplayer <c>LordNames</c> array</item>
    ///   <item>Frontend multiplayer lobby <c>AIVs</c> array</item>
    /// </list>
    /// Returns <see cref="string.Empty"/> if no lord is found at the given slot.
    /// </remarks>
    /// <param name="slotIndex">Zero-based AI slot index (0–7).</param>
    /// <returns>The lord name string, or <see cref="string.Empty"/> if unoccupied.</returns>
    public string GetGenericSlotLordName(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex > 7)
            return string.Empty;

        // Priority 1: In-game skirmish
        FRONT_Multiplayer.MPAIVInfo[]? skirmishAivs = MainViewModel.Instance.HUDIngameMenu?.restartSkirmishMapInfo?.aivs;
        if (skirmishAivs != null && slotIndex < skirmishAivs.Length && !string.IsNullOrEmpty(skirmishAivs[slotIndex].lordName))
            return skirmishAivs[slotIndex].lordName;

        // Priority 2: In-game multiplayer
        string[]? mpNames = MainViewModel.Instance.HUDIngameMenu?.restartMPInfo?.LordNames;
        if (mpNames != null && slotIndex < mpNames.Length && !string.IsNullOrEmpty(mpNames[slotIndex]))
            return mpNames[slotIndex];

        // Priority 3: Frontend lobby
        FRONT_Multiplayer.MPAIVInfo[]? lobbyAivs = MainViewModel.Instance.FRONTMultiplayer?.AIVs;
        if (lobbyAivs != null && slotIndex < lobbyAivs.Length && !string.IsNullOrEmpty(lobbyAivs[slotIndex].lordName))
            return lobbyAivs[slotIndex].lordName;

        return string.Empty;
    }

    /// <summary>
    /// Converts a 1-based player ID to the internal lord name occupying that slot.
    /// </summary>
    /// <remarks>
    /// Exported to Lua as <c>Player_GetCustomAILordNameByPlayerId</c>.
    /// Internally delegates to <see cref="GetGenericSlotLordName"/> with <c>playerId - 1</c>.
    /// </remarks>
    /// <param name="playerId">1-based player ID.</param>
    /// <returns>The lord name string, or <see cref="string.Empty"/> if none found.</returns>
    [LuaApiExport("GetCustomAILordNameByPlayerId")]
    public string GetCustomAILordNameByPlayerId(int playerId)
    {
        return GetGenericSlotLordName(playerId - 1);
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given name maps to a registered custom lord.
    /// </summary>
    /// <remarks>Exported to Lua as <c>Player_IsSupportedCustomLord</c>.</remarks>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    [LuaApiExport("IsSupportedCustomLord")]
    public bool IsSupportedCustomLord(string name)
    {
        return LordDataDict.ContainsKey(name.ToLower());
    }

    /// <summary>
    /// Converts a raw game translation index into the corresponding <see cref="AILordMessageType"/>.
    /// </summary>
    /// <remarks>
    /// The game assigns translation indices based on a per-lord stride of <see cref="MessageTypeStride"/>.
    /// This method performs the modulo arithmetic to recover the category index and maps it to the enum.
    /// </remarks>
    /// <param name="index">Raw translation index from the game engine (1-based).</param>
    /// <param name="messageType">
    /// The resolved <see cref="AILordMessageType"/> if the index is valid; otherwise the default value.
    /// </param>
    /// <returns><see langword="true"/> if the index mapped to a defined enum member; otherwise <see langword="false"/>.</returns>
    public bool TryGetMessageTypeFromIndex(int index, out AILordMessageType messageType)
    {
        int categoryIndex = ((index - 1) % MessageTypeStride) + 1;

        if (Enum.IsDefined(typeof(AILordMessageType), categoryIndex))
        {
            messageType = (AILordMessageType)categoryIndex;
            return true;
        }

        LogHelper.Warning($"undefined category index [{categoryIndex}] derived from raw index [{index}]");
        messageType = default;
        return false;
    }

    /// <summary>
    /// Maps an extended <see cref="Enums.AILords"/> enum value to its zero-based player slot index.
    /// </summary>
    /// <param name="lord">The extended lord enum value (e.g. <see cref="Enums.AILords.SK_X1"/>).</param>
    /// <param name="playerId">The zero-based slot index, or <c>-1</c> if not found.</param>
    /// <returns><see langword="true"/> if the enum value maps to a valid slot; otherwise <see langword="false"/>.</returns>
    public bool GetSlotIndexByExtendedLordEnum(Enums.AILords lord, out int playerId)
    {
        playerId = lord switch
        {
            Enums.AILords.SK_X1 => 0,
            Enums.AILords.SK_X2 => 1,
            Enums.AILords.SK_X3 => 2,
            Enums.AILords.SK_X4 => 3,
            Enums.AILords.SK_X5 => 4,
            Enums.AILords.SK_X6 => 5,
            Enums.AILords.SK_X7 => 6,
            Enums.AILords.SK_X8 => 7,
            _ => -1
        };

        return playerId != -1;
    }

    /// <summary>
    /// Checks if the given lord is a custom lord at all.
    /// </summary>
    /// <param name="lord">The lord enum value (e.g. <see cref="Enums.AILords.SK_X1"/>).</param>
    /// <returns><see langword="true"/> if the enum represents a custom lord; otherwise <see langword="false"/>.</returns>
    public bool IsCustomLord(Enums.AILords lord)
    {
        return lord switch
        {
            Enums.AILords.SK_X1 => true,
            Enums.AILords.SK_X2 => true,
            Enums.AILords.SK_X3 => true,
            Enums.AILords.SK_X4 => true,
            Enums.AILords.SK_X5 => true,
            Enums.AILords.SK_X6 => true,
            Enums.AILords.SK_X7 => true,
            Enums.AILords.SK_X8 => true,
            _ => false
        };
    }


    // ---------------------------------------------------------------------------------------
    // Internal: Face, Voice Line Retrieval
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Retrieves the Noesis image source for the given lord's face portrait, loading and caching it on first access.
    /// </summary>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="imageSource">The resolved <see cref="Noesis.ImageSource"/>, or <see langword="null"/> on failure.</param>
    /// <returns><see langword="true"/> if the face was found and loaded; otherwise <see langword="false"/>.</returns>
    internal bool TryGetFace(string name, out Noesis.ImageSource? imageSource)
    {
        imageSource = null;
        if (!LordDataDict.TryGetValue(name.ToLower(), out CustomLordEntry? cle))
        {
            LogHelper.Error($"lord not found [{name}]");
            return false;
        }

        if (string.IsNullOrWhiteSpace(cle.LordInfo.FacePath))
            return false;

        // Lazily load the texture and wrap it in a Noesis source.
        if (cle.FaceTexture == null || cle.Face == null)
        {
            if (GameAssetManagerAPI.Instance.TryLoadTexture(cle.LordInfo.FacePath, out Texture2D? texture) && texture != null)
            {
                cle.FaceTexture = texture;
                cle.Face = new Noesis.TextureSource(texture);
            }
        }

        imageSource = cle.Face;
        return imageSource != null;
    }

    /// <summary>
    /// Resolves the audio path for the event that the lord joins the skirmish lobby.
    /// </summary>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="audioPath">The resolved audio path, or <see cref="string.Empty"/> if none.</param>
    /// <returns><see langword="true"/> if a clip was found; otherwise <see langword="false"/>.</returns>
    internal bool TryGetJoinAudio(string name, [NotNullWhen(true)] out string? audioPath)
    {
        audioPath = string.Empty;
        string nameLower = name.ToLower();

        if (!LordDataDict.TryGetValue(nameLower, out CustomLordEntry? cle))
        {
            LogHelper.Error($"lord not found [{name}]");
            return false;
        }

        if (string.IsNullOrEmpty(cle.LordInfo.JoinAudioPath))
        {
            LogHelper.Information($"No join audio for lord [{nameLower}]");
            return false;
        }
        audioPath = cle.LordInfo.JoinAudioPath;

        LogHelper.Information($"Resolved join audio for lord [{nameLower}]: [{audioPath}]");
        return true;
    }

    /// <summary>
    /// Resolves the audio paths for the event that the lord leaves the skirmish lobby.
    /// </summary>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="audioPath">The resolved audio path, or <see cref="string.Empty"/> if none.</param>
    /// <returns><see langword="true"/> if a clip was found; otherwise <see langword="false"/>.</returns>
    internal bool TryGetLeaveAudio(string name, [NotNullWhen(true)] out string? audioPath)
    {
        audioPath = string.Empty;
        string nameLower = name.ToLower();

        if (!LordDataDict.TryGetValue(nameLower, out CustomLordEntry? cle))
        {
            LogHelper.Error($"lord not found [{name}]");
            return false;
        }

        if (string.IsNullOrEmpty(cle.LordInfo.LeaveAudioPath))
        {
            LogHelper.Information($"No leave audio for lord [{nameLower}]");
            return false;
        }
        audioPath = cle.LordInfo.LeaveAudioPath;

        LogHelper.Information($"Resolved leave audio for lord [{nameLower}]: [{audioPath}]");
        return true;
    }

    /// <summary>
    /// Resolves the video and audio paths for the given lord and message type,
    /// selecting a random clip from available options and caching the subtitle text.
    /// </summary>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="msgType">The <see cref="AILordMessageType"/> to look up.</param>
    /// <param name="videoPath">The resolved video path, or <see cref="string.Empty"/> if none.</param>
    /// <param name="audioPath">The resolved audio path, or <see cref="string.Empty"/> if none.</param>
    /// <returns><see langword="true"/> if at least one clip was found; otherwise <see langword="false"/>.</returns>
    internal bool TryGetVideoAndAudio(string name, AILordMessageType msgType, out string videoPath, out string audioPath)
    {
        videoPath = string.Empty;
        audioPath = string.Empty;
        string nameLower = name.ToLower();

        if (!LordDataDict.TryGetValue(nameLower, out CustomLordEntry? cle))
        {
            LogHelper.Error($"lord not found [{name}]");
            return false;
        }

        if (!cle.VoiceLines.TryGetValue(msgType, out List<LordMessageClip>? clips) || clips.Count == 0)
        {
            LogHelper.Information($"No clips found for lord [{nameLower}] message type [{msgType}]");
            return false;
        }

        LordMessageClip selected = clips[DeterministicRandom.Next(0, clips.Count)];
        videoPath = selected.VideoPath;
        audioPath = selected.AudioPath;

        string subtitleText = GetLocalizedText(selected.LocalizedText);
        if (!string.IsNullOrEmpty(subtitleText))
            _lastSpokenTextCache[nameLower] = subtitleText;
        else
            _lastSpokenTextCache.Remove(nameLower);

        LogHelper.Information($"Resolved clip for lord [{nameLower}] message type [{msgType}]");
        return true;
    }

    // ---------------------------------------------------------------------------------------
    // Internal: Lua Init Path
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Attempts to retrieve the absolute path to the <c>init.lua</c> script for the given lord.
    /// </summary>
    /// <param name="name">The internal lord name (case-insensitive).</param>
    /// <param name="luaInitPath">The absolute path if found; otherwise <see langword="null"/>.</param>
    /// <returns>
    /// <see langword="true"/> if the lord was found and has a non-empty Lua init path; otherwise <see langword="false"/>.
    /// </returns>
    internal bool TryGetLuaInitPath(string name, [NotNullWhen(true)] out string? luaInitPath)
    {
        luaInitPath = null;
        if (!LordDataDict.TryGetValue(name.ToLower(), out CustomLordEntry? cle))
            return false;

        if (string.IsNullOrEmpty(cle.LuaInitPath))
            return false;

        luaInitPath = cle.LuaInitPath!;
        return true;
    }

    // ---------------------------------------------------------------------------------------
    // Internal: Developer Utilities
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Returns the underlying array of AICs managed by this instance.
    /// </summary>
    /// <returns>A <see cref="SimpleNativeArray{GameBuilding}"/> containing all buildings. </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public SimpleNativeArray<InternalAIC> GetAICArray()
    {
        return _aicArray;
    }

    /// <summary>
    /// Exports all embedded AIV binary data from the game assembly to the <c>_EXPORT</c> directory
    /// adjacent to the game executable.
    /// </summary>
    /// <remarks>
    /// This is a developer/debugging utility. The output format is raw <c>short[]</c> arrays
    /// written as binary files, one per field per row.
    /// </remarks>
    public static void DumpEmbeddedAIVs()
    {
        string outputDirectory = Path.Combine(DirectoryHelpers.GameDirectory, "_EXPORT");
        LogHelper.Information($"Dumping embedded AIVs to [{outputDirectory}]");
        Directory.CreateDirectory(outputDirectory);

        IEnumerable<FieldInfo> fields = typeof(AIVLoader)
            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic)
            .Where(f => f.FieldType == typeof(short[][]));

        foreach (FieldInfo field in fields)
        {
            short[][] data = (short[][])field.GetValue(null);
            for (int i = 0; i < data.Length; i++)
            {
                string filePath = Path.Combine(outputDirectory, $"{field.Name}_{i}.baiv");
                using FileStream fs = File.Create(filePath);
                using BinaryWriter bw = new BinaryWriter(fs);
                foreach (short value in data[i])
                    bw.Write(value);
            }
        }
    }

    /// <summary>
    /// Exports all embedded AIC binary data from the game native library to the <c>_EXPORT</c> directory
    /// adjacent to the game executable.
    /// </summary>
    /// <remarks>
    /// This is a developer/debugging utility. The output format is a internal aic of direct mapping from memory
    /// written as binary files.
    /// </remarks>
    public unsafe void DumpEmbeddedAICs()
    {
        string outputDirectory = Path.Combine(DirectoryHelpers.GameDirectory, "_EXPORT");
        LogHelper.Information($"Dumping embedded AICs to [{outputDirectory}]");
        Directory.CreateDirectory(outputDirectory);

        UInt64 pAILordManagerVA = (UInt64)Instance.GetAICArray().GetArrayAddress();
        if (pAILordManagerVA == 0)
        {
            LogHelper.Error("Cannot dump AICs due to lordManagerRVA being null. Was this not found?");
            return;
        }

        int lordCount = Enum.GetValues(typeof(Enums.AILords)).Length - 1;
        InternalAIC* pAILordManager = (InternalAIC*)pAILordManagerVA;
        InternalAIC* pCurrentAIC = null;
        for (int lordId = 1; lordId < lordCount; lordId++)
        {
            Enums.AILords lord = (Enums.AILords)(lordId);
            pCurrentAIC = &pAILordManager[lordId];
            LogHelper.Information($"Dumping AIC [{lord}]@0x{new IntPtr(pCurrentAIC).ToString("X16")}");

            ReadOnlySpan<byte> bytes = MemoryMarshal.AsBytes(new ReadOnlySpan<byte>((void*)pCurrentAIC, sizeof(InternalAIC)));
            string filePath = Path.Combine(outputDirectory, $"{lord}.baic");
            File.WriteAllBytes(filePath, bytes.ToArray());
        }
    }

    /// <summary>
    /// Sets a AIC of a specified lord to the contents of a binary aic blob.
    /// </summary>
    /// <param name="lord">The lord to set the AIC for.</param>
    /// <param name="aic">The AIC binary blob</param>
    public unsafe void SetAICFromBytes(Enums.AILords lord, ReadOnlySpan<byte> aic)
    {
        UInt64 pAILordManagerVA = (UInt64)Instance.GetAICArray().GetArrayAddress();
        if (pAILordManagerVA == 0)
        {
            LogHelper.Error("Cannot set AIC due to lordManagerRVA being null. Was this not found?");
            return;
        }

        InternalAIC* pAILordManager = (InternalAIC*)pAILordManagerVA;
        fixed (byte* ptr = aic)
        {
            pAILordManager[(int)lord] = *(InternalAIC*)ptr;
        }
    }

    // ---------------------------------------------------------------------------------------
    // Private Helpers
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Resolves the best available localized string from a locale dictionary.
    /// Falls back to <c>en-US</c> if the current language has no entry.
    /// </summary>
    /// <param name="localizedStore">
    /// A dictionary of locale-keyed strings (e.g. <c>"en-US" -> "Hello"</c>), or <see langword="null"/>.
    /// </param>
    /// <returns>The resolved string, or <c>"not-set"</c> if no entry could be found.</returns>
    private string GetLocalizedText(Dictionary<string, string>? localizedStore)
    {
        if (localizedStore == null)
            return "not-set";

        string lang = GameAssetManagerAPI.Instance.CurrentLanguage;

        if (localizedStore.TryGetValue(lang, out string? locText))
            return locText;

        // Fallback to English when current language has no entry.
        if (lang != GameAssetManagerAPI.DEFAULT_LOCALE
            && localizedStore.TryGetValue(GameAssetManagerAPI.DEFAULT_LOCALE, out string? enText))
            return enText;

        return "not-set";
    }

    /// <summary>
    /// Resolves optional localized metadata without exposing placeholder text in the game UI.
    /// </summary>
    private string GetOptionalLocalizedText(Dictionary<string, string>? localizedStore)
    {
        if (localizedStore == null)
            return string.Empty;

        string lang = GameAssetManagerAPI.Instance.CurrentLanguage;
        if (localizedStore.TryGetValue(lang, out string? localized) && !string.IsNullOrWhiteSpace(localized))
            return localized.Trim();

        if (!string.Equals(lang, GameAssetManagerAPI.DEFAULT_LOCALE, StringComparison.OrdinalIgnoreCase) &&
            localizedStore.TryGetValue(GameAssetManagerAPI.DEFAULT_LOCALE, out string? english) &&
            !string.IsNullOrWhiteSpace(english))
        {
            return english.Trim();
        }

        return string.Empty;
    }

    /// <summary>
    /// Attempts to read and deserialize a JSON file into the given type, logging warnings on failure.
    /// </summary>
    /// <typeparam name="T">The type to deserialize into.</typeparam>
    /// <param name="path">Absolute path to the JSON file.</param>
    /// <param name="fileName">Short display name used in log messages.</param>
    /// <param name="result">The deserialized value if successful; otherwise <see langword="null"/>.</param>
    /// <returns><see langword="true"/> if the file was read and deserialized successfully; otherwise <see langword="false"/>.</returns>
    private static bool TryDeserializeJson<T>(string path, string fileName, [NotNullWhen(true)] out T? result)
    {
        result = default;
        if (!File.Exists(path))
        {
            LogHelper.Warning($"Missing [{fileName}]: [{path}]");
            return false;
        }

        result = JsonSerializer.Deserialize<T>(File.ReadAllText(path));
        if (result == null)
        {
            LogHelper.Error($"Malformed or outdated [{fileName}]: [{path}]");
            return false;
        }

        return true;
    }
}
