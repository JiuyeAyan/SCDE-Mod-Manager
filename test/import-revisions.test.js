const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const vm = require("node:vm");
const { EventEmitter } = require("node:events");
const { createRequire } = require("node:module");
const { pathToFileURL } = require("node:url");
const test = require("node:test");
const AdmZip = require("adm-zip");
const { installModPackage } = require("../src/core/mod-package");

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-import-revisions-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const file = path.join(root, "steamapps/workshop/content/3024040/123/mod.scdemod");
  const modsRoot = path.join(root, "mods");
  async function write(payload, id = "example.mod") {
    const zip = new AdmZip();
    zip.addFile("manifest.json", Buffer.from(JSON.stringify({ id, name: "Example", version: "1.0" })));
    zip.addFile("payload/example.txt", Buffer.from(payload));
    await fs.mkdir(path.dirname(file), { recursive: true });
    zip.writeZip(file);
  }
  await write("old");
  await installModPackage(file, modsRoot);
  await write("new");
  const handlers = new Map(), windows = [], installations = [];
  const source = path.resolve(__dirname, "../src/import-window.js");
  class FakeWindow extends EventEmitter {
    constructor() {
      super(); windows.push(this);
      this.webContents = new EventEmitter();
      this.webContents.window = this;
      this.webContents.getURL = () => this.url;
      this.webContents.setWindowOpenHandler = () => {};
      this.webContents.send = () => {};
    }
    static fromWebContents(sender) { return sender.window; }
    static getAllWindows() { return windows; }
    isVisible() { return true; }
    isDestroyed() { return this.destroyed || false; }
    show() {}
    focus() {}
    async loadFile(file) { this.url = pathToFileURL(file).href; }
    close() {
      let cancelled = false;
      this.emit("close", { preventDefault() { cancelled = true; } });
      if (!cancelled) { this.destroyed = true; this.emit("closed"); }
    }
    destroy() { this.close(); }
  }
  const manager = { modsRoot, seUpdater: {}, requiredSystemModIds: new Set(),
    configStore: { load: async () => ({ gameDir: root, language: "en" }) },
    assertGameStopped() {},
    getLocalization: async () => require("../src/locales/en.json"),
    installPackages: async (...args) => { installations.push(args); return true; } };
  const localRequire = createRequire(source);
  const context = { module: { exports: {} }, __dirname: path.dirname(source), AbortController,
    require: name => {
      if (name === "electron") return { BrowserWindow: FakeWindow, ipcMain: { handle: (key, callback) => handlers.set(key, callback) }, dialog: {}, shell: {} };
      if (name === "./core/detect-game") return { detectSteamLibraries: async () => [root] };
      return localRequire(name);
    } };
  vm.runInNewContext(await fs.readFile(source, "utf8"), context);
  const open = context.module.exports.registerImportWindow(manager);
  const parent = new FakeWindow();
  await parent.loadFile(path.resolve(__dirname, "../src/renderer/index.html"));
  return { root, file, modsRoot, manager, parent, windows, handlers, installations, open, write,
    event: () => ({ sender: windows.at(-1).webContents }) };
}

test("import scanning keeps digests, recognizes same-version revisions, and rejects changed preview bytes", async t => {
  const run = await fixture(t);
  const closed = run.open(run.parent);
  const result = await run.handlers.get("import:scan")(run.event());
  assert.equal(result.items.length, 1);
  assert.equal(result.items[0].installedVersion, "1.0");
  assert.equal(result.items[0].updateReason, "content-changed");
  assert.match(result.items[0].packageSha256, /^[a-f0-9]{64}$/);
  assert.equal(run.installations.length, 0, "listing a changed archive never installs it");
  await run.write("changed again after preview");
  await assert.rejects(run.handlers.get("import:commit")(run.event(), [run.file]), /MOD_CHANGED_RESCAN/);
  assert.equal(run.installations.length, 0);
  run.windows.at(-1).close(); await closed;
});

test("startup reminders include same-version revisions only after explicit update selection", async t => {
  const run = await fixture(t);
  const check = run.handlers.get("mod:check-updates")({ sender: run.parent.webContents });
  for (let count = 0; count < 100 && run.windows.length < 2; count++) await new Promise(resolve => setTimeout(resolve, 5));
  assert.equal(run.windows.length, 2, "startup scan should open the update preview for changed bytes");
  const result = await run.handlers.get("import:scan")(run.event());
  assert.equal(result.items[0].updateReason, "content-changed");
  assert.equal(run.installations.length, 0);
  await run.handlers.get("import:commit")(run.event(), [run.file]);
  assert.equal(run.installations.length, 1);
  assert.equal(run.installations[0][1].expectedDigests[path.resolve(run.file)], result.items[0].packageSha256,
    "the manager receives the consent digest for validation against actual imported bytes before swapping");
  assert.deepEqual(Object.keys(run.installations[0][1].expectedDigests), [path.resolve(run.file)]);
  await check;
});

test("startup revisions exclude required system components", async t => {
  const run = await fixture(t);
  run.manager.requiredSystemModIds.add("example.mod");
  assert.equal(await run.handlers.get("mod:check-updates")({ sender: run.parent.webContents }), null);
  assert.equal(run.windows.length, 1);
  assert.equal(run.installations.length, 0);
});

test("update preview labels same-version content changes in both languages without preselecting them", async () => {
  const source = await fs.readFile(path.resolve(__dirname, "../src/renderer/import.js"), "utf8");
  for (const language of ["en", "zh-CN"]) {
    const localization = require(`../src/locales/${language}.json`);
    const elements = new Map();
    const element = () => ({ children: [], attributes: {}, value: "", textContent: "", dataset: {},
      classList: { add() {}, toggle() {} }, addEventListener() {},
      setAttribute(name, value) { this.attributes[name] = value; },
      append(...items) { this.children.push(...items); }, replaceChildren(...items) { this.children = items; } });
    const byId = id => { if (!elements.has(id)) elements.set(id, element()); return elements.get(id); };
    const document = { getElementById: byId, createElement: element, documentElement: {}, body: element(),
      querySelector: byId, querySelectorAll: () => [] };
    const candidate = { id: "example.mod", name: "Example", version: "1.0", installedVersion: "1.0",
      path: "example.scdemod", author: "Example Author", description: "Description", updateReason: "content-changed" };
    let commits = 0;
    const window = { addEventListener() {}, modImport: {
      language: async () => localization, mode: async () => "updates",
      scan: async () => ({ items: [candidate], issues: [] }), commit: async () => { commits++; },
    } };
    vm.runInNewContext(source, { document, window, SCDEI18n: require("../src/i18n"), setTimeout, clearTimeout });
    await new Promise(resolve => setImmediate(resolve));
    const row = byId("candidates").children[0];
    assert.ok(row, `${language}: candidate rendered`);
    assert.equal(row.children[0].checked, false, "updates require an explicit player selection");
    assert.equal(byId("confirm").disabled, true);
    assert.ok(row.children[3].textContent.startsWith(localization.strings.import.contentChanged));
    assert.ok(row.children[0].attributes["aria-label"].includes(localization.strings.import.contentChanged));
    assert.equal(commits, 0);
  }
});
