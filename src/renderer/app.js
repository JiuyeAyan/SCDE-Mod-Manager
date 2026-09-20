let localization;

const elements = {
  appTitle: document.querySelector("#app-title"),
  gamePath: document.querySelector("#game-path"),
  gameStatus: document.querySelector("#game-status"),
  detectGame: document.querySelector("#detect-game"),
  chooseGame: document.querySelector("#choose-game"),
  importMod: document.querySelector("#import-mod"),
  launchGame: document.querySelector("#launch-game"),
  openMods: document.querySelector("#open-mods"),
  openStage: document.querySelector("#open-stage"),
  stageLabel: document.querySelector("#stage-label"),
  conflictCount: document.querySelector("#conflict-count"),
  conflictPanel: document.querySelector("#conflict-panel"),
  conflictSummary: document.querySelector("#conflict-summary"),
  modList: document.querySelector("#mod-list"),
  systemComponents: document.querySelector("#system-components"),
  busy: document.querySelector("#busy"),
  busyDetail: document.querySelector("#busy-detail"),
  toast: document.querySelector("#toast"),
};

let language = "en";
let state;
let toastTimer;
let importPending = false;
let managerUpdate;

function t(key, values = {}) {
  return SCDEI18n.translate(localization, "main", key, values);
}

function applyLanguage(nextLanguage) {
  localization = state.localization;
  language = nextLanguage || "en";
  document.documentElement.lang = language;
  document.documentElement.dir = localization.direction;
  for (const element of document.querySelectorAll("[data-i18n]")) {
    element.textContent = t(element.dataset.i18n);
  }
}

function showBusy(messageKey) {
  elements.busyDetail.textContent = `${t(messageKey)} ${t("doNotClose")}`;
  elements.busy.classList.remove("hidden");
}

function hideBusy() {
  elements.busy.classList.add("hidden");
}

function showToast(message, isError = false) {
  clearTimeout(toastTimer);
  elements.toast.textContent = message;
  elements.toast.className = `toast${isError ? " error" : ""}`;
  toastTimer = setTimeout(() => elements.toast.classList.add("hidden"), 5200);
}

function readableError(error) {
  const raw = String(error?.message || error).replace(
    /^Error invoking remote method '[^']+': Error: /,
    ""
  );
  if (raw.includes("STEAM_NOT_RUNNING")) return t("steamNotRunning");
  if (raw.includes("GAME_RUNNING")) return t("gameRunning");
  if (raw.includes("SE_REQUIRED_DISABLED")) return t("seRequiredDisabled");
  if (raw.includes("MANAGER_BUSY")) return t("managerBusy");
  if (raw.includes("MANAGER_UPDATE") || raw.includes("manager:install-update")) return t("managerUpdateFailed");
  if (raw.includes("SE_UPDATE_RECOVERY_REQUIRED")) return t("seRecovery");
  if (raw.includes("SE_UPDATE_BUSY_OR_NO_BACKUP")) return t("seUnavailable");
  if (language === "zh-CN") return raw;
  if (raw.includes("没有在 Steam 库")) return t("noGameFound");
  if (raw.includes("不是游戏目录") || raw.includes("不存在")) return t("invalidGameFolder");
  if (
    raw.includes("缺少依赖") ||
    raw.includes("依赖形成循环") ||
    (raw.includes("需要") && raw.includes("当前安装"))
  ) {
    return t("dependencyError");
  }
  return t("genericError");
}

function emptyState() {
  const wrapper = document.createElement("div");
  wrapper.className = "empty-state";
  const title = document.createElement("strong");
  const hint = document.createElement("span");
  title.textContent = t("noMods");
  hint.textContent = t("noModsHint");
  wrapper.append(title, hint);
  return wrapper;
}

function createModRow(mod, index, mods) {
  const row = document.createElement("article");
  row.className = "mod-row";

  const nameCell = document.createElement("div");
  nameCell.className = "mod-name-cell";
  const nameLine = document.createElement("div");
  nameLine.className = "mod-name-line";
  const name = document.createElement("strong");
  name.textContent = mod.name;
  const version = document.createElement("span");
  version.className = "mod-version";
  version.textContent = `v${mod.version}`;
  nameLine.append(name, version);
  nameCell.append(nameLine);

  const author = document.createElement("div");
  author.className = "mod-author";
  author.textContent = mod.author || t("unknownAuthor");

  const description = document.createElement("div");
  description.className = "mod-description-cell";
  description.textContent = mod.description || t("noDescription");

  const action = document.createElement("div");
  action.className = "row-action";
  const remove = document.createElement("button");
  remove.className = "remove-button";
  remove.textContent = t("remove");
  remove.disabled = state.gameLocked || state.mutationBusy || mod.required;
  if (mod.required) remove.title = t("systemComponent");
  remove.addEventListener("click", async () => {
    if (!window.confirm(t("removeConfirm", { name: mod.name }))) return;
    await runAction("busyRemove", () => window.scde.removeMod(mod.id), "modRemoved");
  });
  action.append(remove);

  const toggleCell = document.createElement("div");
  toggleCell.className = "toggle-cell";
  const label = document.createElement("label");
  label.className = "switch";
  const toggle = document.createElement("input");
  toggle.type = "checkbox";
  toggle.checked = mod.enabled;
  toggle.disabled = state.gameLocked || state.mutationBusy || mod.required;
  if (mod.required) label.title = t("systemComponent");
  toggle.setAttribute(
    "aria-label",
    t(mod.enabled ? "disableMod" : "enableMod", { name: mod.name })
  );
  const track = document.createElement("span");
  track.className = "switch-track";
  toggle.addEventListener("change", async () => {
    toggle.disabled = true;
    const result = await runAction(
      mod.enabled ? "busyDisable" : "busyEnable",
      () => window.scde.setModEnabled(mod.id, toggle.checked),
      toggle.checked ? "modEnabled" : "modDisabled"
    );
    if (!result) render(await window.scde.getState());
  });
  label.append(toggle, track);
  toggleCell.append(label);

  const orderCell = document.createElement("div");
  orderCell.className = "order-cell";
  const moveUp = document.createElement("button");
  moveUp.className = "order-button";
  moveUp.type = "button";
  moveUp.textContent = "↑";
  moveUp.title = t("moveUp", { name: mod.name });
  moveUp.setAttribute("aria-label", moveUp.title);
  moveUp.disabled = state.gameLocked || state.mutationBusy || mod.required || index === 0 || mods[index - 1]?.required;
  moveUp.addEventListener("click", () =>
    runAction("busyReorder", () => window.scde.moveMod(mod.id, -1), "modReordered")
  );

  const moveDown = document.createElement("button");
  moveDown.className = "order-button";
  moveDown.type = "button";
  moveDown.textContent = "↓";
  moveDown.title = t("moveDown", { name: mod.name });
  moveDown.setAttribute("aria-label", moveDown.title);
  moveDown.disabled = state.gameLocked || state.mutationBusy || mod.required || index === mods.length - 1;
  moveDown.addEventListener("click", () =>
    runAction("busyReorder", () => window.scde.moveMod(mod.id, 1), "modReordered")
  );
  orderCell.append(moveUp, moveDown);

  row.append(nameCell, author, description, action, toggleCell, orderCell);
  return row;
}

function render(nextState) {
  state = nextState;
  applyLanguage(state.language);
  elements.appTitle.textContent = `SCDE Mod Manager v${state.appVersion}`;
  document.title = elements.appTitle.textContent;
  document.querySelector("#manager-update-install").disabled = state.gameLocked || state.mutationBusy || !managerUpdate?.portable;

  elements.gamePath.textContent = state.gameDir || t("notSelected");
  elements.gamePath.title = state.gameDir || t("notSelected");
  elements.gameStatus.classList.toggle("valid", state.gameValid);
  elements.conflictCount.textContent = state.lastConflicts.length;
  elements.launchGame.disabled = state.gameLocked || state.mutationBusy || !state.gameValid;
  elements.launchGame.title = state.gameLocked ? t("gameRunning") : "";
  elements.importMod.disabled = state.gameLocked || state.mutationBusy || importPending;
  elements.detectGame.disabled = state.gameLocked || state.mutationBusy;
  elements.chooseGame.disabled = state.gameLocked || state.mutationBusy;
  elements.openStage.disabled = !state.gameValid;

  elements.stageLabel.textContent = state.stageReady ? t("ready") : t("notReady");

  const userMods = state.mods.filter((mod) => !mod.required);
  elements.systemComponents.replaceChildren(...state.mods.filter((mod) => mod.required).map((mod) => {
    const card = document.createElement("div");
    card.className = "system-component";
    const title = document.createElement("strong");
    title.textContent = `${mod.name} v${mod.version}`;
    const author = document.createElement("small");
    author.textContent = mod.author || t("unknownAuthor");
    card.append(title, author);
    if (mod.id === "shcde-script-extender") {
      card.title = t("seBuildHint");
      const line = document.createElement("div"); line.className = "system-component-line";
      const label = document.createElement("label"); label.className = "switch se-switch";
      const toggle = document.createElement("input"); toggle.type = "checkbox"; toggle.id = "toggle-se";
      toggle.checked = mod.enabled; toggle.disabled = state.gameLocked || state.mutationBusy;
      toggle.setAttribute("aria-label", t(mod.enabled ? "disableMod" : "enableMod", { name: "Script Extender" }));
      label.title = toggle.getAttribute("aria-label");
      const track = document.createElement("span"); track.className = "switch-track";
      label.append(toggle, track); line.append(title, label); card.prepend(line);
      toggle.onchange = async () => {
        toggle.disabled = true;
        const result = await runAction(toggle.checked ? "busyEnable" : "busyDisable", () => window.scde.setModEnabled(mod.id, toggle.checked));
        if (result?.disabledDependents?.length) window.alert(t("seCascadeDisabled", { names: result.disabledDependents.join("\n") }));
        if (!result) render(await window.scde.getState());
      };
    }
    return card;
  }));
  elements.modList.replaceChildren(
    ...(userMods.length
      ? userMods.map((mod, index) => createModRow(mod, index, userMods))
      : [emptyState()])
  );

  if (state.lastConflicts.length) {
    const first = state.lastConflicts[0];
    const extra =
      state.lastConflicts.length > 1
        ? t("conflictExtra", { count: state.lastConflicts.length - 1 })
        : "";
    elements.conflictSummary.textContent = t("conflictSummary", {
      path: first.path,
      winner: first.winner,
      overwritten: first.overwritten,
      extra,
    });
    elements.conflictPanel.classList.remove("hidden");
  } else {
    elements.conflictPanel.classList.add("hidden");
  }
}

async function runAction(busyKey, action, successKey) {
  if (busyKey) showBusy(busyKey);
  try {
    const result = await action();
    if (result?.mods) render(result);
    if (result?.state?.mods) render(result.state);
    if (successKey && result !== null) showToast(t(successKey));
    return result;
  } catch (error) {
    showToast(readableError(error), true);
    return null;
  } finally {
    if (busyKey) hideBusy();
  }
}

elements.detectGame.addEventListener("click", () =>
  runAction("busyDetect", () => window.scde.autoDetectGame(), "detectedGame")
);

elements.chooseGame.addEventListener("click", () =>
  runAction("busyChoose", () => window.scde.chooseGame(), "gameDirectorySet")
);

elements.importMod.addEventListener("click", async () => {
  if (importPending || state.gameLocked) return;
  importPending = true;
  elements.importMod.disabled = true;
  try {
    const result = await runAction(null, () => window.scde.chooseModPackages());
    showImportResult(result);
  } finally {
    importPending = false;
    elements.importMod.disabled = state.gameLocked;
  }
});

function showImportResult(result) {
  if (!result) return;

  const imported = result.imported.length;
  const failed = result.failures.length;
  const details = result.failures
    .map((failure) => `${failure.file}: ${readableError(failure.error)}`)
    .join("; ");
  if (failed === 0) {
    showToast(t("imported", { count: imported }));
  } else if (imported > 0) {
    showToast(t("importPartial", { success: imported, failed, details }), true);
  } else {
    showToast(t("importFailed", { count: failed, details }), true);
  }
  if (result.activationFailures?.length) {
    const activationDetails = result.activationFailures
      .map((failure) => `${failure.name}: ${readableError(failure.error)}`).join("; ");
    showToast(`${elements.toast.textContent}\n${t("activationFailed", { details: activationDetails })}`, true);
  }
}

elements.launchGame.addEventListener("click", async () => {
  if (state.gameLocked) return;
  render({ ...state, gameLocked: true });
  const result = await runAction("busyLaunch", () => window.scde.launchGame());
  if (!result) {
    try {
      render(await window.scde.getState());
    } catch (error) {
      showToast(readableError(error), true);
    }
  }
  if (result) showToast(t("gameLaunched"));
});

window.scde.onGameLockChanged((locked) => {
  if (state) render({ ...state, gameLocked: locked });
});
window.scde.onMutationLockChanged((locked) => {
  if (state) render({ ...state, mutationBusy: locked });
});
window.scde.onSEUpdateStatus((result) => {
  showSEUpdates(result);
  window.scde.getState().then(render).catch(error => showToast(readableError(error), true));
});

elements.openMods.addEventListener("click", () => {
  window.scde.openFolder("mods").catch((error) => showToast(readableError(error), true));
});

elements.openStage.addEventListener("click", () => {
  window.scde.openFolder("config").catch((error) => showToast(readableError(error), true));
});

const workspace = document.querySelector(".workspace");
function showManagerUpdate(update) {
  if (!update) return;
  managerUpdate = update;
  document.querySelector("#manager-update-title").textContent = t("managerUpdateTitle", { current: update.currentVersion, version: update.version });
  document.querySelector("#manager-update-hint").textContent = t("managerUpdateHint");
  if (!update.portable) document.querySelector("#manager-update-hint").append(t("managerUpdateDevelopment"));
  document.querySelector("#manager-update-later").textContent = t("updateLater");
  document.querySelector("#manager-update-install").textContent = t("managerUpdateInstall");
  document.querySelector("#manager-update-install").disabled = state.gameLocked || state.mutationBusy || !update.portable;
  document.querySelector("#manager-update").classList.remove("hidden");
}
document.querySelector("#manager-update-later").onclick = () => document.querySelector("#manager-update").classList.add("hidden");
document.querySelector("#manager-update-install").onclick = async () => {
  if (state.gameLocked || state.mutationBusy) return;
  showBusy("busyRead");
  try { await window.scde.installManagerUpdate(); }
  catch (error) { hideBusy(); showToast(readableError(error), true); }
};
function showSEUpdates(result) {
  if (!result || (!result.updates.length && !result.failures.length && !result.core?.status)) return;
  document.querySelector("#release-notice-title").textContent = t("seUpdateTitle");
  document.querySelector("#release-notice-close").textContent = t("close");
  document.querySelector("#release-notice-hint").textContent = t("seUpdateHint");
  const rows = [];
  if (result.core?.status) {
    const labels = {
      downloading: "seDownloading", pending: "sePending", updated: "seUpdatedAutomatic",
      "rolled-back": "seRolledBack", failed: "seFailed",
    };
    const row = document.createElement("div"); row.className = "release-notice-item";
    row.textContent = `Script Extender ${result.core.version || ""}: ${t(labels[result.core.status] || result.core.status)}`;
    if (result.core.error) row.append(" — " + result.core.error);
    rows.push(row);
  }
  for (const item of result.updates) {
    const row = document.createElement("div"); row.className = "release-notice-item";
    row.textContent = `${item.name}：v${item.installedVersion} → v${item.version}`;
    const button = document.createElement("button"); button.className = "button compact";
    button.textContent = t("seReleasePage");
    button.onclick = () => window.scde.openSERelease(item.id).catch(error => showToast(readableError(error), true));
    row.append(button); rows.push(row);
  }
  for (const item of result.failures) {
    const row = document.createElement("div"); row.className = "release-notice-item";
    row.textContent = item.name + ": " + t(item.reason === "network" ? "seNetworkFailure" : "seInvalidRelease", { attempts: item.attempts });
    rows.push(row);
  }
  document.querySelector("#release-notice-items").replaceChildren(...rows);
  // In-page, non-modal, no focus/show calls: never bring the manager over a running game.
  document.querySelector("#release-notice").classList.remove("hidden");
}
document.querySelector("#release-notice-close").addEventListener("click", () => document.querySelector("#release-notice").classList.add("hidden"));
workspace.addEventListener(
  "wheel",
  (event) => {
    if (event.ctrlKey) return;
    event.preventDefault();
    workspace.scrollBy({ left: event.deltaX, top: event.deltaY });
  },
  { passive: false }
);

(async () => {
  try {
    render(await window.scde.getState());
  } catch (error) {
    showToast(readableError(error), true);
  } finally {
    hideBusy();
  }
  if (state) {
    try {
      // Let the initialized interface paint before doing any Workshop work.
      await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
      await new Promise((resolve) => setTimeout(resolve, 1500));
      window.scde.checkManagerUpdate().then(showManagerUpdate).catch(() => {});
      window.scde.checkSEUpdates().then(showSEUpdates).catch(error => showToast(readableError(error), true));
      const result = await window.scde.checkWorkshopUpdates();
      if (result?.state) render(result.state);
      showImportResult(result);
    } catch (error) { showToast(readableError(error), true); }
  }
})();
