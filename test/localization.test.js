const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const { Localizations, canonicalLanguage } = require("../src/core/localization");
const { translate } = require("../src/i18n");
const { ModManager } = require("../src/core/manager");
const en = require("../src/locales/en.json");

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-language-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const folder = path.join(root, "config/modmanager"); await fs.mkdir(folder, { recursive: true });
  const write = (tag, pack) => fs.writeFile(path.join(folder, tag + ".json"), JSON.stringify(pack));
  return { root, folder, write };
}

test("language packs seed English/Chinese once and preserve external edits across reopen", async t => {
  const { root, folder, write } = await fixture(t);
  const first = new Localizations(root); await first.initialize();
  assert.deepEqual((await fs.readdir(folder)).sort(), ["en.json", "zh-CN.json"]);
  await write("en", { language: "en", strings: { main: { importMods: "Custom import" } } });
  const reopened = new Localizations(root);
  const result = await reopened.get("en");
  assert.equal(result.strings.main.importMods, "Custom import");
  assert.equal(result.strings.main.launchWithMods, en.strings.main.launchWithMods);
  assert.equal(JSON.parse(await fs.readFile(path.join(folder, "en.json"))).strings.main.importMods, "Custom import");
  assert.equal(JSON.parse(await fs.readFile(path.join(folder, "en.json"))).strings.main.openConfigFolder, "Open config Folder", "new template keys are written for translators, not only supplied in memory");
});

test("new languages match system locale then remain selected across system changes", async t => {
  const { root, write } = await fixture(t);
  await write("fr", { language: "fr", strings: { main: { importMods: "Importer" } } });
  const manager = new ModManager(root);
  assert.equal(await manager.initializeLanguage("fr_CA"), "fr");
  assert.equal(await manager.initializeLanguage("zh-CN"), "fr");
  const state = await manager.getState();
  assert.equal(state.language, "fr");
  assert.equal(state.localization.strings.main.importMods, "Importer");
  assert.equal(state.localization.strings.import.cancel, "Cancel");
  assert.equal((await manager.configStore.load()).language, "fr");
});

test("exact locale wins over base language; unknown systems fall back to English", async t => {
  const { root, write } = await fixture(t);
  await write("pt", { language: "pt", strings: {} });
  await write("pt-BR", { language: "pt-BR", strings: {} });
  const catalog = new Localizations(root);
  assert.equal(await catalog.choose("pt-BR"), "pt-BR");
  assert.equal(await catalog.choose("pt-PT"), "pt");
  assert.equal(await catalog.choose("ko-KR"), "en");
  assert.equal(await catalog.choose("zh-Hant-TW"), "zh-CN");
  await write("zh-Hant", { language: "zh-Hant", strings: {} });
  assert.equal(await new Localizations(root).choose("zh-Hant-TW"), "zh-Hant");
  assert.equal(canonicalLanguage("../../outside"), "");
});

test("missing files and malformed translations fall back without losing the saved language", async t => {
  const { root, folder } = await fixture(t);
  await fs.writeFile(path.join(folder, "de.json"), "not valid JSON");
  const manager = new ModManager(root);
  await manager.configStore.update(c => ({ ...c, language: "de" }));
  assert.equal(await manager.initializeLanguage("zh-CN"), "de");
  assert.equal((await manager.getState()).language, "en");
  assert.ok(manager.localizations.errors.length);
  assert.equal((await manager.configStore.load()).language, "de");
  assert.equal((await manager.localizations.get("ja")).language, "en");
});

test("language placeholders are validated, text is literal and translated packs can opt into RTL", async t => {
  const { root, write } = await fixture(t);
  await write("ar", { language: "ar", direction: "rtl", strings: { main: {
    importMods: "<img src=x onerror=alert(1)>", imported: "Missing count", enableMod: "Enable {name}"
  } } });
  const result = await new Localizations(root).get("ar");
  assert.equal(result.direction, "rtl");
  assert.equal(result.strings.main.imported, en.strings.main.imported);
  assert.equal(translate(result, "main", "enableMod", { name: "Mod {count}", count: 3 }), "Enable Mod {count}");
  assert.equal(result.strings.main.importMods, "<img src=x onerror=alert(1)>");
});

test("oversized files fall back and the reader explicitly rejects symlinks", async t => {
  const { root, folder } = await fixture(t);
  await fs.writeFile(path.join(folder, "es.json"), " ".repeat(512 * 1024 + 1));
  const pack = await new Localizations(root).get("es");
  assert.equal(pack.language, "en");
  const source = await fs.readFile(path.resolve("src/core/localization.js"), "utf8");
  assert.match(source, /stat\.isSymbolicLink\(\)/);
});

test("bundled language keys and substitution placeholders match", () => {
  const zh = require("../src/locales/zh-CN.json");
  const tokens = text => [...new Set(text.match(/\{\w+\}/g) || [])].sort();
  for (const section of Object.keys(en.strings)) {
    assert.deepEqual(Object.keys(zh.strings[section]).sort(), Object.keys(en.strings[section]).sort());
    for (const key of Object.keys(en.strings[section])) assert.deepEqual(tokens(zh.strings[section][key]), tokens(en.strings[section][key]), `${section}.${key}`);
  }
});

test("manager languages migrate to the game copy and survive first preparation and rebuilding", async t => {
  const { root, write } = await fixture(t);
  await write("fr", { language: "fr", strings: { main: { importMods: "Saved translation" } } });
  const game = path.join(root, "original"); await fs.mkdir(game);
  await fs.writeFile(path.join(game, require("../src/core/constants").GAME_EXECUTABLE), "fake");
  const manager = new ModManager(root);
  await manager.initializeLanguage("fr-FR");
  await manager.setGameDirectory(game);
  const lang = path.join(manager.stageDir, "BepInEx/config/modmanager/lang");
  assert.equal(manager.localizations.folder, lang);
  assert.equal((await manager.getLocalization()).strings.main.importMods, "Saved translation");
  assert.equal(await fs.readFile(path.join(lang, "fr.json"), "utf8"), await fs.readFile(path.join(root, "config/modmanager/fr.json"), "utf8"));
  await fs.writeFile(path.join(lang, "fr.json"), JSON.stringify({ language: "fr", strings: { main: { importMods: "Player edit" } } }));
  await manager.prepareStage();
  await manager.prepareStage();
  const reopened = new ModManager(root); await reopened.initializeLanguage("en-US");
  assert.equal((await reopened.getLocalization()).strings.main.importMods, "Player edit");
  await assert.rejects(fs.access(path.join(game, "BepInEx")), { code: "ENOENT" });
  assert.ok(!(await fs.readdir(root)).some(name => name.startsWith("scdemm-config-backup-")));
});

test("language defaults never follow a game-copy config junction", async t => {
  const { root } = await fixture(t);
  const stage = path.join(root, "stage"), outside = path.join(root, "outside");
  await fs.mkdir(stage); await fs.mkdir(outside);
  await fs.symlink(outside, path.join(stage, "BepInEx"), "junction");
  const catalog = new Localizations(root, { folder: path.join(stage, "BepInEx/config/modmanager/lang"), safetyRoot: stage });
  assert.equal((await catalog.get("en")).language, "en");
  assert.ok(catalog.errors.length);
  assert.deepEqual(await fs.readdir(outside), []);
});

test("installed Mod language interface reads its own folder with system matching and English fallback", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(root);
  const game = path.join(root, "game"); await fs.mkdir(game);
  await fs.writeFile(path.join(game, require("../src/core/constants").GAME_EXECUTABLE), "fake");
  await manager.setGameDirectory(game); await manager.initializeLanguage("de-DE");
  const mod = path.join(manager.modsRoot, "sample"); await fs.mkdir(mod, { recursive: true });
  await fs.writeFile(path.join(mod, "manifest.json"), JSON.stringify({ id: "sample", name: "Sample", version: "1" }));
  const folder = path.join(manager.stageDir, "BepInEx/config/sample/lang"); await fs.mkdir(folder, { recursive: true });
  await fs.writeFile(path.join(folder, "en.json"), JSON.stringify({ language: "en", strings: { ui: { title: "Default", greeting: "Hello {name}" } } }));
  await fs.writeFile(path.join(folder, "de.json"), JSON.stringify({ language: "de", strings: { ui: { title: "Titel" } } }));
  const result = await manager.getModLocalization("sample");
  assert.equal(result.language, "de");
  assert.equal(result.strings.ui.title, "Titel");
  assert.equal(result.strings.ui.greeting, "Hello {name}");
  assert.equal((await manager.getLocalization()).language, "en");
  await assert.rejects(manager.getModLocalization("../sample"));
  await assert.rejects(manager.getModLocalization("not-installed"));
  assert.deepEqual((await fs.readdir(folder)).sort(), ["de.json", "en.json"]);
});

test("ordinary Mod language defaults are player-owned through redeploy, disable and rebuild", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(root);
  const game = path.join(root, "game"); await fs.mkdir(game);
  await fs.writeFile(path.join(game, require("../src/core/constants").GAME_EXECUTABLE), "fixture");
  await manager.setGameDirectory(game);
  const mod = path.join(manager.modsRoot, "example");
  const relative = "BepInEx/config/example/lang/en.json";
  await fs.mkdir(path.dirname(path.join(mod, "payload", relative)), { recursive: true });
  await fs.writeFile(path.join(mod, "manifest.json"), JSON.stringify({ id: "example", name: "Example", version: "1" }));
  await fs.writeFile(path.join(mod, "payload", relative), "default");
  await manager.setEnabled("example", true); await manager.prepareStage();
  const target = path.join(manager.stageDir, relative);
  assert.equal(await fs.readFile(target, "utf8"), "default");
  await fs.writeFile(target, "player translation");
  await fs.writeFile(path.join(mod, "payload", relative), "updated default");
  await manager.redeploy(); await manager.setEnabled("example", false); await manager.prepareStage();
  assert.equal(await fs.readFile(target, "utf8"), "player translation");
});
