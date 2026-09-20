const fs = require("node:fs/promises");
const path = require("node:path");
const { randomUUID } = require("node:crypto");
const { readInstalledMods } = require("./mod-package");
const { resolveEnabledMods } = require("./manager");
const { compareVersions } = require("./workshop-updates");
const { prepareSE } = require("./se-core-package");
const { detectSteamGame } = require("./detect-game");
const { GAME_EXECUTABLE } = require("./constants");

const ID = "shcde-script-extender";
async function readVersion(folder) {
  try { return JSON.parse(await fs.readFile(path.join(folder, "manifest.json"), "utf8")).version; }
  catch (error) { if (error.code === "ENOENT") return null; throw error; }
}

class SECoreUpdater {
  constructor(manager, options) {
    this.manager = manager;
    this.options = options;
    this.root = path.join(manager.dataRoot, "se-updates");
    this.target = path.join(manager.modsRoot, ID);
    this.state = {};
    this.onChanged = () => {};
    this.applying = false;
    manager.seUpdater = this;
    manager.onIdle = () => { if (!this.working) this.applyPending().catch(() => {}); };
  }

  folder(name) {
    if (!/^(ready|backup)-[a-f0-9-]{36}$/.test(name || "")) throw new Error("Unsafe SE update state path.");
    return path.join(this.root, name);
  }

  async save(state) {
    const temporary = path.join(this.root, "state.json.tmp");
    await fs.writeFile(temporary, JSON.stringify(state, null, 2));
    await fs.rename(temporary, path.join(this.root, "state.json"));
    this.state = state;
  }

  async initialize() {
    await fs.mkdir(this.root, { recursive: true });
    try { this.state = JSON.parse(await fs.readFile(path.join(this.root, "state.json"), "utf8")); }
    catch (error) { if (error.code !== "ENOENT") throw error; }
    // Recover only our interrupted directory swap; never fall back to deleting the user's SE.
    if (this.state.transaction) {
      const tx = this.state.transaction;
      const actual = await readVersion(this.target);
      if (actual === tx.toVersion) await this.save(this.committed(tx));
      else {
        if (!actual) await fs.rename(this.folder(tx.backup.folder), this.target);
        else if (actual !== tx.fromVersion) throw new Error("SE update recovery requires attention; backup retained.");
        await this.save({ ...this.state, transaction: null });
      }
    }
  }

  accepts(version) { return !!version && this.state.activeVersion === version; }

  status() {
    return { ...this.state.notice, backupVersion: this.state.backup?.version || null,
      reapplyVersion: this.state.skippedVersion ? this.state.rejectedBackup?.version || null : null,
      pendingVersion: this.state.pending?.version || null };
  }

  emit() { this.onChanged(this.status()); }

  async notify(status, version, error = "") {
    await this.save({ ...this.state, notice: { status, version, error } });
    this.emit();
  }

  async update(update, signal) {
    if (this.working || this.applying) return this.status();
    this.working = true;
    let temporary;
    try {
      if (this.state.pending) { await this.applyPending(); return this.status(); }
      if (!update || compareVersions(update.version, await readVersion(this.target)) !== 1) return this.status();
      await this.notify("downloading", update.version);
      temporary = await fs.mkdtemp(path.join(this.root, "download-"));
      const config = await this.manager.configStore.load();
      const gameDir = config.gameDir || (this.options.prepare ? "" : (await detectSteamGame())[0]);
      const manifest = await (this.options.prepare || prepareSE)(update, temporary, {
        ...this.options, signal,
        gameManagedRoot: gameDir ? path.join(gameDir, GAME_EXECUTABLE.replace(/\.exe$/i, "_Data"), "Managed") : "",
        runtimeRoot: path.join(this.manager.modsRoot, "bepinex-runtime/payload/BepInEx/core"),
      });
      signal?.throwIfAborted();
      const ready = "ready-" + randomUUID();
      await fs.rename(temporary, this.folder(ready));
      temporary = null;
      await this.save({ ...this.state, pending: { folder: ready, version: manifest.version, upstreamVersion: update.version } });
      await this.notify("pending", manifest.version);
      await this.applyPending();
    } catch (error) {
      await this.notify("failed", update?.version, `${error.message}${error.attempts ? ` (${error.attempts} attempts)` : ""}`);
    } finally {
      if (temporary) await fs.rm(temporary, { recursive: true, force: true });
      this.working = false;
      await this.applyPending();
    }
    return this.status();
  }

  committed(tx) {
    return { ...this.state, transaction: null, pending: null, activeVersion: tx.toVersion,
      backup: tx.rollback ? null : tx.backup,
      rejectedBackup: tx.rollback ? tx.backup : null,
      skippedVersion: tx.skippedVersion,
      notice: { status: tx.rollback ? "rolled-back" : "updated", version: tx.toVersion } };
  }

  async swap(candidate, rollback = false, manual = false) {
    const mods = await readInstalledMods(this.manager.modsRoot);
    const config = await this.manager.configStore.load();
    const enabledIds = resolveEnabledMods(mods.map(mod => mod.id === ID ? { ...mod, version: candidate.version } : mod), config.enabledMods, config);
    if (manual && !await this.manager.confirmSEComponents(mods, enabledIds, candidate.version, rollback ? "rollback" : "reapply")) return;
    const fromVersion = await readVersion(this.target);
    if (!fromVersion || await readVersion(this.folder(candidate.folder)) !== candidate.version) throw new Error("SE update files have changed; update cancelled.");
    const tx = { fromVersion, toVersion: candidate.version, backup: { folder: "backup-" + randomUUID(), version: fromVersion },
      skippedVersion: rollback ? fromVersion.split("+")[0] : null, rollback };
    const previous = this.state;
    await this.save({ ...this.state, transaction: tx });
    try {
      await fs.rename(this.target, this.folder(tx.backup.folder));
      await fs.rename(this.folder(candidate.folder), this.target);
      await this.save(this.committed(tx));
    } catch (error) {
      if (await readVersion(this.target) === tx.toVersion) await fs.rename(this.target, this.folder(candidate.folder));
      if (!await readVersion(this.target)) {
        await fs.rename(this.folder(tx.backup.folder), this.target);
      }
      await this.save(previous);
      throw error;
    }
    // Do not rewrite a game copy in the background. The existing receipt check deploys this
    // version at the next launch, before spawning the game. User settings remain untouched.
  }

  async applyPending() {
    if (this.applying || !this.state.pending || this.manager.gameLocked || this.manager.mutationBusy) return;
    this.applying = true;
    const candidate = this.state.pending;
    try {
      await this.manager.withModMutation(() => this.swap(candidate));
    } catch (error) {
      await this.save({ ...this.state, pending: null, notice: { status: "failed", error: error.message } });
      if (!this.state.transaction) await fs.rm(this.folder(candidate.folder), { recursive: true, force: true }).catch(() => {});
    } finally { this.applying = false; this.emit(); }
  }

  async rollback() {
    if (this.working || this.applying || this.state.pending || !this.state.backup) throw new Error("SE_UPDATE_BUSY_OR_NO_BACKUP");
    this.applying = true;
    try { await this.manager.withModMutation(() => this.swap(this.state.backup, true, true)); }
    finally { this.applying = false; this.emit(); }
    return this.status();
  }

  async reapply() {
    if (this.working || this.applying || this.state.pending || !this.status().reapplyVersion) throw new Error("SE_UPDATE_BUSY_OR_NO_BACKUP");
    this.applying = true;
    try { await this.manager.withModMutation(() => this.swap(this.state.rejectedBackup, false, true)); }
    finally { this.applying = false; this.emit(); }
    return this.status();
  }
}

module.exports = { SECoreUpdater };
