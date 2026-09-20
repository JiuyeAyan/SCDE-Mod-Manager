const path = require("node:path");
const { BrowserWindow, dialog, ipcMain, shell } = require("electron");
const { detectSteamLibraries } = require("./core/detect-game");
const { readInstalledMods } = require("./core/mod-package");
const { readPackageMetadata, scanWorkshop } = require("./core/workshop");
const { findWorkshopUpdates } = require("./core/workshop-updates");
const { checkReleaseUpdates } = require("./core/release-updates");
const { translate } = require("./i18n");

function registerImportWindow(manager) {
  let session;
  let updateScan;
  let releaseCheck;
  let releaseResult = { updates: [], failures: [] };
  const releaseLinks = new Map();
  const validMainWindow = event => {
    const parent = BrowserWindow.fromWebContents(event.sender);
    if (!parent || event.sender.getURL() !== require("node:url").pathToFileURL(
      path.join(__dirname, "renderer", "index.html")).href) throw new Error("Invalid update window");
    return parent;
  };
  manager.seUpdater.onChanged = core => {
    releaseResult = { ...releaseResult, core };
    for (const window of BrowserWindow.getAllWindows()) {
      if (window.webContents.getURL() === require("node:url").pathToFileURL(path.join(__dirname, "renderer", "index.html")).href) {
        window.webContents.send("mod:se-update-status", releaseResult);
      }
    }
  };
  ipcMain.handle("mod:check-se-updates", (event) => {
    const parent = validMainWindow(event);
    if (!parent.isVisible()) return null;
    if (releaseCheck) return releaseCheck.then(() => releaseResult);
    const controller = new AbortController();
    const cancel = () => controller.abort();
    parent.once("closed", cancel);
    releaseCheck = (async () => {
      try {
        const result = await checkReleaseUpdates(await readInstalledMods(manager.modsRoot), { signal: controller.signal });
        for (const item of result.updates) releaseLinks.set(item.id, item.url);
        releaseResult = { ...result, core: manager.seUpdater.status() };
        releaseResult.core = await manager.seUpdater.update(result.updates.find(item => item.id === "shcde-script-extender"), controller.signal);
        return releaseResult;
      } catch (error) {
        if (!controller.signal.aborted) throw error;
        return null;
      } finally { if (!parent.isDestroyed()) parent.removeListener("closed", cancel); }
    })();
    return releaseCheck;
  });
  ipcMain.handle("mod:open-se-release", (_event, id) => {
    const url = releaseLinks.get(id);
    if (!url) throw new Error("Unknown release");
    return shell.openExternal(url);
  });
  const current = (event) => {
    if (!session || event.sender !== session.window.webContents) throw new Error("Invalid import window");
    return session;
  };
  const list = (active) => [...active.items.values()].map((item) => ({
    ...item, installedVersion: active.installed.get(item.id) || "",
  }));
  ipcMain.handle("import:scan", async (event) => {
    const active = current(event);
    if (active.busy) throw new Error("IMPORT_BUSY");
    if (active.initialUpdates) {
      const items = active.initialUpdates;
      active.initialUpdates = null;
      return { items, issues: [] };
    }
    active.busy = true;
    const controller = new AbortController();
    active.scan = controller;
    // Keep manually chosen files across refreshes; discard stale Workshop entries.
    for (const [key, item] of active.items) if (item.source === "workshop") active.items.delete(key);
    try {
      const config = await manager.configStore.load();
      active.installed = new Map((await readInstalledMods(manager.modsRoot)).map((mod) => [mod.id, mod.version]));
      const libraries = await detectSteamLibraries(config.gameDir);
      const result = await scanWorkshop(libraries, {
        signal: controller.signal,
        onItem: (item) => {
          if (!active.updatesOnly || findWorkshopUpdates(
            [...active.installed].map(([id, version]) => ({ id, version })), [item], manager.requiredSystemModIds
          ).length) active.items.set(item.path, { ...item, source: "workshop" });
        },
      });
      return { ...result, items: list(active), language: config.language || "en" };
    } catch (error) {
      if (!controller.signal.aborted) throw error;
      return { items: [], issues: [], roots: [], searchedRoots: [], count: 0 };
    } finally { active.scan = null; active.busy = false; }
  });
  ipcMain.handle("import:manual", async (event) => {
    const active = current(event);
    if (active.updatesOnly) throw new Error("Updates only");
    if (active.busy) throw new Error("IMPORT_BUSY");
    active.busy = true;
    const controller = new AbortController();
    active.scan = controller;
    try {
      const localization = await manager.getLocalization();
      const result = await dialog.showOpenDialog(active.window, {
        title: translate(localization, "dialogs", "chooseMods"),
        properties: ["openFile", "multiSelections"],
        filters: [{ name: translate(localization, "dialogs", "modFileType"), extensions: ["scdemod", "map", "semod"] }],
      });
      const issues = [];
      if (!result.canceled) for (const file of result.filePaths) {
        try {
          const item = await readPackageMetadata(file, controller.signal);
          if (!item) throw new Error("No installable Script Extender Mod in this file");
          active.items.set(file, { ...item, source: "manual" });
        } catch (error) {
          controller.signal.throwIfAborted();
          issues.push({ path: file, error: error.message });
        }
      }
      return { items: list(active), issues };
    } catch (error) {
      if (!controller.signal.aborted) throw error;
      return { items: [], issues: [] };
    } finally { active.scan = null; active.busy = false; }
  });
  ipcMain.handle("import:commit", async (event, paths) => {
    const active = current(event);
    if (active.busy) throw new Error("IMPORT_BUSY");
    manager.assertGameStopped();
    if (!Array.isArray(paths) || !paths.length || paths.some((file) => !active.items.has(file))) {
      throw new Error("INVALID_IMPORT_SELECTION");
    }
    const ids = paths.map((file) => active.items.get(file).id);
    if (new Set(ids).size !== ids.length) throw new Error("DUPLICATE_MOD_SELECTION");
    active.busy = active.importing = true;
    try {
      for (const file of paths) {
        const actual = await readPackageMetadata(file);
        const preview = active.items.get(file);
        if (!actual || actual.id !== preview.id || actual.version !== preview.version) throw new Error("MOD_CHANGED_RESCAN");
      }
      if (active.updatesOnly) {
        const installed = await readInstalledMods(manager.modsRoot);
        if (findWorkshopUpdates(installed, paths.map((file) => active.items.get(file)),
          manager.requiredSystemModIds).length !== paths.length) throw new Error("MOD_CHANGED_RESCAN");
      }
      active.result = await manager.installPackages(paths);
      active.importing = false;
      active.window.close();
      return true;
    } finally { active.busy = active.importing = false; }
  });
  ipcMain.handle("import:language", async (event) => {
    current(event);
    return manager.getLocalization();
  });
  ipcMain.handle("import:mode", (event) => current(event).updatesOnly ? "updates" : "import");
  ipcMain.handle("import:close", (event) => current(event).window.close());

  function openImportWindow(parent, updates = null) {
    manager.assertGameStopped();
    // An explicit import takes priority over the optional startup check.
    if (!updates) updateScan?.abort();
    if (session) {
      if (updates) return Promise.resolve(null);
      session.window.focus(); return session.promise;
    }
    const window = new BrowserWindow({
      parent, modal: true, show: false, width: 1120, height: 760,
      minWidth: 860, minHeight: 560, backgroundColor: "#090a0a",
      icon: path.join(__dirname, "..", "ICON", "SCDEMM.ico"),
      autoHideMenuBar: true, titleBarStyle: "hidden",
      titleBarOverlay: { color: "#090a0a", symbolColor: "#d8d8d8", height: 48 },
      webPreferences: { preload: path.join(__dirname, "import-preload.js"),
        sandbox: true, contextIsolation: true, nodeIntegration: false },
    });
    const active = { window, items: new Map(), installed: new Map(), result: null, busy: false };
    active.updatesOnly = updates !== null;
    active.initialUpdates = updates;
    for (const item of updates || []) {
      active.items.set(item.path, item);
      active.installed.set(item.id, item.installedVersion);
    }
    active.promise = new Promise((resolve, reject) => { active.resolve = resolve; active.reject = reject; });
    session = active;
    const preventDuringImport = (event) => { if (active.importing) event.preventDefault(); };
    parent.on("close", preventDuringImport);
    window.on("close", (event) => {
      preventDuringImport(event);
      if (!active.importing) active.closing = true;
    });
    window.once("closed", () => {
      active.scan?.abort();
      active.items.clear();
      parent.removeListener("close", preventDuringImport);
      session = null;
      active.resolve(active.result);
    });
    window.webContents.setWindowOpenHandler(() => ({ action: "deny" }));
    window.webContents.on("will-navigate", (event) => event.preventDefault());
    window.once("ready-to-show", () => window.show());
    const fail = (error) => {
      if (active.closing || window.isDestroyed()) return;
      active.reject(error);
      window.destroy();
    };
    window.webContents.once("render-process-gone", (_event, details) => fail(new Error(`Import window: ${details.reason}`)));
    window.loadFile(path.join(__dirname, "renderer", "import.html")).catch(fail);
    return active.promise;
  }
  const checkedWindows = new WeakSet();
  ipcMain.handle("mod:check-updates", async (event) => {
    const parent = BrowserWindow.fromWebContents(event.sender);
    if (!parent || event.sender.getURL() !== require("node:url").pathToFileURL(
      path.join(__dirname, "renderer", "index.html")).href || checkedWindows.has(parent)) return null;
    checkedWindows.add(parent);
    const controller = new AbortController();
    updateScan = controller;
    const cancel = () => controller.abort();
    parent.once("closed", cancel);
    try {
      // Renderer readiness alone does not mean the native window is visible yet.
      if (!parent.isVisible()) await new Promise((resolve) => {
        const ready = () => {
          parent.removeListener("show", ready);
          controller.signal.removeEventListener("abort", ready);
          resolve();
        };
        parent.once("show", ready);
        controller.signal.addEventListener("abort", ready, { once: true });
      });
      controller.signal.throwIfAborted();
      if (manager.gameLocked || session) return null;
      const config = await manager.configStore.load();
      const installed = await readInstalledMods(manager.modsRoot);
      if (!installed.some((mod) => !manager.requiredSystemModIds.has(mod.id))) return null;
      const candidates = [];
      await scanWorkshop(await detectSteamLibraries(config.gameDir), {
        signal: controller.signal, onItem: (item) => candidates.push(item),
      });
      const currentMods = await readInstalledMods(manager.modsRoot);
      const updates = findWorkshopUpdates(currentMods, candidates, manager.requiredSystemModIds);
      if (!updates.length || parent.isDestroyed() || manager.gameLocked || session) return null;
      return await openImportWindow(parent, updates);
    } catch (error) {
      if (!controller.signal.aborted) throw error;
      return null;
    } finally {
      if (updateScan === controller) updateScan = null;
      if (!parent.isDestroyed()) parent.removeListener("closed", cancel);
    }
  });
  return openImportWindow;
}

module.exports = { registerImportWindow };
