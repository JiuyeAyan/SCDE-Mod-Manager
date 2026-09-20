const fs = require("node:fs/promises");
const path = require("node:path");
const { constants } = require("node:fs");
const { randomUUID } = require("node:crypto");
const english = require("../locales/en.json");
const chinese = require("../locales/zh-CN.json");
const bundled = { en: english, "zh-CN": chinese };

async function checkLanguageDirectory(root, folder) {
  const relative = path.relative(root, folder);
  if (relative.startsWith("..") || path.isAbsolute(relative)) throw new Error("Invalid language directory");
  let current = root;
  for (const part of ["", ...relative.split(path.sep).filter(Boolean)]) {
    current = path.join(current, part);
    try { const stat = await fs.lstat(current); if (!stat.isDirectory() || stat.isSymbolicLink()) throw new Error("Linked language directory is not supported"); }
    catch (error) { if (error.code !== "ENOENT") throw error; }
  }
}

function canonicalLanguage(value) {
  if (typeof value !== "string" || value.length > 80 || !/^[a-zA-Z0-9_-]+$/.test(value)) return "";
  try { return Intl.getCanonicalLocales(value.replaceAll("_", "-"))[0] || ""; } catch { return ""; }
}

const placeholders = text => [...new Set(text.match(/\{[a-zA-Z][a-zA-Z0-9]*\}/g) || [])].sort().join(",");
function mergePack(pack, fallback, language) {
  if (canonicalLanguage(pack?.language) !== language || !pack.strings || typeof pack.strings !== "object") throw new Error("Invalid language pack metadata");
  const strings = {};
  for (const [section, defaults] of Object.entries(fallback.strings)) {
    strings[section] = { ...defaults };
    for (const [key, template] of Object.entries(defaults)) {
      const translated = pack.strings[section]?.[key];
      if (typeof translated === "string" && translated.length <= 16000 && placeholders(translated) === placeholders(template)) strings[section][key] = translated;
    }
  }
  return { language, name: typeof pack.name === "string" ? pack.name : language,
    direction: pack.direction === "rtl" ? "rtl" : "ltr", strings };
}

class Localizations {
  constructor(dataRoot, options = {}) {
    this.folder = options.folder || path.join(dataRoot, "config", "modmanager");
    this.options = options;
    this.defaults = options.defaults || bundled;
    this.cache = new Map();
    this.errors = [];
  }

  initialize() {
    return this.initialization ||= (async () => {
      if (this.options.safetyRoot) await checkLanguageDirectory(this.options.safetyRoot, this.folder);
      if (!this.options.readOnly) await fs.mkdir(this.folder, { recursive: true });
      // One-way migration: the game-copy files become authoritative; never overwrite edits.
      if (this.options.legacyFolder) {
        let entries = [];
        try { entries = await fs.readdir(this.options.legacyFolder, { withFileTypes: true }); }
        catch (error) { if (error.code !== "ENOENT") throw error; }
        for (const entry of entries) {
          if (!entry.isFile() || !entry.name.endsWith(".json") || !canonicalLanguage(entry.name.slice(0, -5))) continue;
          const source = path.join(this.options.legacyFolder, entry.name);
          if ((await fs.stat(source)).size > 512 * 1024) continue;
          try { await fs.copyFile(source, path.join(this.folder, entry.name), constants.COPYFILE_EXCL); }
          catch (error) { if (error.code !== "EEXIST") throw error; }
        }
      }
      for (const language of this.options.readOnly ? [] : Object.keys(bundled)) {
        const target = path.join(this.folder, language + ".json");
        try { await fs.copyFile(path.join(__dirname, "../locales", language + ".json"), target, constants.COPYFILE_EXCL); }
        catch (error) { if (error.code !== "EEXIST") throw error; }
        // Keep author-editable English/Chinese templates complete after app upgrades.
        // Add keys only; invalid files and existing translations are not repaired/overwritten.
        const temporary = target + "." + randomUUID() + ".tmp";
        try {
          const stat = await fs.lstat(target);
          if (!stat.isFile() || stat.isSymbolicLink() || stat.size > 512 * 1024) continue;
          const pack = JSON.parse((await fs.readFile(target, "utf8")).replace(/^\uFEFF/, ""));
          if (pack.language !== language || !pack.strings || typeof pack.strings !== "object") continue;
          let changed = false;
          for (const [section, entries] of Object.entries(bundled[language].strings)) {
            if (!Object.hasOwn(pack.strings, section)) { pack.strings[section] = {}; changed = true; }
            if (!pack.strings[section] || typeof pack.strings[section] !== "object") continue;
            for (const [key, value] of Object.entries(entries)) {
              if (!Object.hasOwn(pack.strings[section], key)) { pack.strings[section][key] = value; changed = true; }
            }
          }
          if (changed) {
            await fs.writeFile(temporary, JSON.stringify(pack, null, 2) + "\n", { flag: "wx" });
            await fs.rename(temporary, target);
          }
        } catch (error) { this.errors.push(`${language}.json: ${error.message}`); }
        finally { await fs.rm(temporary, { force: true }).catch(() => {}); }
      }
      this.files = new Map(Object.keys(this.defaults).map(language => [language, language + ".json"]));
      for (const entry of await fs.readdir(this.folder, { withFileTypes: true })) {
        if (!entry.isFile() || !entry.name.toLowerCase().endsWith(".json")) continue;
        const language = canonicalLanguage(entry.name.slice(0, -5));
        if (language) this.files.set(language, entry.name);
      }
    })().catch(error => {
      this.files = new Map();
      this.errors.push(error.message);
      // An unreadable community language directory must not prevent recovery or launch.
    });
  }

  async choose(systemLocale) {
    await this.initialize();
    const language = canonicalLanguage(systemLocale);
    for (let candidate = language; candidate; candidate = candidate.includes("-") ? candidate.slice(0, candidate.lastIndexOf("-")) : "") {
      if (this.files.has(candidate)) return candidate;
    }
    const base = language.split("-")[0];
    return base === "zh" && this.files.has("zh-CN") ? "zh-CN" : "en";
  }

  async get(requested) {
    await this.initialize();
    const language = canonicalLanguage(requested) || "en";
    if (this.cache.has(language)) return this.cache.get(language);
    const fallback = this.defaults[language] || this.defaults.en;
    let result = fallback;
    const file = this.files.get(language);
    if (file) {
      try {
        if (this.options.safetyRoot) await checkLanguageDirectory(this.options.safetyRoot, this.folder);
        const target = path.join(this.folder, file);
        const stat = await fs.lstat(target);
        if (!stat.isFile() || stat.isSymbolicLink() || stat.size > 512 * 1024) throw new Error("Invalid or oversized language file");
        const pack = JSON.parse((await fs.readFile(target, "utf8")).replace(/^\uFEFF/, ""));
        result = mergePack(pack, fallback, language);
      } catch (error) { this.errors.push(`${file}: ${error.message}`); }
    }
    this.cache.set(language, result);
    return result;
  }
}

// Manager-side extension point only: no plugin injection and no recursive Mod scan.
async function createModLocalizations(stageDir, modId) {
  if (!/^[a-z0-9][a-z0-9._-]{0,127}$/i.test(modId) || modId.toLowerCase() === "modmanager") throw new Error("Invalid Mod language ID");
  const folder = path.join(stageDir, "BepInEx/config", modId, "lang");
  let defaults = { language: "en", name: modId, direction: "ltr", strings: {} };
  try {
    await checkLanguageDirectory(stageDir, folder);
    const target = path.join(folder, "en.json"), stat = await fs.lstat(target);
    if (!stat.isFile() || stat.isSymbolicLink() || stat.size > 512 * 1024) throw new Error("Invalid Mod language template");
    const pack = JSON.parse((await fs.readFile(target, "utf8")).replace(/^\uFEFF/, ""));
    if (pack.language !== "en" || !pack.strings || typeof pack.strings !== "object") throw new Error("Invalid Mod language template");
    for (const [section, entries] of Object.entries(pack.strings)) {
      if (!/^[a-zA-Z][a-zA-Z0-9_]*$/.test(section) || !entries || typeof entries !== "object") continue;
      defaults.strings[section] = Object.fromEntries(Object.entries(entries).filter(([key, value]) =>
        /^[a-zA-Z][a-zA-Z0-9_]*$/.test(key) && typeof value === "string" && value.length <= 16000));
    }
  } catch { /* Missing/invalid author templates yield an empty, non-executable dictionary. */ }
  return new Localizations(stageDir, { folder, safetyRoot: stageDir, readOnly: true, defaults: { en: defaults } });
}

module.exports = { Localizations, canonicalLanguage, mergePack, createModLocalizations };
