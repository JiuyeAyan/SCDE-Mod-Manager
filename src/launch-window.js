const path = require("node:path");
const { BrowserWindow, ipcMain } = require("electron");
const { LaunchProgress } = require("./core/launch-progress");

function registerLaunchWindow(manager) {
  let window;
  let progress;
  let snapshot;
  let language = "en";
  let localization;
  let launching = false;
  const current = event => {
    if (!window || event.sender !== window.webContents) throw new Error("Invalid launch window");
  };
  ipcMain.handle("launch:state", event => { current(event); return { ...snapshot, language, localization }; });
  ipcMain.handle("launch:close", event => { current(event); window.close(); });

  manager.onGameStarting = async ({ stageDir, launchId }) => {
    if (progress && !progress.stopped) await progress.watch(stageDir, launchId);
  };
  manager.onGameExited = () => progress?.finish("exited");

  return async () => {
    if (launching) throw new Error("GAME_RUNNING");
    manager.assertGameStopped();
    launching = true;
    try {
      progress?.stop();
      if (window && !window.isDestroyed()) window.close();
      localization = await manager.getLocalization();
      language = localization.language;
      progress = new LaunchProgress(next => {
        snapshot = next;
        if (!window || window.isDestroyed()) return;
        if (next.phase === "ready") window.close();
        else window.webContents.send("launch:progress", { ...next, language });
      });
      window = new BrowserWindow({
        width: 680, height: 560, minWidth: 540, minHeight: 400,
        backgroundColor: "#090a0a", autoHideMenuBar: true,
        icon: path.join(__dirname, "..", "ICON", "SCDEMM.ico"),
        title: "SCDE Mod Manager", titleBarStyle: "hidden",
        titleBarOverlay: { color: "#090a0a", symbolColor: "#d8d8d8", height: 48 },
        webPreferences: { preload: path.join(__dirname, "launch-preload.js"), contextIsolation: true, nodeIntegration: false, sandbox: true },
      });
      const activeWindow = window;
      const activeProgress = progress;
      activeWindow.on("closed", () => {
        activeProgress.stop();
        if (window === activeWindow) window = null;
      });
      activeWindow.webContents.setWindowOpenHandler(() => ({ action: "deny" }));
      activeWindow.webContents.on("will-navigate", event => event.preventDefault());
      activeWindow.loadFile(path.join(__dirname, "renderer", "launch.html")).catch(() => {
        if (!activeWindow.isDestroyed()) activeWindow.close();
      });
      try {
        const result = await manager.launch();
        if (!result) {
          activeProgress.stop();
          if (!activeWindow.isDestroyed()) activeWindow.close();
        }
        return result;
      }
      catch (error) {
        activeProgress.stop();
        if (!activeWindow.isDestroyed()) activeWindow.close();
        throw error;
      }
    } finally { launching = false; }
  };
}

module.exports = { registerLaunchWindow };
