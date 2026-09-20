# SCDE Mod Manager language packs

Introduced in Manager 0.2.7; game-copy location and per-Mod read interface added in 0.2.8. No JavaScript, plugin DLL or recompilation is required to translate supplied text.

## Location

Once a valid original game directory is selected, the Manager creates these files in its adjacent **game copy**, even before the first game launch:

```text
<Stronghold Crusader Definitive Edition - SCDE Modded>\BepInEx\config\modmanager\lang\
  en.json
  zh-CN.json
```

Use **Open config Folder** in the main window, then open `modmanager\lang`. This is not the original Steam game directory or the temporary portable extraction directory. Electron's `.pak` resources are not author translation files. Existing translated values are preserved. Missing keys in valid English/Chinese templates are added through an atomic replacement so translators can find new UI text in the files, not just in fallback code. Malformed files are left intact. Other community packs remain read-only. Only English and Simplified Chinese are supplied. The source templates are `src/locales/en.json` and `src/locales/zh-CN.json`.

The old `%LOCALAPPDATA%\SCDE Mod Manager\manager-data\config\modmanager` folder is used before selecting a valid game directory. When a game copy is selected, missing JSON files are copied from that old folder without removing the originals or overwriting existing files in the new location. The game-copy location is then authoritative. Manager-controlled game-copy rebuilding preserves `BepInEx\config` through a temporary sibling backup; a failed rebuild retains that backup and reports its path. Manually deleting the game-copy directory outside the Manager is not protected by this operation.

## Add a translation

1. Copy `en.json` and name it with a language tag, for example `fr.json`, `ja.json`, `de.json` or `pt-BR.json`.
2. Set `language` to that tag, `name` to its readable language name, and translate string **values** under `strings`. Keep property names unchanged.
3. Keep placeholders such as `{name}`, `{count}`, `{path}` and `{version}` exactly as written. Their order can change; the set must not change. Preserve JSON escaping, including `\n` for line breaks.
4. Save as UTF-8 JSON. A UTF-8 BOM is accepted. Comments/trailing commas are not JSON. Each file is limited to 512 KiB and each string to 16,000 characters. Link/junction entries are not language packs.
5. Close and reopen the Manager to load edits. There is no per-frame file watcher or network translation service.

A partial example (not a complete translation):

```json
{
  "language": "fr",
  "name": "Français",
  "direction": "ltr",
  "strings": {
    "main": {
      "importMods": "Importer des mods",
      "enableMod": "Activer {name}"
    }
  }
}
```

Use `direction: "rtl"` for a right-to-left language. Each window sets HTML language/direction, but a complete Arabic/Hebrew translation and layout still needs its own visual review.

## Sections

| Section | Surface |
|---|---|
| `main` | Main window, controls, tooltips, SE status and Manager update notices |
| `import` | Workshop import window |
| `updates` | Simplified imported-Mod update window; other labels fall back to `import` |
| `launch` | Loading/progress window |
| `dialogs` | Native file-picker titles, startup failure and incompatible-component warnings |

Only recognized text keys are read. Missing keys or invalid placeholder substitutions use the bundled text; community languages fall back to English. Invalid JSON, mismatched language metadata, unreadable/oversized files or unavailable language files do not prevent the Manager from opening; the bundled language is used instead. Updated bundled defaults supply new keys even when an older customized file is retained.

Language strings are rendered as text, never HTML or executable code. Translations can still misrepresent warnings, so users should choose translations they trust. Mod names, authors and descriptions remain author-supplied content, and original diagnostic/OS errors can remain in their original language.

## Selection and persistence

- With no saved language, the Manager tries the complete system locale first, then progressively less specific tags. For example, `fr-CA` can use `fr.json`.
- Chinese systems without an exact community pack use bundled `zh-CN`; other unmatched systems use English.
- Existing users retain their saved selection. Installing a new translation or changing the Windows language does not silently switch an established selection.
- To choose an added language, close the Manager and edit **only** the `language` field of `%LOCALAPPDATA%\SCDE Mod Manager\manager-data\config.json`, for example `"language": "fr"`. To repeat system-language matching, use `"language": ""`.
- If a saved community pack disappears, the UI falls back to English without erasing the saved language, allowing the pack to work again when restored on a later launch.

Source checks are covered by `test/localization.test.js`. `tools/verify-localization-ui.js` uses an isolated synthetic community pack to exercise the main, import, update and launch windows; that fixture is not a supplied French translation.

## Other Mod authors: shared file contract and read interface

Each Mod owns a separate directory:

```text
BepInEx\config\
  modmanager\lang\en.json
  sc2-fog-of-war\lang\en.json
  sc2-keyboard-control\lang\en.json
```

`<Mod ID>` in `BepInEx\config\<Mod ID>\lang` is a placeholder for the actual package ID, not literal brackets or a double slash. For imported SE packages, use the installed Manager package ID, not a display name. The manager reserves `modmanager` for its own files.

- Use the same `{ language, name, direction, strings }` JSON structure. `strings` contains named sections and text keys. Keep section/key identifiers to letters, digits and underscores, beginning with a letter. An author's `en.json` defines known keys and English fallback text.
- The Manager-side API is `await manager.getModLocalization(modId)`; the sandboxed Manager UI can call `await window.scde.getModLocalization(modId)` through its fixed IPC endpoint. It accepts an installed Mod ID, never an arbitrary filesystem path. Results contain the resolved language and merged strings. Templates are bounded and links/junctions rejected. Invalid/missing English templates produce an empty dictionary.
- It checks only the requested Mod's `lang` directory and matches the system locale independently of whether the Manager itself has that community translation. Missing translations/keys use the author's English template. Reads are cached for that Manager process; reopen to load edits.
- This is **not** a game-side C# service, DLL patch or forced replacement of Mod text. Mod authors must implement the same read/fallback convention in their own projects. No existing Fog, Advanced Control or other gameplay Mod is changed by Manager 0.2.8.
- Packages may include first-use JSON defaults at `payload/BepInEx/config/<Mod ID>/lang/*.json`. Deployment seeds missing files only; reimporting, toggling or updating a Mod does not overwrite or remove player-owned language files. A full Manager-controlled stage rebuild preserves config files as described above.

The additional folder level has no game-frame cost. The Manager neither walks all Mod language directories at startup nor loads every translation file: it lists the relevant directory and reads the selected small dictionary (and the author's English template for an explicitly requested Mod). No additional localization runtime dependency was introduced.
