const fs = require("node:fs/promises");
const path = require("node:path");
const { execFile, spawn } = require("node:child_process");
const { promisify } = require("node:util");
const { ConfigStore } = require("./config-store");
const { Localizations, canonicalLanguage, createModLocalizations } = require("./localization");
const { compareVersions } = require("./workshop-updates");
const {
  createCompatibilityProfile,
  createLobbyProfileText,
} = require("./compatibility-profile");
const { detectSteamGame } = require("./detect-game");
const {
  applyMods,
  pathExists,
  prepareStage,
  resolveStageDirectory,
  validateGameDirectory,
} = require("./deployer");
const { installModPackage, readInstalledMods } = require("./mod-package");
const { GAME_APP_ID, GAME_EXECUTABLE } = require("./constants");
const { isInside, safeJoin } = require("./paths");
const {
  getActiveSteamUserId,
  prepareGameSettings,
  resolveSettingsBackupPath,
} = require("./user-settings");

const execFileAsync = promisify(execFile);
const DEPLOYMENT_RECEIPT = "_scde_manager/active-mods.json";
const PREFERRED_DEPLOYMENT_PROBE = "winhttp.dll";
const SE_ID = "shcde-script-extender";

function dependsOnSE(mod, mods, seen = new Set()) {
  if (!mod || seen.has(mod.id)) return false;
  if (mod.id === SE_ID || mod.scriptExtender) return true;
  seen.add(mod.id);
  return (mod.dependencies || []).some(dep => dep.id === SE_ID || dependsOnSE(mods.find(item => item.id === dep.id), mods, seen));
}

async function deploymentReceiptIsCurrent(stageDir, mods, enabledMods, activeFiles) {
  if (enabledMods.length > 0 && activeFiles.length === 0) return false;

  const probe =
    activeFiles.find((relative) => relative.toLowerCase() === PREFERRED_DEPLOYMENT_PROBE) ||
    activeFiles[0];
  if (probe) {
    try {
      if (!(await pathExists(safeJoin(stageDir, probe)))) return false;
    } catch {
      return false;
    }
  }

  try {
    const deployed = JSON.parse(
      await fs.readFile(safeJoin(stageDir, DEPLOYMENT_RECEIPT), "utf8")
    );
    const expected = createCompatibilityProfile(mods, enabledMods);
    return deployed.schema === expected.schema && deployed.fingerprint === expected.fingerprint;
  } catch {
    return false;
  }
}

async function isSteamRunning(runTasklist = execFileAsync) {
  if (process.platform !== "win32" && runTasklist === execFileAsync) return false;
  try {
    const { stdout } = await runTasklist(
      "tasklist.exe",
      ["/FI", "IMAGENAME eq steam.exe", "/FO", "CSV", "/NH"],
      { windowsHide: true }
    );
    return /"steam\.exe"/i.test(stdout);
  } catch {
    return false;
  }
}

function resolveEnabledMods(mods, requestedIds, config = {}) {
  const modById = new Map(mods.map((mod) => [mod.id, mod]));
  const resolved = [];
  const visiting = new Set();
  const visited = new Set();

  function visit(modId) {
    if (modId === SE_ID && config.seEnabled === false) throw Object.assign(new Error("SE_REQUIRED_DISABLED"), { code: "SE_REQUIRED_DISABLED" });
    if (visited.has(modId)) return;
    if (visiting.has(modId)) throw new Error(`Mod 依赖形成循环：${modId}`);

    const mod = modById.get(modId);
    if (!mod) throw new Error(`找不到已启用的 Mod：${modId}`);
    if (mod.scriptExtender && config.seEnabled === false) throw Object.assign(new Error("SE_REQUIRED_DISABLED"), { code: "SE_REQUIRED_DISABLED" });

    visiting.add(modId);
    for (const dependency of mod.dependencies || []) {
      const installed = modById.get(dependency.id);
      if (!installed) {
        throw new Error(`${mod.name} 缺少依赖：${dependency.id}`);
      }
      if (dependency.version && installed.version !== dependency.version) {
        throw new Error(
          `${mod.name} 需要 ${dependency.id} v${dependency.version}，当前安装的是 v${installed.version}`
        );
      }
      visit(dependency.id);
    }
    if (mod.scriptExtender) {
      const version = modById.get("shcde-script-extender")?.version || "";
      for (const [key, rejected] of [["minimumVersion", -1], ["maximumVersion", 1]]) {
        const bound = mod.scriptExtender[key];
        if (bound && [null, rejected].includes(compareVersions(version, bound))) {
          const error = new Error(`${mod.name}: Script Extender v${version} is outside the supported range (${mod.scriptExtender.minimumVersion || "*"} – ${mod.scriptExtender.maximumVersion || "*"}).`);
          error.code = "SE_VERSION_INCOMPATIBLE";
          throw error;
        }
      }
    }
    visiting.delete(modId);
    visited.add(modId);
    resolved.push(modId);
  }

  for (const modId of requestedIds) visit(modId);
  return resolved;
}

function normalizeModOrder(mods, requestedOrder, pinnedIds = []) {
  const installedIds = new Set(mods.map((mod) => mod.id));
  const order = (requestedOrder || []).filter((id) => installedIds.has(id));
  const knownIds = new Set(order);
  for (const mod of mods) {
    if (!knownIds.has(mod.id)) order.push(mod.id);
  }
  const pinned = pinnedIds.filter((id) => installedIds.has(id));
  const pinnedSet = new Set(pinned);
  return [...pinned, ...order.filter((id) => !pinnedSet.has(id))];
}

function createGameLaunchEnvironment(baseEnvironment = process.env) {
  return {
    ...baseEnvironment,
    SteamAppId: GAME_APP_ID,
    SteamGameId: GAME_APP_ID,
  };
}

function alignModOrder(modOrder, orderedIds) {
  const orderedSet = new Set(orderedIds);
  let orderedIndex = 0;
  return modOrder.map((id) =>
    orderedSet.has(id) ? orderedIds[orderedIndex++] : id
  );
}

class ModManager {
  constructor(dataRoot, options = {}) {
    this.dataRoot = path.resolve(dataRoot);
    this.modsRoot = path.join(this.dataRoot, "mods");
    this.stageDir = "";
    this.configStore = new ConfigStore(this.dataRoot);
    this.localizations = new Localizations(this.dataRoot);
    this.modLocalizations = new Map();
    this.systemPackages = Array.isArray(options.systemPackages) ? options.systemPackages : [];
    this.systemModOrder = this.systemPackages.map((item) => item.id);
    this.requiredSystemModIds = new Set(this.systemPackages.map((item) => item.id));
    this.runSteamCheck = options.isSteamRunning || isSteamRunning;
    this.spawnProcess = options.spawn || spawn;
    this.gameSettingsPath = options.gameSettingsPath || "";
    this.getSteamUserId = options.getActiveSteamUserId || getActiveSteamUserId;
    this.settingsSleep = options.settingsSleep;
    this.gameLocked = false;
    this.mutationBusy = false;
    this.onMutationLockChanged = options.onMutationLockChanged || (() => {});
    this.onIdle = () => {};
    this.onGameLockChanged = options.onGameLockChanged || (() => {});
    this.onGameStarting = options.onGameStarting || (() => {});
    this.onGameExited = options.onGameExited || (() => {});
    this.confirmSECompatibility = options.confirmSECompatibility || (async () => false);
    this.confirmEnableSE = options.confirmEnableSE || (async () => false);
    this.processIsAlive = options.processIsAlive || (pid => {
      try { process.kill(pid, 0); return true; } catch (error) { return error.code !== "ESRCH"; }
    });
    this.checkSessionProcess = options.checkSessionProcess || (async session => {
      if (process.platform !== "win32") return true;
      try {
        const { stdout } = await execFileAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command",
          `[Diagnostics.Process]::GetProcessById(${session.pid}).MainModule.FileName`], { windowsHide: true, timeout: 3000, maxBuffer: 4096 });
        return !stdout.trim() || stdout.trim().toLowerCase() === String(session.executable).toLowerCase();
      } catch { return this.processIsAlive(session.pid); }
    });
  }

  assertGameStopped() {
    if (this.gameLocked) throw new Error("GAME_RUNNING");
    if (this.selfUpdateExiting) throw new Error("MANAGER_BUSY");
    if (this.mutationBusy) throw new Error("MANAGER_BUSY");
    if (this.seUpdater?.state.transaction) throw new Error("SE_UPDATE_RECOVERY_REQUIRED");
  }

  async withModMutation(action) {
    this.assertGameStopped();
    this.mutationBusy = true;
    this.onMutationLockChanged(true);
    let result;
    try { result = await action(); }
    finally {
      if (!this.selfUpdateExiting) {
        this.mutationBusy = false;
        this.onMutationLockChanged(false);
        this.onIdle();
      }
    }
    if (result?.mods) result.mutationBusy = this.mutationBusy;
    if (result?.state) result.state.mutationBusy = this.mutationBusy;
    return result;
  }

  setGameLocked(locked) {
    this.gameLocked = locked;
    this.onGameLockChanged(locked);
    if (!locked && !this.mutationBusy) this.onIdle();
  }

  async initialize() {
    await Promise.all([
      this.configStore.ensure(),
      fs.mkdir(this.modsRoot, { recursive: true }),
    ]);
  }

  async initializeLanguage(systemLocale) {
    this.systemLocale = systemLocale;
    const config = await this.configStore.load();
    await this.configureLanguageDirectory(config);
    await this.localizations.initialize();
    if (canonicalLanguage(config.language)) {
      return config.language;
    }

    const language = await this.localizations.choose(systemLocale);
    await this.configStore.save({ ...config, language });
    return language;
  }

  async getLocalization() {
    const config = await this.configStore.load();
    await this.configureLanguageDirectory(config);
    return this.localizations.get(config.language);
  }

  async configureLanguageDirectory(config) {
    const stage = resolveStageDirectory(config.gameDir);
    const folder = stage ? path.join(stage, "BepInEx/config/modmanager/lang") : path.join(this.dataRoot, "config/modmanager");
    if (this.localizations.folder === folder) return;
    if (stage && !(await validateGameDirectory(config.gameDir)).valid) return;
    this.localizations = new Localizations(this.dataRoot, { folder, safetyRoot: stage || this.dataRoot,
      legacyFolder: stage ? path.join(this.dataRoot, "config/modmanager") : undefined });
    await this.localizations.initialize();
  }

  async getModLocalization(modId) {
    const config = await this.configStore.load();
    if (!(await validateGameDirectory(config.gameDir)).valid) throw new Error("GAME_DIRECTORY_REQUIRED");
    if (!(await readInstalledMods(this.modsRoot)).some(mod => mod.id === modId)) throw new Error("Unknown Mod language ID");
    const stage = resolveStageDirectory(config.gameDir), key = stage + "|" + modId;
    if (!this.modLocalizations.has(key)) this.modLocalizations.set(key, await createModLocalizations(stage, modId));
    const catalog = this.modLocalizations.get(key);
    return catalog.get(await catalog.choose(this.systemLocale || config.language));
  }

  async restoreGameSession() {
    let session;
    try { session = JSON.parse(await fs.readFile(path.join(this.dataRoot, "game-session.json"), "utf8")); }
    catch (error) { if (error.code === "ENOENT") return; throw error; }
    if (!Number.isSafeInteger(session.pid) || session.pid <= 0) throw new Error("Invalid game session record.");
    if (!this.processIsAlive(session.pid)) return;
    if (!await this.checkSessionProcess(session)) return;
    // Keep the update/edit lock if the manager was closed while its game was still running.
    // If process identity cannot be read, retain the lock conservatively until that PID exits.
    this.resumedGameRunning = true;
    this.setGameLocked(true);
    const timer = setInterval(() => {
      if (this.processIsAlive(session.pid)) return;
      clearInterval(timer);
      this.resumedGameRunning = false;
      this.setGameLocked(false);
    }, 2000);
    timer.unref();
  }

  async getState() {
    await this.initialize();
    const [config, mods] = await Promise.all([
      this.configStore.load(),
      readInstalledMods(this.modsRoot),
    ]);
    this.stageDir = resolveStageDirectory(config.gameDir);
    const [game, stagedGame] = await Promise.all([
      validateGameDirectory(config.gameDir),
      validateGameDirectory(this.stageDir),
    ]);
    const stageValid = stagedGame.valid && config.stageSource === config.gameDir && game.valid;
    const modById = new Map(mods.map((mod) => [mod.id, mod]));
    const modOrder = normalizeModOrder(mods, config.modOrder, this.systemModOrder);
    const deploymentComplete =
      stageValid &&
      (await deploymentReceiptIsCurrent(
        this.stageDir,
        mods,
        config.enabledMods,
        config.activeFiles
      ));
    const stageReady = stageValid && deploymentComplete;

    const localization = await this.getLocalization();
    return {
      appVersion: require("../../package.json").version,
      gameLocked: this.gameLocked,
      mutationBusy: this.mutationBusy,
      seUpdate: this.seUpdater?.status() || null,
      gameDir: config.gameDir,
      language: localization.language,
      localization,
      gameValid: game.valid,
      gameReason: game.valid ? "游戏目录有效" : game.reason,
      stageDir: this.stageDir,
      stageValid,
      stageReady,
      stageSource: config.stageSource || "",
      enabledMods: config.enabledMods,
      activeFileCount: config.activeFiles.length,
      lastConflicts: config.lastConflicts || [],
      mods: modOrder.map((id) => {
        const { folder, ...mod } = modById.get(id);
        return {
          ...mod,
          enabled: config.enabledMods.includes(mod.id),
          required: this.requiredSystemModIds.has(mod.id),
        };
      }),
    };
  }

  async setGameDirectory(gameDir) {
    return this.withModMutation(() => this.#setGameDirectory(gameDir));
  }

  async #setGameDirectory(gameDir) {
    const resolved = path.resolve(gameDir);
    const validation = await validateGameDirectory(resolved);
    if (!validation.valid) throw new Error(validation.reason);
    this.stageDir = resolveStageDirectory(resolved);

    await this.configStore.update((config) => ({
      ...config,
      gameDir: resolved,
      activeFiles: config.gameDir === resolved ? config.activeFiles : [],
      lastConflicts: config.gameDir === resolved ? config.lastConflicts || [] : [],
    }));
    return this.getState();
  }

  async autoDetectGame() {
    const matches = await detectSteamGame();
    if (matches.length === 0) {
      throw new Error("没有在 Steam 库中自动找到游戏，请手动选择游戏目录。");
    }
    return this.setGameDirectory(matches[0]);
  }

  async installPackage(packagePath) {
    const result = await this.installPackages([packagePath]);
    if (result.failures.length > 0) throw new Error(result.failures[0].error);
    return result.state;
  }

  async installPackages(packagePaths) {
    return this.withModMutation(() => this.#installPackages(packagePaths));
  }

  async #installPackages(packagePaths, deployStage = true) {
    const previousIds = new Set((await readInstalledMods(this.modsRoot)).map((mod) => mod.id));
    const imported = [];
    const failures = [];
    const activationFailures = [];

    for (const packagePath of packagePaths) {
      try {
        imported.push(await installModPackage(packagePath, this.modsRoot));
      } catch (error) {
        failures.push({
          file: path.basename(packagePath),
          error: error.message,
          code: error.code,
        });
      }
    }

    if (imported.length > 0) {
      const mods = await readInstalledMods(this.modsRoot);
      await this.configStore.update((config) => {
        const modOrder = normalizeModOrder(mods, config.modOrder, this.systemModOrder);
        let enabledMods = config.enabledMods;
        for (const id of new Set(imported.map((mod) => mod.id))) {
          if (previousIds.has(id)) continue;
          try {
            const requested = new Set([...enabledMods, id]);
            enabledMods = resolveEnabledMods(mods, modOrder.filter((item) => requested.has(item)), config);
          } catch (error) {
            activationFailures.push({ name: mods.find((mod) => mod.id === id).name, error: error.message });
          }
        }
        return { ...config, modOrder: alignModOrder(modOrder, enabledMods), enabledMods };
      });
    }

    let state = await this.getState();
    const updatedEnabledMod = imported.some((mod) => state.enabledMods.includes(mod.id));
    if (deployStage && state.stageValid && updatedEnabledMod) {
      await this.redeploy();
      state = await this.getState();
    }

    return { state, imported, failures, activationFailures };
  }

  async setEnabled(modId, enabled) {
    return this.withModMutation(() => this.#setEnabled(modId, enabled));
  }

  async #setEnabled(modId, enabled) {
    if (!enabled && modId !== SE_ID && this.requiredSystemModIds.has(modId)) {
      throw new Error("此 Mod 是管理器必需的内置组件，不能禁用。");
    }
    const mods = await readInstalledMods(this.modsRoot);
    if (!mods.some((mod) => mod.id === modId)) throw new Error(`找不到 Mod：${modId}`);
    const before = await this.configStore.load();
    const enableSE = enabled && modId !== SE_ID && before.seEnabled === false && dependsOnSE(mods.find(mod => mod.id === modId), mods);
    if (enableSE && !await this.confirmEnableSE(mods.find(mod => mod.id === modId).name)) return null;
    const disabledDependents = !enabled && modId === SE_ID
      ? mods.filter(mod => mod.id !== SE_ID && before.enabledMods.includes(mod.id) && dependsOnSE(mod, mods)) : [];

    await this.configStore.update((config) => {
      if (!enabled && modId !== SE_ID) {
        const dependents = mods.filter(
          (mod) =>
            config.enabledMods.includes(mod.id) &&
            (mod.dependencies || []).some((dependency) => dependency.id === modId)
        );
        if (dependents.length > 0) {
          throw new Error(
            `${modId} 正被以下 Mod 使用：${dependents.map((mod) => mod.name).join("、")}。请先禁用它们。`
          );
        }
      }
      const enabledSet = new Set(config.enabledMods);
      if (modId === SE_ID || enableSE) config.seEnabled = enabled;
      if (enableSE) enabledSet.add(SE_ID);
      for (const mod of disabledDependents) enabledSet.delete(mod.id);
      if (enabled) enabledSet.add(modId);
      else enabledSet.delete(modId);
      const modOrder = normalizeModOrder(mods, config.modOrder, this.systemModOrder);
      const enabledMods = modOrder.filter((id) => enabledSet.has(id));
      const resolvedEnabledMods = enabled
        ? resolveEnabledMods(mods, enabledMods, config)
        : enabledMods;
      return {
        ...config,
        modOrder: alignModOrder(modOrder, resolvedEnabledMods),
        enabledMods: resolvedEnabledMods,
      };
    });

    const state = await this.getState();
    if (state.stageValid) await this.redeploy();
    return { ...await this.getState(), disabledDependents: disabledDependents.map(mod => mod.name) };
  }

  async removeMod(modId) {
    return this.withModMutation(() => this.#removeMod(modId));
  }

  async #removeMod(modId) {
    if (this.requiredSystemModIds.has(modId)) {
      throw new Error("此 Mod 是管理器必需的内置组件，不能移除。");
    }
    const target = path.join(this.modsRoot, modId);
    if (!isInside(this.modsRoot, target) || target === this.modsRoot) {
      throw new Error("拒绝删除不安全的 Mod 路径。");
    }

    const [state, mods] = await Promise.all([this.getState(), readInstalledMods(this.modsRoot)]);
    const exists = state.mods.some((mod) => mod.id === modId);
    if (!exists) throw new Error(`找不到 Mod：${modId}`);

    const dependents = mods.filter((mod) =>
      (mod.dependencies || []).some((dependency) => dependency.id === modId)
    );
    if (dependents.length > 0) {
      throw new Error(
        `${modId} 仍被以下已安装 Mod 依赖：${dependents.map((mod) => mod.name).join("、")}。请先移除它们。`
      );
    }

    if (state.enabledMods.includes(modId)) await this.#setEnabled(modId, false);
    await fs.rm(target, { recursive: true, force: true });
    await this.configStore.update((config) => ({
      ...config,
      modOrder: config.modOrder.filter((id) => id !== modId),
    }));
    return this.getState();
  }

  async moveMod(modId, direction) {
    return this.withModMutation(() => this.#moveMod(modId, direction));
  }

  async #moveMod(modId, direction) {
    if (direction !== -1 && direction !== 1) throw new Error("无效的 Mod 排序方向。");
    if (this.requiredSystemModIds.has(modId)) {
      throw new Error("内置系统组件的加载位置已固定，不能移动。");
    }
    const mods = await readInstalledMods(this.modsRoot);
    if (!mods.some((mod) => mod.id === modId)) throw new Error(`找不到 Mod：${modId}`);

    await this.configStore.update((config) => {
      const modOrder = normalizeModOrder(mods, config.modOrder, this.systemModOrder);
      const index = modOrder.indexOf(modId);
      const targetIndex = index + direction;
      if (targetIndex < 0 || targetIndex >= modOrder.length) return { ...config, modOrder };
      if (this.requiredSystemModIds.has(modOrder[targetIndex])) {
        throw new Error("普通 Mod 不能移动到内置系统组件之前。");
      }

      [modOrder[index], modOrder[targetIndex]] = [modOrder[targetIndex], modOrder[index]];
      const enabledSet = new Set(config.enabledMods);
      const requestedEnabledMods = modOrder.filter((id) => enabledSet.has(id));
      const enabledMods = resolveEnabledMods(mods, requestedEnabledMods, config);
      if (enabledMods.some((id, enabledIndex) => id !== requestedEnabledMods[enabledIndex])) {
        throw new Error("此排序会让依赖项晚于使用它的 Mod，无法应用。");
      }
      return { ...config, modOrder, enabledMods };
    });

    const state = await this.getState();
    if (state.stageValid) await this.redeploy();
    return this.getState();
  }

  async prepareStage() {
    const config = await this.configStore.load();
    this.stageDir = resolveStageDirectory(config.gameDir);
    const mods = await readInstalledMods(this.modsRoot);
    const enabledMods = resolveEnabledMods(mods, config.enabledMods, config);
    const result = await prepareStage({
      gameDir: config.gameDir,
      stageDir: this.stageDir,
      mods,
      enabledIds: enabledMods,
    });
    await this.configStore.save({
      ...config,
      enabledMods,
      stageSource: config.gameDir,
      activeFiles: result.activeFiles,
      lastConflicts: result.conflicts,
    });
    await this.writeCompatibilityProfile(mods, enabledMods);
    return this.getState();
  }

  async redeploy() {
    const config = await this.configStore.load();
    this.stageDir = resolveStageDirectory(config.gameDir);
    if (config.stageSource !== config.gameDir) {
      throw new Error("游戏目录已改变，请重新准备游戏副本。");
    }
    const mods = await readInstalledMods(this.modsRoot);
    const enabledMods = resolveEnabledMods(mods, config.enabledMods, config);
    const result = await applyMods({
      gameDir: config.gameDir,
      stageDir: this.stageDir,
      mods,
      enabledIds: enabledMods,
      previousActiveFiles: config.activeFiles,
    });
    await this.configStore.save({
      ...config,
      enabledMods,
      activeFiles: result.activeFiles,
      lastConflicts: result.conflicts,
    });
    await this.writeCompatibilityProfile(mods, enabledMods);
    return result;
  }

  async writeCompatibilityProfile(mods, enabledMods) {
    const directory = path.join(this.stageDir, "_scde_manager");
    const target = path.join(directory, "active-mods.json");
    const lobbyTarget = path.join(directory, "active-mods.lobby");
    const temporary = `${target}.tmp`;
    const lobbyTemporary = `${lobbyTarget}.tmp`;
    const profile = createCompatibilityProfile(mods, enabledMods);
    await fs.mkdir(directory, { recursive: true });
    await fs.writeFile(temporary, `${JSON.stringify(profile, null, 2)}\n`, "utf8");
    await fs.writeFile(lobbyTemporary, `${createLobbyProfileText(profile)}\n`, "utf8");
    await fs.copyFile(temporary, target);
    await fs.copyFile(lobbyTemporary, lobbyTarget);
    await fs.rm(temporary, { force: true });
    await fs.rm(lobbyTemporary, { force: true });
    return profile;
  }

  async ensureSystemMods(deployStage = true, tolerateSEVersionMismatch = false) {
    if (this.seUpdater?.state.transaction) throw new Error("SE_UPDATE_RECOVERY_REQUIRED");
    if (this.resumedGameRunning) return this.getState();
    if (this.systemPackages.length === 0) return this.getState();

    let mods = await readInstalledMods(this.modsRoot);
    const installedById = new Map(mods.map((mod) => [mod.id, mod]));
    const accepted = (item, installed) => installed?.version === item.version ||
      (item.id === "shcde-script-extender" && this.seUpdater?.accepts(installed?.version));
    const packagesToInstall = this.systemPackages
      .filter((item) => !accepted(item, installedById.get(item.id)))
      .map((item) => item.packagePath);

    if (packagesToInstall.length > 0) {
      const result = await this.#installPackages(packagesToInstall, false);
      if (result.failures.length > 0) {
        const error = new Error(
          `联机兼容性系统组件安装失败：${result.failures.map((item) => item.error).join("；")}`
        );
        error.code = result.failures.find((item) => ["EPERM", "EACCES", "EBUSY"].includes(item.code))?.code;
        throw error;
      }
      mods = await readInstalledMods(this.modsRoot);
    }

    const finalById = new Map(mods.map((mod) => [mod.id, mod]));
    for (const item of this.systemPackages) {
      if (!accepted(item, finalById.get(item.id))) {
        throw new Error(`联机兼容性系统组件版本不正确：${item.id} v${item.version}`);
      }
    }

    let seVersionMismatch = false;
    await this.configStore.update((config) => {
      const modOrder = normalizeModOrder(mods, config.modOrder, this.systemModOrder);
      const enabledSet = new Set([...config.enabledMods, ...this.requiredSystemModIds]);
      if (config.seEnabled === false) {
        enabledSet.delete(SE_ID);
        for (const mod of mods) if (dependsOnSE(mod, mods)) enabledSet.delete(mod.id);
      }
      let enabledMods = modOrder.filter((id) => enabledSet.has(id));
      try { enabledMods = resolveEnabledMods(mods, enabledMods, config); }
      catch (error) {
        if (!tolerateSEVersionMismatch || error.code !== "SE_VERSION_INCOMPATIBLE") throw error;
        // Keep recovery controls available without deploying the incompatible profile.
        seVersionMismatch = true;
      }
      return {
        ...config,
        modOrder: alignModOrder(modOrder, enabledMods),
        enabledMods,
      };
    });

    const state = await this.getState();
    if (deployStage && !seVersionMismatch && state.stageValid && !state.stageReady) {
      await this.redeploy();
      return this.getState();
    }
    return state;
  }

  async launch() {
    this.assertGameStopped();
    this.setGameLocked(true);
    try {
      const result = await this.#launchGame();
      if (!result) this.setGameLocked(false);
      return result;
    } catch (error) {
      this.setGameLocked(false);
      throw error;
    }
  }

  async confirmSEComponents(mods, enabledIds, version, action) {
    const warnings = await require("./se-compatibility").componentWarnings(mods, enabledIds, version);
    if (!warnings.length) return true;
    const confirmed = await this.confirmSECompatibility({ warnings, version, action });
    // A UI callback cannot override incompatible-component launch protection.
    return action !== "launch" && confirmed === true;
  }

  async #launchGame() {
    if (!(await this.runSteamCheck())) throw new Error("STEAM_NOT_RUNNING");

    let state = await this.getState();
    if (!state.gameValid) throw new Error(state.gameReason);
    state = await this.ensureSystemMods(false, true);
    const version = state.enabledMods.includes(SE_ID) ? state.mods.find(mod => mod.id === SE_ID)?.version : null;
    if (version && !await this.confirmSEComponents(await readInstalledMods(this.modsRoot), state.enabledMods, version, "launch")) return null;
    if (state.stageValid) {
      if (!state.stageReady) {
        await this.redeploy();
        state = await this.getState();
      }
    } else {
      state = await this.prepareStage();
    }
    if (!state.stageReady) {
      throw new Error("游戏副本中的 Mod 文件部署不完整，已停止启动。");
    }
    let settingsProtection = {
      status: "disabled",
      settingsPath: this.gameSettingsPath,
      backupPath: "",
    };
    if (this.gameSettingsPath) {
      const steamUserId = await this.getSteamUserId();
      const config = await this.configStore.load();
      const accountChanged = (config.lastSteamUserId || "") !== (steamUserId || "");
      settingsProtection = await prepareGameSettings({
        settingsPath: this.gameSettingsPath,
        backupPath: resolveSettingsBackupPath(this.dataRoot, steamUserId),
        accountChanged,
        sleep: this.settingsSleep,
      });
      if (accountChanged) {
        await this.configStore.save({ ...config, lastSteamUserId: steamUserId || "" });
      }
    }
    const executable = path.join(this.stageDir, GAME_EXECUTABLE);
    if (!(await pathExists(executable))) throw new Error("游戏副本的可执行文件不存在。");
    const launchId = `${Date.now()}-${process.pid}`;
    const launchAudit = path.join(this.dataRoot, "launch-history.log");
    const activeMods = state.mods
      .filter((mod) => mod.enabled)
      .map((mod) => `${mod.id}@${mod.version}`)
      .join(",");
    await fs.appendFile(
      launchAudit,
      `[${new Date().toISOString()}] launch=${launchId} deployment=receipt-current settings=${settingsProtection.status} mods=${activeMods}\n`,
      "utf8"
    );

    // Status reporting is optional and must never prevent a valid launch.
    try { await this.onGameStarting({ stageDir: this.stageDir, launchId }); } catch {}
    const child = await new Promise((resolve, reject) => {
      const process = this.spawnProcess(executable, [], {
        cwd: this.stageDir,
        detached: true,
        stdio: "ignore",
        windowsHide: false,
        env: {
          ...createGameLaunchEnvironment(),
          SCDEModManagerLaunchId: launchId,
          SCDEModManagerDataRoot: this.dataRoot,
          SCDEModManagerSettingsBackup: settingsProtection.backupPath || "",
        },
      });
      process.once("spawn", () => resolve(process));
      process.once("error", reject);
      process.once("exit", () => {
        this.setGameLocked(false);
        try { this.onGameExited(); } catch {}
      });
    });
    child.once("exit", (code, signal) => {
      fs.appendFile(
        launchAudit,
        `[${new Date().toISOString()}] launch=${launchId} pid=${child.pid} exit=${code} signal=${signal || "none"}\n`,
        "utf8"
      ).catch(() => {});
    });
    // A record failure must not report launch failure and unlock a game that already spawned.
    await fs.writeFile(path.join(this.dataRoot, "game-session.json"), JSON.stringify({ pid: child.pid, executable }))
      .catch(error => fs.appendFile(launchAudit, `session-record-error=${error.message}\n`).catch(() => {}));
    child.unref();
    return { pid: child.pid, state: { ...state, gameLocked: this.gameLocked } };
  }
}

module.exports = {
  ModManager,
  createGameLaunchEnvironment,
  isSteamRunning,
  resolveEnabledMods,
  resolveStageDirectory,
};
