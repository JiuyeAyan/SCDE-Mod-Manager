const { contextBridge, ipcRenderer } = require("electron");
contextBridge.exposeInMainWorld("launch", {
  getState: () => ipcRenderer.invoke("launch:state"),
  close: () => ipcRenderer.invoke("launch:close"),
  onProgress: callback => ipcRenderer.on("launch:progress", (_event, state) => callback(state)),
});
