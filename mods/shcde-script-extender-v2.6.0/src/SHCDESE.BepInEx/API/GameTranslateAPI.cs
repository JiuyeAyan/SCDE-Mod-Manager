using CrusaderDE;
using R3;
using SHCDESE.EventAPI;
using SHCDESE.EventAPI.MapLoader;
using SHCDESE.Logging;
using SHCDESE.Lua.DocsGen;
using SHCDESE.LUA.DocsGen;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace SHCDESE.API;

/// <summary>
/// Provides an API for interacting with and overriding the game's text translation system.
/// Supports localization files (crusader.txt format) and temporary/permanent overrides.
/// </summary>
/// <remarks>
/// This singleton class allows scripts to dynamically change in-game text. It works by intercepting the
/// game's internal `Translate.lookUpText` methods. When the game requests a piece of text, this API
/// first checks if a custom override exists. If so, it returns the custom text; otherwise, it allows
/// the original game function to proceed. Supports both permanent overrides (cleared on map unload) and
/// temporary overrides (cleared manually).
/// </remarks>
[LuaApiNamespace("Translate")]
public sealed class GameTranslateAPI
{
#pragma warning disable 0618
    private static readonly Lazy<GameTranslateAPI> _lazy = new(() => new GameTranslateAPI());
    public static GameTranslateAPI Instance => _lazy.Value;

    // Permanent overrides (cleared on map unload)
    private Dictionary<string, string> _lookUpTextOverrides;
    private Dictionary<string, string> _lookUpTextExOverrides;

    // Temporary overrides (cleared manually via ReleaseTemporaryOverrides)
    private Dictionary<string, string> _tempLookUpTextOverrides;
    private Dictionary<string, string> _tempLookUpTextExOverrides;

    // Localization file overrides (loaded from crusader.txt files in mod folders)
    private Dictionary<string, Dictionary<string, string>> _localizedTexts; // locale -> (key -> text)

    /// <summary>
    /// Describes the MOD_FOLDER/LOCALE_FOLDER_PREFIX folder responsible for the
    /// global translation system.
    /// </summary>
    internal const string LOCALE_FOLDER_PREFIX = "Locales";

    private int _initialized = 0;

    private GameTranslateAPI()
    {
        _lookUpTextOverrides = new Dictionary<string, string>();
        _lookUpTextExOverrides = new Dictionary<string, string>();
        _tempLookUpTextOverrides = new Dictionary<string, string>();
        _tempLookUpTextExOverrides = new Dictionary<string, string>();
        _localizedTexts = new Dictionary<string, Dictionary<string, string>>();
    }

    internal static void InitializeSubscribers()
    {
        if (Interlocked.Exchange(ref Instance._initialized, 1) != 0)
            return;

        LogHelper.Information($"Setting up GameTranslateAPI subscribers");

        MapLoaderR3EventHooks.OnUnloadMap.Observable.Subscribe(OnMapUnload);
    }

    internal void Unload()
    {
        _localizedTexts.Clear();
        _tempLookUpTextExOverrides.Clear();
        _tempLookUpTextOverrides.Clear();
        _lookUpTextExOverrides.Clear();
        _lookUpTextOverrides.Clear();
    }

    /// <summary>
    /// Clear permanent overrides when a map is unloaded.
    /// Temporary overrides and localization files persist across map loads.
    /// </summary>
    /// <param name="args">Args from the event</param>
    private static void OnMapUnload(MapUnloadEventArgs args)
    {
        Instance._lookUpTextOverrides.Clear();
        Instance._lookUpTextExOverrides.Clear();
    }

    // ---------------------------------------------------------------------------------------
    // LOCALIZATION FILE SUPPORT
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Loads a crusader.txt localization file from the specified path.
    /// The file should follow the game's format: locale code, >>TEXTSTART, numbered sections, etc.
    /// Locale codes are normalized to BCP 47 format (e.g., "deDE" becomes "de-DE").
    /// </summary>
    /// <param name="filePath">Absolute path to the crusader.txt file</param>
    /// <param name="locale">The locale code this file represents (e.g., "de-DE", "en-US"). If null, extracted from file.</param>
    /// <returns>True if loaded successfully, false otherwise</returns>
    public bool LoadLocalizationFile(string filePath, string? locale = null)
    {
        if (!File.Exists(filePath))
        {
            LogHelper.Warning($"Localization file not found: {filePath}");
            return false;
        }

        try
        {
            string[] lines = File.ReadAllLines(filePath);
            string? detectedLocale = locale;
            Dictionary<string, string> translations = new Dictionary<string, string>();

            string? currentSection = null;
            int currentIndex = 0;
            List<string> currentTextLines = new List<string>();

            for (int i = 0; i < lines.Length; i++)
            {
                string line = lines[i].Trim();

                // Detect locale code at start of file
                if (i == 0 && string.IsNullOrEmpty(detectedLocale))
                {
                    detectedLocale = NormalizeLocaleCode(line);
                    LogHelper.Information($"Detected locale: {detectedLocale}");
                    continue;
                }

                // Skip TEXTSTART marker
                if (line == ">>TEXTSTART")
                    continue;

                // Detect section headers (e.g., ">>1-----", ">>2-----")
                if (line.StartsWith(">>") && line.Contains("-----"))
                {
                    // Save previous section if exists
                    if (currentSection != null && currentTextLines.Count > 0)
                    {
                        SaveTranslationSection(translations, currentIndex, currentTextLines);
                    }

                    // Parse section number
                    string sectionNum = line.Substring(2, line.IndexOf('-') - 2);
                    currentIndex = int.Parse(sectionNum);
                    currentSection = "TEXT_SECTION"; // Generic section name (not actually used anymore)
                    currentTextLines.Clear();
                    continue;
                }

                // Accumulate text lines for current section
                if (currentSection != null)
                {
                    currentTextLines.Add(line);
                }
            }

            // Save last section
            if (currentSection != null && currentTextLines.Count > 0)
            {
                SaveTranslationSection(translations, currentIndex, currentTextLines);
            }

            if (string.IsNullOrEmpty(detectedLocale))
            {
                LogHelper.Warning($"Could not detect locale from file: {filePath}");
                return false;
            }

            // Normalize the locale code if it was passed in
            if (locale != null)
            {
                detectedLocale = NormalizeLocaleCode(detectedLocale);
            }

            // Merge translations into the existing locale dictionary so that multiple mods
            // can each provide a partial crusader.txt for the same locale without clobbering
            // one another. Duplicate keys are last-write-wins within the merge.
            if (!_localizedTexts.TryGetValue(detectedLocale, out Dictionary<string, string>? localeTexts))
            {
                localeTexts = new Dictionary<string, string>();
                _localizedTexts[detectedLocale] = localeTexts;
            }

            foreach (KeyValuePair<string, string> entry in translations)
            {
                localeTexts[entry.Key] = entry.Value;
            }

            LogHelper.Information($"Merged {translations.Count} translations for locale {detectedLocale} from {filePath} (total for locale: {localeTexts.Count})");

            return true;
        }
        catch (Exception ex)
        {
            LogHelper.Error(ex, $"Failed to load localization file: {filePath}");
            return false;
        }
    }

    /// <summary>
    /// Normalizes locale codes to BCP 47 format with hyphen separator.
    /// Converts "deDE" to "de-DE", "enUS" to "en-US", etc.
    /// </summary>
    private string NormalizeLocaleCode(string localeCode)
    {
        if (string.IsNullOrEmpty(localeCode))
            return localeCode;

        // Already in correct format (has hyphen)
        if (localeCode.Contains("-"))
            return localeCode;

        // Convert 4-character codes: deDE -> de-DE
        if (localeCode.Length == 4)
        {
            return $"{localeCode.Substring(0, 2)}-{localeCode.Substring(2, 2).ToUpper()}";
        }

        // Convert 4+ character codes: ptbr -> pt-BR, zhcn -> zh-CN
        if (localeCode.Length >= 4)
        {
            return $"{localeCode.Substring(0, 2)}-{localeCode.Substring(2).ToUpper()}";
        }

        // Special case for 2-character codes (like "ar")
        if (localeCode.Length == 2)
        {
            return localeCode.ToLower();
        }

        return localeCode;
    }

    /// <summary>
    /// Helper to save a translation section from crusader.txt format.
    /// The section number corresponds to an index in GameSectionNames array.
    /// Each line in the section gets stored with format: SECTION_NAME_XXX where XXX is the 0-based line number
    /// </summary>
    private void SaveTranslationSection(Dictionary<string, string> translations, int sectionIndex, List<string> textLines)
    {
        int arrayIndex = sectionIndex - 1;

        if (arrayIndex < 0 || arrayIndex >= Translate.Instance.SectionNames.Length)
        {
            LogHelper.Warning($"Unknown section index: {sectionIndex}. Skipping.");
            return;
        }

        string sectionName = Translate.Instance.SectionNames[arrayIndex];

        // Each line gets its own numbered key: TEXT_MONTHS_000, TEXT_MONTHS_001, etc.
        for (int i = 0; i < textLines.Count; i++)
        {
            if (!string.IsNullOrEmpty(textLines[i]))
            {
                // Game uses 0-based indexing with format: SECTION_NNN
                string key = $"{sectionName}_{(i):D3}";
                translations[key] = textLines[i];
                LogHelper.Debug($"Added translation: {key}");
            }
        }
    }

    /// <summary>
    /// Loads all crusader.txt files from a mod's Locales directory.
    /// Expected structure: {modDirectory}/Locales/{locale}/crusader.txt
    /// </summary>
    /// <param name="modDirectory">The root directory of the mod</param>
    /// <returns>Number of localization files loaded</returns>
    public int LoadModLocalizations(string modDirectory)
    {
        string localesDir = Path.Combine(modDirectory, LOCALE_FOLDER_PREFIX);
        if (!Directory.Exists(localesDir))
        {
            LogHelper.Debug($"No Locales directory found in {modDirectory}");
            return 0;
        }

        int loadedCount = 0;

        // Iterate through locale subdirectories (e.g., deDE, frFR, enUS)
        foreach (string localeDir in Directory.GetDirectories(localesDir))
        {
            string locale = Path.GetFileName(localeDir);
            string crusaderFile = Path.Combine(localeDir, "crusader.txt");

            if (File.Exists(crusaderFile))
            {
                if (LoadLocalizationFile(crusaderFile, locale))
                {
                    loadedCount++;
                }
            }
        }

        LogHelper.Information($"Loaded {loadedCount} localization files from {modDirectory}");
        return loadedCount;
    }

    /// <summary>Loads localization files from the indexed namespace of a loose or packed mod.</summary>
    internal int LoadModLocalizations(string modGuid, string sourceName)
    {
        int loadedCount = 0;
        foreach (string resourcePath in GameAssetManagerAPI.Instance.GetModFilePaths(modGuid, "Locales/"))
        {
            string normalized = resourcePath.Replace('\\', '/');
            if (!normalized.EndsWith("/crusader.txt", StringComparison.OrdinalIgnoreCase))
                continue;

            string[] parts = normalized.Split('/');
            if (parts.Length != 3 || !string.Equals(parts[0], "Locales", StringComparison.OrdinalIgnoreCase))
                continue;

            if (GameAssetManagerAPI.Instance.TryGetModFilePath(modGuid, normalized, out string filePath) && LoadLocalizationFile(filePath, parts[1]))
                loadedCount++;
        }

        LogHelper.Information($"Loaded {loadedCount} localization files from {sourceName}");
        return loadedCount;
    }

    // ---------------------------------------------------------------------------------------
    // ORIGINAL LOOKUP METHODS (WITH LOCALIZATION SUPPORT)
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Gets the original, untranslated text from the game's dictionary using a simple key.
    /// </summary>
    /// <param name="index">The raw text key (e.g., "TEXT_IN_CHURCH_000").</param>
    /// <returns>The original game text associated with the key.</returns>
    [LuaApiExport("Translate_GetLookUpText")]
    public string GetLookUpText(string index)
    {
        return Translate.Instance.lookUpText(index);
    }

    /// <summary>
    /// Overrides a piece of game text that is identified by a single, unique string key.
    /// This is a permanent override that persists until the map is unloaded.
    /// </summary>
    /// <param name="index">The raw text key to override (e.g., "TEXT_IN_CHURCH_000").</param>
    /// <param name="text">The new custom text that will be displayed instead.</param>
    /// <returns><c>true</c> if the override was successfully added; <c>false</c> if an override for this key already exists.</returns>
    [LuaApiExport("SetLookUpText")]
    public bool SetLookUpText(string index, string text)
    {
        if (!_lookUpTextOverrides.ContainsKey(index))
        {
            LogHelper.Information($"Added permanent override: {index}={text}");
            _lookUpTextOverrides.Add(index, text);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Overrides a piece of game text temporarily. This override persists across map loads
    /// until explicitly released via <see cref="ReleaseTemporaryOverride"/> or <see cref="ReleaseAllTemporaryOverrides"/>.
    /// </summary>
    /// <param name="index">The raw text key to override.</param>
    /// <param name="text">The new custom text.</param>
    /// <returns>A handle (the key) that can be used to release this override later.</returns>
    [LuaApiExport("SetTemporaryLookUpText")]
    public string SetTemporaryLookUpText(string index, string text)
    {
        LogHelper.Information($"Added temporary override: {index}={text}");
        _tempLookUpTextOverrides[index] = text;
        return index;
    }

    /// <summary>
    /// For internal use by detours or the C# API. Gets the custom override text for a simple key, if it exists.
    /// Checks temporary overrides first, then permanent overrides, then localized texts for the
    /// current language, then falls back to en-US so that mods shipping only an English
    /// crusader.txt still surface their mod-specific keys on non-English installs.
    /// </summary>
    /// <param name="index">The raw text key.</param>
    /// <returns>The custom text, or an empty string if no override is found.</returns>
    public string GetOverwrittenLookUpText(string index)
    {
        //LogHelper.Verbose($"index={index}");

        // Priority 1: Temporary overrides
        if (_tempLookUpTextOverrides.TryGetValue(index, out string? text))
            return text;

        // Priority 2: Permanent overrides
        if (_lookUpTextOverrides.TryGetValue(index, out text))
            return text;

        // Priority 3: Localized texts from crusader.txt files (current language)
        if (_localizedTexts.TryGetValue(GameAssetManagerAPI.Instance.CurrentLanguage, out Dictionary<string, string>? localeTexts))
        {
            if (localeTexts.TryGetValue(index, out text))
                return text;
        }

        // Priority 4: en-US fallback, covers mod-specific keys when the mod only ships an
        // English crusader.txt. Without this, those keys would silently return empty on any
        // non-English install because the game's own translation layer has no entry for them.
        if (GameAssetManagerAPI.Instance.CurrentLanguage != GameAssetManagerAPI.DEFAULT_LOCALE &&
            _localizedTexts.TryGetValue(GameAssetManagerAPI.DEFAULT_LOCALE, out Dictionary<string, string>? fallbackTexts))
        {
            if (fallbackTexts.TryGetValue(index, out text))
                return text;
        }


        return string.Empty;
    }

    /// <summary>
    /// Gets the original, untranslated text from the game's dictionary using a section and an index.
    /// </summary>
    /// <param name="sectionString">The text section (e.g., `eTextSections.TEXT_BUBBLE_HELP_TEXT`).</param>
    /// <param name="index">The numeric index within the section.</param>
    /// <returns>The original game text.</returns>
    [LuaApiExport("GetLookUpTextEx")]
    public string GetLookUpTextEx(string sectionString, int index)
    {
        return Translate.Instance.lookUpText(sectionString, index);
    }

    /// <summary>
    /// Overrides a piece of game text that is identified by a section and a numeric index.
    /// This is a permanent override that persists until the map is unloaded.
    /// </summary>
    /// <param name="sectionString">The text section (e.g., `eTextSections.TEXT_BUBBLE_HELP_TEXT`).</param>
    /// <param name="index">The numeric index within the section.</param>
    /// <param name="text">The new custom text that will be displayed instead.</param>
    /// <returns><c>true</c> if the override was successfully added; <c>false</c> if an override for this key combination already exists.</returns>
    [LuaApiExport("SetLookUpTextEx")]
    public bool SetLookUpTextEx(string sectionString, int index, string text)
    {
        string key = sectionString + "_" + index.ToString("D3");
        if (!_lookUpTextExOverrides.ContainsKey(key))
        {
            LogHelper.Debug($"Added permanent override: {key}={text}");
            _lookUpTextExOverrides.Add(key, text);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Overrides a piece of game text temporarily using section and index.
    /// This override persists across map loads until explicitly released.
    /// </summary>
    /// <param name="sectionString">The text section.</param>
    /// <param name="index">The numeric index within the section.</param>
    /// <param name="text">The new custom text.</param>
    /// <returns>A handle (the generated key) that can be used to release this override later.</returns>
    [LuaApiExport("SetTemporaryLookUpTextEx")]
    public string SetTemporaryLookUpTextEx(string sectionString, int index, string text)
    {
        string key = sectionString + "_" + index.ToString("D3");
        LogHelper.Debug($"Added temporary override: [{key}]={text}");
        _tempLookUpTextExOverrides[key] = text;
        return key;
    }

    /// <summary>
    /// For internal use by detours or the C# API. Gets the custom override text for a section/index key, if it exists.
    /// Checks temporary overrides first, then permanent overrides, then localized texts for the
    /// current language, then falls back to en-US so that mods shipping only an English
    /// crusader.txt still surface their mod-specific keys on non-English installs.
    /// </summary>
    /// <param name="sectionString">The text section.</param>
    /// <param name="index">The numeric index.</param>
    /// <returns>The custom text, or an empty string if no override is found.</returns>
    public string GetOverwrittenLookUpTextEx(string sectionString, int index)
    {
        string key = sectionString + "_" + index.ToString("D3");
        //LogHelper.Verbose($"key=[{key}]");

        // Priority 1: Temporary overrides
        if (_tempLookUpTextExOverrides.TryGetValue(key, out string? text))
            return text;

        // Priority 2: Permanent overrides
        if (_lookUpTextExOverrides.TryGetValue(key, out text))
            return text;

        // Priority 3: Localized texts (current language)
        if (_localizedTexts.TryGetValue(GameAssetManagerAPI.Instance.CurrentLanguage, out Dictionary<string, string>? localeTexts))
        {
            if (localeTexts.TryGetValue(key, out text))
                return text;

        }

        // Priority 4: en-US fallback, covers mod-specific keys when the mod only ships an
        // English crusader.txt. Without this, those keys would silently return empty on any
        // non-English install because the game's own translation layer has no entry for them.
        if (GameAssetManagerAPI.Instance.CurrentLanguage != GameAssetManagerAPI.DEFAULT_LOCALE &&
            _localizedTexts.TryGetValue(GameAssetManagerAPI.DEFAULT_LOCALE, out Dictionary<string, string>? fallbackTexts))
        {
            if (fallbackTexts.TryGetValue(key, out text))
                return text;
        }

        return string.Empty;
    }

    // ---------------------------------------------------------------------------------------
    // TEMPORARY OVERRIDE MANAGEMENT
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Releases a specific temporary override by its key.
    /// </summary>
    /// <param name="key">The key returned when the temporary override was created.</param>
    /// <returns>True if the override was found and removed, false otherwise.</returns>
    [LuaApiExport("ReleaseTemporaryOverride")]
    public bool ReleaseTemporaryOverride(string key)
    {
        bool removed = _tempLookUpTextOverrides.Remove(key);
        removed |= _tempLookUpTextExOverrides.Remove(key);

        if (removed)
        {
            LogHelper.Information($"Released temporary override: {key}");
        }

        return removed;
    }

    /// <summary>
    /// Releases all temporary overrides at once.
    /// </summary>
    [LuaApiExport("ReleaseAllTemporaryOverrides")]
    public void ReleaseAllTemporaryOverrides()
    {
        int count = _tempLookUpTextOverrides.Count + _tempLookUpTextExOverrides.Count;
        _tempLookUpTextOverrides.Clear();
        _tempLookUpTextExOverrides.Clear();
        LogHelper.Information($"Released {count} temporary overrides");
    }

    /// <summary>
    /// Gets the count of active temporary overrides.
    /// </summary>
    [LuaApiExport("GetTemporaryOverrideCount")]
    public int GetTemporaryOverrideCount()
    {
        return _tempLookUpTextOverrides.Count + _tempLookUpTextExOverrides.Count;
    }

    /// <summary>
    /// Gets the count of active permanent overrides.
    /// </summary>
    [LuaApiExport("GetPermanentOverrideCount")]
    public int GetPermanentOverrideCount()
    {
        return _lookUpTextOverrides.Count + _lookUpTextExOverrides.Count;
    }

    /// <summary>
    /// Gets all loaded locale codes.
    /// </summary>
    [LuaApiExport("GetLoadedLocales")]
    public string[] GetLoadedLocales()
    {
        List<string> locales = new List<string>(_localizedTexts.Keys);
        return locales.ToArray();
    }

    /// <summary>
    /// Gets the translation count for a specific locale.
    /// </summary>
    [LuaApiExport("GetLocaleTranslationCount")]
    public int GetLocaleTranslationCount(string locale)
    {
        if (_localizedTexts.TryGetValue(locale, out Dictionary<string, string>? translations))
        {
            return translations.Count;
        }
        return 0;
    }

#pragma warning restore 0618
}
