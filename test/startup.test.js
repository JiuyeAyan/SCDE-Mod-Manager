const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const vm = require("node:vm");
const { EventEmitter } = require("node:events");

test("portable launches use independent extraction directories", () => {
  // electron-builder 26.15.3 emits a shared build-level UNPACK_DIR_NAME unless this is true.
  assert.equal(require("../package.json").build.portable.unpackDirName, true);
});

async function startup({ primary = true, fail = false, language = "zh-CN", response = 0 } = {}) {
  const windows = [], errors = [], logs = [], folders = [], handlers = new Map();
  let finishInitialization;
  const initialized = new Promise(resolve => { finishInitialization = resolve; });
  const app = new EventEmitter();
  Object.assign(app, {
    isPackaged: false, whenReady: () => Promise.resolve(), requestSingleInstanceLock: () => primary,
    getLocale: () => "zh-CN", getVersion: () => require("../package.json").version, getPath: () => "test-data", setAppUserModelId() {},
    quit() { app.quitted = true; }, exit(code) { app.exitCode = code; },
  });
  class Window extends EventEmitter {
    constructor(options) {
      super(); this.visible = options.show !== false; this.webContents = new EventEmitter();
      Object.assign(this.webContents, { setWindowOpenHandler() {}, send() {} });
      windows.push(this);
    }
    // Deliberately never emit ready-to-show: startup must not rely only on first paint.
    loadFile() { return Promise.resolve(); }
    show() { this.visible = true; }
    focus() { this.focused = true; }
    isMinimized() { return Boolean(this.minimized); }
    restore() { this.minimized = false; }
    isDestroyed() { return false; }
    static getAllWindows() { return windows; }
  }
  class Manager {
    constructor(_root, options) { Manager.options = options; this.stageDir = path.resolve("test-game - SCDE Modded"); this.configStore = { load: async () => ({ language }) }; }
    async initialize() {}
    async restoreGameSession() {}
    async initializeLanguage() { return "zh-CN"; }
    async ensureSystemMods() {
      await initialized;
      if (fail) throw Object.assign(new Error("TEST_COMPONENT_UPGRADE_FAILED"), { code: "EPERM" });
    }
    async getState() { return { ready: true }; }
    async getLocalization() { return require(`../src/locales/${language}.json`); }
  }
  const electron = { app, BrowserWindow: Window, ipcMain: { handle: (key, handler) => handlers.set(key, handler) },
    dialog: { showMessageBox: async (...args) => { errors.push(args.at(-1)); return { response }; } }, shell: { openPath: async target => { folders.push(target); return ""; } } };
  const fakeFs = { mkdir: async () => {}, appendFile: async (_file, text) => logs.push(text) };
  const source = await fs.readFile(path.join(__dirname, "../src/main.js"), "utf8");
  vm.runInNewContext(source, {
    __dirname: path.resolve(__dirname, "../src"), process: { env: { LOCALAPPDATA: "test-data" }, platform: "win32" },
    require(name) {
      if (name === "electron") return electron;
      if (name === "node:path") return path;
      if (name === "node:fs/promises") return fakeFs;
      if (name === "./core/manager") return { ModManager: Manager };
      if (name === "./core/se-core-updater") return { SECoreUpdater: class { async initialize() {} } };
      if (name === "./core/user-settings") return { resolveGameSettingsPath: () => "" };
      if (name === "./import-window") return { registerImportWindow: () => () => {} };
      if (name === "./launch-window") return { registerLaunchWindow: () => () => {} };
      if (name === "./manager-update") return { registerManagerUpdate: () => {} };
      if (name === "./i18n") return require("../src/i18n");
      if (name.startsWith("./locales/")) return require("../src/" + name);
      throw new Error("Unexpected startup dependency: " + name);
    },
  });
  const flush = () => new Promise(resolve => setImmediate(resolve));
  await flush();
  return { app, windows, errors, logs, folders, handlers, finishInitialization, flush, options: Manager.options };
}

test("config-folder button opens game-copy config and SE enable confirmation is bilingual", async () => {
  for (const language of ["en", "zh-CN"]) {
    const run = await startup({ language, response: 1 }); run.finishInitialization(); await run.flush();
    await run.handlers.get("folder:open")(null, "config");
    assert.deepEqual(run.folders, [path.resolve("test-game - SCDE Modded/BepInEx/config")]);
    assert.equal(await run.options.confirmEnableSE("Dependent Pack"), true);
    assert.match(run.errors[0].message, /Dependent Pack/);
    assert.equal(run.errors[0].defaultId, 0);
    assert.equal(run.errors[0].cancelId, 0);
    assert.match(run.errors[0].buttons[1], language === "en" ? /Enable SE/ : /同时启用 SE/);
  }
});

test("bilingual incompatible-component launch warnings list every component and offer no override", async () => {
  for (const language of ["zh-CN", "en"]) {
    const run = await startup({ language, response: 1 });
    run.finishInitialization(); await run.flush();
    const warnings = [
      { packageName: "Pack A", name: "Feature A", minimumVersion: "2.7.1" },
      { packageName: "Pack B", name: "Feature B", maximumVersion: "2.5.0" },
    ];
    assert.equal(await run.options.confirmSECompatibility({ warnings, version: "2.6.0", action: "launch" }), false);
    const dialog = run.errors[0];
    assert.equal(dialog.buttons.length, 1);
    assert.equal(dialog.defaultId, 0); assert.equal(dialog.cancelId, 0);
    for (const text of ["Feature A", "Feature B", "2.7.1", "2.5.0", "2.6.0"]) assert.ok(dialog.detail.includes(text));
    assert.match(dialog.message, language === "en" ? /Launch blocked/ : /无法启动/);
    assert.equal(await run.options.confirmSECompatibility({ warnings, version: "2.6.0", action: "rollback" }), true);
    assert.equal(run.errors[1].buttons.length, 2, "version switching is distinct from launching the game");
  }
});

test("main window appears before component initialization and without a first-paint event", async () => {
  const run = await startup();
  assert.equal(run.windows.length, 1);
  assert.equal(run.windows[0].visible, true);
  let replied = false;
  const response = run.handlers.get("state:get")().then(() => { replied = true; });
  await run.flush();
  assert.equal(replied, false, "Operations must wait for component initialization");
  run.finishInitialization();
  await response;
  assert.equal(replied, true);
});

test("startup failure is recorded and shown rather than leaving a headless process", async () => {
  const run = await startup({ fail: true });
  run.finishInitialization();
  await run.flush();
  assert.equal(run.errors.length, 1);
  assert.match(run.errors[0].detail, /TEST_COMPONENT_UPGRADE_FAILED/);
  assert.match(run.errors[0].message, /文件资源管理器/);
  assert.match(run.logs.join(""), /TEST_COMPONENT_UPGRADE_FAILED/);
  assert.equal(run.app.exitCode, 1);
});

test("a second launch exits and a second-instance notification restores the existing window", async () => {
  const secondary = await startup({ primary: false });
  assert.equal(secondary.app.quitted, true);
  assert.equal(secondary.windows.length, 0);
  const primary = await startup();
  primary.windows[0].minimized = true;
  primary.app.emit("second-instance");
  assert.equal(primary.windows[0].visible, true);
  assert.equal(primary.windows[0].minimized, false);
  assert.equal(primary.windows[0].focused, true);
  primary.finishInitialization();
  await primary.flush();
});

test("a blocked system-component upgrade preserves the installed version and error code", async (t) => {
  const { ModManager } = require("../src/core/manager");
  const AdmZip = require("adm-zip");
  const root = await fs.mkdtemp(path.join(require("node:os").tmpdir(), "scde-startup-locked-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const packagePath = path.join(root, "component.scdemod");
  const writePackage = async (version) => {
    const archive = new AdmZip();
    archive.addFile("manifest.json", Buffer.from(JSON.stringify({ id: "test-component", name: "Component", version })));
    archive.addFile("payload/component.dll", Buffer.from(version));
    await fs.writeFile(packagePath, archive.toBuffer());
  };
  await writePackage("1.0.0");
  const manager = new ModManager(path.join(root, "data"), {
    systemPackages: [{ id: "test-component", version: "2.0.0", packagePath }],
  });
  await manager.initialize();
  await manager.installPackages([packagePath]);
  await writePackage("2.0.0");
  const target = path.join(manager.modsRoot, "test-component");
  const rename = fs.rename;
  const stub = t.mock.method(fs, "rename", async (from, to) => {
    if (from === target) throw Object.assign(new Error("EPERM: folder in use"), { code: "EPERM" });
    return rename(from, to);
  });
  await assert.rejects(() => manager.ensureSystemMods(false), { code: "EPERM" });
  assert.equal(JSON.parse(await fs.readFile(path.join(target, "manifest.json"), "utf8")).version, "1.0.0");
  assert.equal(await fs.readFile(path.join(target, "payload/component.dll"), "utf8"), "1.0.0");
  assert.deepEqual(await fs.readdir(manager.modsRoot), ["test-component"]);
  stub.mock.restore();
  await manager.ensureSystemMods(false);
  assert.equal(JSON.parse(await fs.readFile(path.join(target, "manifest.json"), "utf8")).version, "2.0.0");
});
