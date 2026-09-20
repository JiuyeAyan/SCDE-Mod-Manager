const { contextBridge, ipcRenderer } = require("electron");

contextBridge.exposeInMainWorld("scde", {
  getState: () => ipcRenderer.invoke("state:get"),
  getModLocalization: (modId) => ipcRenderer.invoke("localization:mod", modId),
  checkManagerUpdate: () => ipcRenderer.invoke("manager:check-update"),
  installManagerUpdate: () => ipcRenderer.invoke("manager:install-update"),
  checkWorkshopUpdates: () => ipcRenderer.invoke("mod:check-updates"),
  checkSEUpdates: () => ipcRenderer.invoke("mod:check-se-updates"),
  openSERelease: (id) => ipcRenderer.invoke("mod:open-se-release", id),
  onSEUpdateStatus: (callback) => ipcRenderer.on("mod:se-update-status", (_event, result) => callback(result)),
  onMutationLockChanged: (callback) => ipcRenderer.on("manager:busy-changed", (_event, locked) => callback(locked)),
  onGameLockChanged: (callback) => {
    ipcRenderer.on("game:lock-changed", (_event, locked) => callback(locked));
  },
  autoDetectGame: () => ipcRenderer.invoke("game:auto-detect"),
  chooseGame: () => ipcRenderer.invoke("game:choose"),
  chooseModPackages: () => ipcRenderer.invoke("mod:choose-package"),
  setModEnabled: (modId, enabled) => ipcRenderer.invoke("mod:set-enabled", modId, enabled),
  removeMod: (modId) => ipcRenderer.invoke("mod:remove", modId),
  moveMod: (modId, direction) => ipcRenderer.invoke("mod:move", modId, direction),
  launchGame: () => ipcRenderer.invoke("game:launch"),
  openFolder: (kind) => ipcRenderer.invoke("folder:open", kind),
});
