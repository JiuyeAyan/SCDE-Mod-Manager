using SHCDESE.Logging;
using Steamworks;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace SHCDESE.Extensions;

public static class SteamAppsExtensions
{
    private static readonly Dictionary<string, string> SteamToCultureMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        { "arabic", "ar-SA" },
        { "bulgarian", "bg-BG" },
        { "schinese", "zh-CN" },
        { "tchinese", "zh-TW" },
        { "czech", "cs-CZ" },
        { "danish", "da-DK" },
        { "dutch", "nl-NL" },
        { "english", "en-US" },
        { "finnish", "fi-FI" },
        { "french", "fr-FR" },
        { "german", "de-DE" },
        { "greek", "el-GR" },
        { "hungarian", "hu-HU" },
        { "indonesian", "id-ID" },
        { "italian", "it-IT" },
        { "japanese", "ja-JP" },
        { "koreana", "ko-KR" },
        { "norwegian", "nb-NO" },
        { "polish", "pl-PL" },
        { "portuguese", "pt-PT" },
        { "brazilian", "pt-BR" },
        { "romanian", "ro-RO" },
        { "russian", "ru-RU" },
        { "spanish", "es-ES" },
        { "latam", "es-419" },
        { "swedish", "sv-SE" },
        { "thai", "th-TH" },
        { "turkish", "tr-TR" },
        { "ukrainian", "uk-UA" },
        { "vietnamese", "vi-VN" }
    };

    extension(SteamApps)
    {
        /// <summary>
        /// Gets the current Steam game language and formats it as a BCP-47 Culture name (e.g., "de-DE")
        /// </summary>
        public static string GetCurrentGameCultureName(string fallbackCulture = "en-US")
        {
            try
            {
                string steamLanguage = SteamApps.GetCurrentGameLanguage();
                LogHelper.Debug($"Steam language returned: [{steamLanguage}]");

                if (string.IsNullOrWhiteSpace(steamLanguage))
                    return fallbackCulture;

                if (SteamToCultureMap.TryGetValue(steamLanguage, out string cultureName))
                    return cultureName;

                LogHelper.Warning($"Steam language [{steamLanguage}] not found in culture map.");
                return fallbackCulture;
            } 
            catch (InvalidOperationException ex)
            {
                LogHelper.Warning($"Errur during game culture retrieval from steam (may be okay): {ex}");
            }
            return fallbackCulture;
        }

        /// <summary>
        /// Gets the current Steam game language directly as a .NET CultureInfo object.
        /// </summary>
        public static CultureInfo GetCurrentGameCultureInfo(string fallbackCulture = "en-US")
        {
            return new CultureInfo(GetCurrentGameCultureName(fallbackCulture));
        }
    }
}