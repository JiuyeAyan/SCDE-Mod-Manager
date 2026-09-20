const { contextBridge, ipcRenderer } = require("electron");
contextBridge.exposeInMainWorld("modImport", {
  language: () => ipcRenderer.invoke("import:language"),
  mode: () => ipcRenderer.invoke("import:mode"),
  scan: () => ipcRenderer.invoke("import:scan"),
  manual: () => ipcRenderer.invoke("import:manual"),
  commit: (paths) => ipcRenderer.invoke("import:commit", paths),
  close: () => ipcRenderer.invoke("import:close"),
});
