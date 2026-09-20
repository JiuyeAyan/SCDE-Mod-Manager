const path = require("node:path");
const fs = require("node:fs/promises");
const { app, BrowserWindow, dialog, ipcMain, shell } = require("electron");
const { ModManager } = require("./core/manager");
const { resolveGameSettingsPath } = require("./core/user-settings");
const { registerImportWindow } = require("./import-window");
const { registerLaunchWindow } = require("./launch-window");
const { SECoreUpdater } = require("./core/se-core-updater");
const { registerManagerUpdate } = require("./manager-update");
const { translate } = require("./i18n");

let manager;
let initialization;
let mainWindow;
let starting = true;
let startupFailed = false;
let startupLog;
let startupLanguage = "en";
let startupLocalization;

async function failStartup(error) {
  if (startupFailed) return;
  startupFailed = true;
  starting = false;
  const localization = startupLocalization || require(startupLanguage === "zh-CN" ? "./locales/zh-CN.json" : "./locales/en.json");
  const t = (key, values) => translate(localization, "dialogs", key, values);
  let detail = String(error?.stack || error);
  try {
    await fs.mkdir(path.dirname(startupLog), { recursive: true });
    await fs.appendFile(startupLog, `[${new Date().toISOString()}] ${detail}\n`, "utf8");
    detail += "\n\n" + t("logPath", { path: startupLog });
  } catch {
    detail += "\n\n" + t("logUnavailable");
  }
  try {
    const blocked = ["EPERM", "EACCES", "EBUSY"].includes(error?.code);
    const message = t("startupFailed") + (blocked ? t("startupBlocked") : "");
    await dialog.showMessageBox({
      type: "error", title: "SCDE Mod Manager",
      message,
      detail, buttons: [t("close")],
    });
  } finally { app.exit(1); }
}

function getSystemPackages() {
  const packageRoot = app.isPackaged
    ? path.join(process.resourcesPath, "system-mods")
    : path.join(__dirname, "..", "release");
  return [
    {
      id: "bepinex-runtime",
      version: "5.4.23.5",
      packagePath: path.join(packageRoot, "bepinex-runtime-5.4.23.5.scdemod"),
    },
    {
      id: "shcde-script-extender",
      version: "2.6.0+scdemm.1",
      packagePath: path.join(packageRoot, "shcde-script-extender-2.6.0+scdemm.1.scdemod"),
    },
    {
      id: "scde-multiplayer-compatibility",
      version: "0.3.1",
      packagePath: path.join(
        packageRoot,
        "scde-multiplayer-compatibility-0.3.1.scdemod"
      ),
    },
  ];
}

function createWindow() {
  const window = new BrowserWindow({
    width: 1440,
    height: 860,
    minWidth: 1040,
    minHeight: 680,
    backgroundColor: "#090a0a",
    icon: path.join(__dirname, "..", "ICON", "SCDEMM.ico"),
    show: true,
    autoHideMenuBar: true,
    titleBarStyle: "hidden",
    titleBarOverlay: {
      color: "#090a0a",
      symbolColor: "#d8d8d8",
      height: 48,
    },
    webPreferences: {
      preload: path.join(__dirname, "preload.js"),
      contextIsolation: true,
      nodeIntegration: false,
      sandbox: true,
    },
  });
  mainWindow = window;
  window.on("close", (event) => { if (starting || (manager?.mutationBusy && !manager.selfUpdateExiting)) event.preventDefault(); });
  window.once("closed", () => { if (mainWindow === window) mainWindow = null; });
  window.loadFile(path.join(__dirname, "renderer", "index.html")).then(() => {
    // Show explicitly after loading as well; visibility must not depend on ready-to-show.
    if (!window.isDestroyed()) window.show();
  }).catch(failStartup);
  window.webContents.setWindowOpenHandler(() => ({ action: "deny" }));
  window.webContents.on("will-navigate", (event) => event.preventDefault());
}

function registerHandlers() {
  const handle = (channel, handler) => ipcMain.handle(channel, async (...args) => {
    await initialization;
    return handler(...args);
  });
  const openImportWindow = registerImportWindow(manager);
  registerManagerUpdate(manager, { version: app.getVersion(), portableFile: app.isPackaged ? process.env.PORTABLE_EXECUTABLE_FILE : "", quit: () => app.quit() });
  handle("state:get", () => manager.getState());
  handle("localization:mod", (_event, modId) => manager.getModLocalization(modId));
  handle("game:auto-detect", () => manager.autoDetectGame());
  handle("game:choose", async () => {
    const localization = await manager.getLocalization();
    const result = await dialog.showOpenDialog({
      title: translate(localization, "dialogs", "chooseGame"),
      properties: ["openDirectory"],
    });
    if (result.canceled) return null;
    return manager.setGameDirectory(result.filePaths[0]);
  });
  handle("mod:choose-package", (event) => openImportWindow(BrowserWindow.fromWebContents(event.sender)));
  handle("mod:set-enabled", (_event, modId, enabled) =>
    manager.setEnabled(modId, enabled)
  );
  handle("mod:remove", (_event, modId) => manager.removeMod(modId));
  handle("mod:move", (_event, modId, direction) => manager.moveMod(modId, direction));
  handle("game:launch", registerLaunchWindow(manager));
  handle("folder:open", async (_event, kind) => {
    const target = kind === "mods" ? manager.modsRoot : kind === "config" && manager.stageDir ? path.join(manager.stageDir, "BepInEx/config") : "";
    if (!target) throw new Error("Invalid folder");
    const error = await shell.openPath(target);
    if (error) throw new Error(error);
    return true;
  });
}

if (!app.requestSingleInstanceLock()) {
  app.quit();
} else {
app.on("second-instance", () => {
  if (!mainWindow || mainWindow.isDestroyed()) return;
  if (mainWindow.isMinimized()) mainWindow.restore();
  mainWindow.show();
  mainWindow.focus();
});

app.whenReady().then(async () => {
  app.setAppUserModelId("com.scde.modmanager");
  const localDataRoot = process.env.LOCALAPPDATA || app.getPath("userData");
  const dataRoot = path.join(localDataRoot, "SCDE Mod Manager", "manager-data");
  startupLog = path.join(dataRoot, "startup-errors.log");
  startupLanguage = /^zh/i.test(app.getLocale()) ? "zh-CN" : "en";
  manager = new ModManager(dataRoot, {
    systemPackages: getSystemPackages(),
    gameSettingsPath: resolveGameSettingsPath(process.env),
    confirmEnableSE: async name => {
      const localization = await manager.getLocalization();
      const t = (key, values) => translate(localization, "dialogs", key, values);
      const result = await dialog.showMessageBox(mainWindow, {
        type: "question", title: "SCDE Mod Manager", message: t("enableSERequired", { name }),
        buttons: [t("cancel"), t("enableSEAndMod")], defaultId: 0, cancelId: 0, noLink: true,
      });
      return result.response === 1;
    },
    confirmSECompatibility: async ({ warnings, version, action }) => {
      const localization = await manager.getLocalization();
      const t = (key, values) => translate(localization, "dialogs", key, values);
      const launching = action === "launch";
      const details = warnings.map(item => `${item.packageName} / ${item.name}\n    SE ${item.minimumVersion || "*"} – ${item.maximumVersion || "*"}`).join("\n");
      const result = await dialog.showMessageBox(mainWindow, {
        type: "warning", title: "SCDE Mod Manager",
        message: t(launching ? "seLaunchBlocked" : "seSwitchWarning"),
        detail: t("seTarget", { version }) + "\n\n" + details + "\n\n" + t("seWarningHint"),
        buttons: launching ? [t("ok")] : [t("cancel"), t("switchAnyway")],
        defaultId: 0, cancelId: 0, noLink: true,
      });
      return !launching && result.response === 1;
    },
    onGameLockChanged: (locked) => {
      for (const window of BrowserWindow.getAllWindows()) {
        window.webContents.send("game:lock-changed", locked);
      }
    },
    onMutationLockChanged: (locked) => {
      for (const window of BrowserWindow.getAllWindows()) {
        window.webContents.send("manager:busy-changed", locked);
      }
    },
  });
  const seUpdater = new SECoreUpdater(manager, { helperRoot: app.isPackaged
    ? path.join(process.resourcesPath, "se-update-helper") : path.join(__dirname, "../release/se-update-helper") });
  initialization = (async () => {
    await manager.initialize();
    await seUpdater.initialize();
    await manager.restoreGameSession();
    startupLanguage = await manager.initializeLanguage(app.getLocale());
    startupLocalization = await manager.getLocalization();
    await manager.ensureSystemMods(true, true);
  })();
  registerHandlers();
  createWindow();
  await initialization;
  starting = false;

  app.on("activate", () => {
    if (BrowserWindow.getAllWindows().length === 0) createWindow();
  });
}).catch(failStartup);
}

app.on("window-all-closed", () => {
  if (process.platform !== "darwin") app.quit();
});
