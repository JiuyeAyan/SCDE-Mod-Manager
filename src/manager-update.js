const fs = require("node:fs/promises");
const path = require("node:path");
const { ipcMain } = require("electron");
const { checkManagerUpdate, prepareManagerUpdate, startManagerUpdate } = require("./core/manager-updates");

function registerManagerUpdate(manager, options) {
  let checking;
  let update;
  ipcMain.handle("manager:check-update", () => {
    if (!checking) checking = (async () => {
      const config = await manager.configStore.load();
      update = await checkManagerUpdate(options.version, config.gameDir, options.checkOptions);
      // Do not expose a caller-selectable replacement path through IPC.
      return update ? { version: update.version, currentVersion: options.version, portable: !!options.portableFile } : null;
    })();
    return checking;
  });
  ipcMain.handle("manager:install-update", () => manager.withModMutation(async () => {
    if (!update) throw new Error("MANAGER_UPDATE_NOT_FOUND");
    const job = await prepareManagerUpdate(update, options.portableFile);
    await startManagerUpdate(job);
    await fs.writeFile(path.join(job, "commit"), "confirmed", "utf8");
    // Keep launch and SE update commits blocked through the shutdown boundary.
    manager.selfUpdateExiting = true;
    setImmediate(options.quit);
    return true;
  }));
}

module.exports = { registerManagerUpdate };
