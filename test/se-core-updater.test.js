const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const { EventEmitter } = require("node:events");
const AdmZip = require("adm-zip");
const { ModManager } = require("../src/core/manager");
const { SECoreUpdater } = require("../src/core/se-core-updater");
const { downloadSE, extractSE } = require("../src/core/se-core-package");
const { checkReleaseUpdates } = require("../src/core/release-updates");
const { GAME_EXECUTABLE } = require("../src/core/constants");
const ID = "shcde-script-extender";
const old = "2.6.0+scdemm.1", newer = "2.7.0+scdemm.1";
const url = "https://gitlab.com/api/v4/projects/74440776/packages/generic/shcdese/2.7.0/SHCDESE.zip";
const update = { id: ID, version: "2.7.0", downloadUrl: url };
async function writeMod(folder, version) {
  await fs.mkdir(path.join(folder, "payload/BepInEx/plugins/000shcdese"), { recursive: true });
  await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify({ id: ID, name: "Script Extender", version, author: "Rawra", dependencies: [] }));
  await fs.writeFile(path.join(folder, "payload/BepInEx/plugins/000shcdese/SHCDESE.dll"), version);
  return { version };
}
async function fixture(t, prepare) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-se-update-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const children = [];
  const manager = new ModManager(path.join(root, "manager"), {
    systemPackages: [{ id: ID, version: old, packagePath: "must-not-reinstall.scdemod" }], isSteamRunning: async () => true,
    spawn() { const child = new EventEmitter(); child.pid = 123; child.unref = () => {}; children.push(child); queueMicrotask(() => child.emit("spawn")); return child; },
  });
  await manager.initialize();
  await writeMod(path.join(manager.modsRoot, ID), old);
  await manager.configStore.update(c => ({ ...c, enabledMods: [ID], language: "en" }));
  const updater = new SECoreUpdater(manager, { prepare: prepare || (async (_, folder) => writeMod(folder, newer)) });
  await updater.initialize();
  return { root, manager, updater, children };
}
async function version(updater) { return JSON.parse(await fs.readFile(path.join(updater.target, "manifest.json"))).version; }
async function until(predicate) {
  for (let i = 0; i < 200; i++) { if (await predicate()) return; await new Promise(r => setTimeout(r, 5)); }
  throw new Error("Timed out");
}

test("only the known official SE runtime asset becomes an automatic download", async () => {
  for (const asset of [url, "https://evil.example/SHCDESE.zip", url.replace("74440776", "123"), url.replace("SHCDESE.zip", "SHCDESE-Debug.zip")]) {
    const result = await checkReleaseUpdates([{ id: ID, version: old }], { fetch: async () => new Response(JSON.stringify([{ tag_name: "v2.7.0", assets: { links: [{ direct_asset_url: asset }] } }])) });
    assert.equal(result.updates[0].downloadUrl, asset === url ? url : null);
  }
});

test("automatic SE install persists across reopen, retains old backup and supports rollback", async t => {
  const { manager, updater } = await fixture(t);
  assert.equal((await updater.update(update)).status, "updated");
  assert.equal(await version(updater), newer);
  assert.equal(await version({ target: updater.folder(updater.state.backup.folder) }), old);
  await manager.ensureSystemMods(false);
  assert.equal((await manager.getState()).mods[0].version, newer);
  const reopened = new SECoreUpdater(manager, updater.options); await reopened.initialize();
  await manager.ensureSystemMods(false);
  assert.equal((await reopened.rollback()).status, "rolled-back");
  assert.equal(await version(reopened), old);
  await reopened.update(update);
  assert.equal(await version(reopened), newer, "Automatic updates no longer honor legacy rollback skip markers");
  assert.equal((await manager.getState()).mutationBusy, false);
});

test("game launch is allowed during download and pending SE applies only after exit", async t => {
  let finish;
  const { root, manager, updater, children } = await fixture(t, async (_, folder) => { await new Promise(r => { finish = r; }); return writeMod(folder, newer); });
  const original = path.join(root, "game"); await fs.mkdir(original);
  await fs.writeFile(path.join(original, GAME_EXECUTABLE), "fake");
  await manager.setGameDirectory(original);
  const job = updater.update(update);
  await until(() => !!finish);
  assert.equal(manager.mutationBusy, false);
  await manager.launch();
  assert.equal(manager.gameLocked, true);
  finish(); await job;
  assert.equal(await version(updater), old);
  assert.equal(updater.status().status, "pending");
  await assert.rejects(updater.rollback(), /BUSY_OR_NO_BACKUP/);
  children[0].emit("exit", 0);
  await until(() => updater.status().status === "updated");
  assert.equal(await version(updater), newer);
  assert.equal(await fs.readFile(path.join(manager.stageDir, "BepInEx/plugins/000shcdese/SHCDESE.dll"), "utf8"), old);
  await manager.launch();
  assert.equal(await fs.readFile(path.join(manager.stageDir, "BepInEx/plugins/000shcdese/SHCDESE.dll"), "utf8"), newer);
  assert.equal(await fs.readdir(original).then(files => files.includes("BepInEx")), false);
  children[1].emit("exit", 0);
  await new Promise(r => setTimeout(r, 20));
});

test("explicit reapply restores a rolled-back SE offline, survives reopen and supports another rollback", async t => {
  const { manager, updater } = await fixture(t);
  await updater.update(update);
  await updater.rollback();
  assert.equal(updater.status().reapplyVersion, newer);
  const reopened = new SECoreUpdater(manager, { prepare: () => { throw new Error("must not download"); } });
  await reopened.initialize();
  assert.equal(reopened.status().reapplyVersion, newer);
  assert.equal((await reopened.reapply()).status, "updated");
  assert.equal(await version(reopened), newer);
  assert.equal(reopened.state.skippedVersion, null);
  assert.equal(reopened.status().reapplyVersion, null);
  assert.equal(reopened.status().backupVersion, old);
  await manager.ensureSystemMods(false);
  assert.equal(await version(reopened), newer);
  await reopened.rollback();
  await reopened.reapply();
  assert.equal(await version(reopened), newer);
});

test("SE 2.8 update takes effect on the next game launch in the same manager process", async t => {
  const next = "2.8.0+scdemm.1";
  const { root, manager, updater, children } = await fixture(t, async (_, folder) => writeMod(folder, next));
  const original = path.join(root, "game"); await fs.mkdir(original);
  await fs.writeFile(path.join(original, GAME_EXECUTABLE), "fake");
  await manager.setGameDirectory(original); await manager.prepareStage();
  const deployed = path.join(manager.stageDir, "BepInEx/plugins/000shcdese/SHCDESE.dll");
  assert.equal(await fs.readFile(deployed, "utf8"), old);
  await updater.update({ ...update, version: "2.8.0" });
  assert.equal((await manager.getState()).mods.find(mod => mod.id === ID).version, next);
  assert.equal(await fs.readFile(deployed, "utf8"), old, "background update does not overwrite the game copy");
  await manager.launch();
  assert.equal(await fs.readFile(deployed, "utf8"), next);
  assert.equal(children.length, 1);
  children[0].emit("exit", 0); await new Promise(r => setTimeout(r, 20));
});

test("reapply respects the game lock and failure retains the rejected backup for retry", async t => {
  const { manager, updater } = await fixture(t);
  await updater.update(update);
  await updater.rollback();
  const retained = updater.state.rejectedBackup;
  manager.gameLocked = true;
  await assert.rejects(updater.reapply(), /GAME_RUNNING/);
  manager.gameLocked = false;
  const save = updater.save.bind(updater);
  let failed = false;
  updater.save = async state => {
    if (!failed && state.notice?.status === "updated") { failed = true; throw new Error("disk failure"); }
    return save(state);
  };
  await assert.rejects(updater.reapply(), /disk failure/);
  assert.equal(await version(updater), old);
  assert.deepEqual(updater.state.rejectedBackup, retained);
  assert.equal(updater.status().reapplyVersion, newer);
  await updater.reapply();
  assert.equal(await version(updater), newer);
});

test("rollback and reapply validate enabled SE Mod version requirements before swapping", async t => {
  const { manager, updater } = await fixture(t);
  await updater.update(update);
  const mod = path.join(manager.modsRoot, "se-dependent");
  await fs.mkdir(mod);
  const manifest = { id: "se-dependent", name: "Dependent", version: "1", dependencies: [{ id: ID }],
    scriptExtender: { guid: "dependent", minimumVersion: "2.7.0" } };
  await fs.writeFile(path.join(mod, "manifest.json"), JSON.stringify(manifest));
  await manager.configStore.update(c => ({ ...c, enabledMods: [ID, manifest.id] }));
  await assert.rejects(updater.rollback(), /2\.7\.0/);
  assert.equal(await version(updater), newer);
  manifest.scriptExtender = { guid: "dependent" };
  await fs.writeFile(path.join(mod, "manifest.json"), JSON.stringify(manifest));
  await updater.rollback();
  manifest.scriptExtender = { guid: "dependent", maximumVersion: "2.6.0" };
  await fs.writeFile(path.join(mod, "manifest.json"), JSON.stringify(manifest));
  await assert.rejects(updater.reapply(), /2\.6\.0/);
  assert.equal(await version(updater), old);
});

test("nested components warn on rollback and block launch even if a callback tries to override", async t => {
  const { root, manager, updater, children } = await fixture(t);
  const packId = "se-pack", folder = path.join(manager.modsRoot, packId);
  const nested = path.join(folder, "payload/BepInEx/plugins/Pack/Mods/Child");
  await fs.mkdir(nested, { recursive: true });
  await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify({ id: packId, name: "Pack", version: "1.0", dependencies: [{ id: ID }],
    scriptExtender: { guid: "Pack", minimumVersion: "2.3.0", metadataVersion: 2 } }));
  await fs.writeFile(path.join(nested, "info.json"), JSON.stringify({ GUID: "Child", Name: "Child Feature", MinimumScriptExtenderVersion: "2.7.0" }));
  await manager.configStore.update(c => ({ ...c, enabledMods: [ID, packId] }));
  const original = path.join(root, "game"); await fs.mkdir(original);
  await fs.writeFile(path.join(original, GAME_EXECUTABLE), "fake");
  await manager.setGameDirectory(original);
  await updater.update(update);
  let allow = false;
  const notices = [];
  manager.confirmSECompatibility = async notice => {
    notices.push(notice);
    assert.equal(notice.warnings[0].name, "Child Feature");
    assert.equal(manager.gameLocked || manager.mutationBusy, true, "no changes while a warning is open");
    return allow;
  };
  await updater.rollback();
  assert.equal(await version(updater), newer, "cancelled warning leaves SE untouched");
  allow = true;
  await updater.rollback();
  assert.equal(await version(updater), old);
  assert.equal(await manager.launch(), null, "launch cannot be confirmed past an incompatible component");
  assert.equal(manager.gameLocked, false);
  assert.equal(children.length, 0);
  assert.deepEqual(notices.map(n => n.action), ["rollback", "rollback", "launch"]);
  await updater.reapply();
  await manager.launch();
  assert.equal(children.length, 1);
  children[0].emit("exit", 0);
  await new Promise(r => setTimeout(r, 20));
});

test("an existing incompatible pack does not prevent manager startup, but blocks launch until disabled", async t => {
  const { root, manager, children } = await fixture(t);
  const original = path.join(root, "game"); await fs.mkdir(original);
  await fs.writeFile(path.join(original, GAME_EXECUTABLE), "fake");
  await manager.setGameDirectory(original);
  await manager.prepareStage();
  const folder = path.join(manager.modsRoot, "se-new-pack"); await fs.mkdir(folder);
  await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify({ id: "se-new-pack", name: "New Pack", version: "1",
    dependencies: [{ id: ID }], scriptExtender: { guid: "NewPack", minimumVersion: "2.7.1", metadataVersion: 2 } }));
  await manager.configStore.update(c => ({ ...c, enabledMods: [ID, "se-new-pack"] }));
  const state = await manager.ensureSystemMods(true, true);
  assert.ok(state.enabledMods.includes("se-new-pack"), "startup must not silently disable a mod");
  assert.equal(state.stageReady, false, "startup must not deploy an incompatible combination");
  const notices = [];
  manager.confirmSECompatibility = async notice => { notices.push(notice); return true; };
  assert.equal(await manager.launch(), null);
  assert.equal(children.length, 0);
  assert.equal(manager.gameLocked, false);
  assert.equal(notices[0].warnings[0].name, "New Pack");
  await manager.setEnabled("se-new-pack", false);
  await manager.launch();
  assert.equal(children.length, 1);
  children[0].emit("exit", 0);
  await new Promise(r => setTimeout(r, 20));
});

test("manual edits and update commits exclude each other, without holding a network lock", async t => {
  const { manager, updater } = await fixture(t);
  let release;
  const operation = manager.withModMutation(() => new Promise(r => { release = r; }));
  await updater.update(update);
  assert.equal(updater.status().status, "pending");
  await assert.rejects(manager.launch(), /MANAGER_BUSY/);
  release(); await operation;
  await until(() => updater.status().status === "updated");
  assert.equal(await version(updater), newer);
});

test("download or structural guard failure leaves old SE untouched", async t => {
  const { updater } = await fixture(t, async () => { throw new Error("SE managed updater entry point has changed."); });
  assert.equal((await updater.update(update)).status, "failed");
  assert.equal(await version(updater), old);
  assert.equal(updater.state.backup, undefined);
  assert.equal((await fs.readdir(updater.root)).some(name => name.startsWith("download-")), false);
});

test("an interrupted swap restores the old directory before any built-in pinning", async t => {
  const { manager, updater } = await fixture(t);
  const backup = { folder: "backup-11111111-1111-1111-1111-111111111111", version: old };
  await updater.save({ activeVersion: old, transaction: { fromVersion: old, toVersion: newer, backup } });
  await fs.rename(updater.target, updater.folder(backup.folder));
  const reopened = new SECoreUpdater(manager, updater.options); await reopened.initialize();
  assert.equal(await version(reopened), old);
  assert.equal(reopened.state.transaction, null);
  await manager.ensureSystemMods(false);
});

test("failure to persist a completed swap rolls the filesystem back", async t => {
  const { updater } = await fixture(t);
  const save = updater.save.bind(updater); let failed = false;
  updater.save = async state => {
    if (!failed && state.notice?.status === "updated") { failed = true; throw new Error("simulated disk failure"); }
    return save(state);
  };
  await updater.update(update);
  assert.equal(await version(updater), old);
  assert.equal(updater.status().status, "failed");
  assert.equal(updater.state.transaction, undefined);
});

test("a reopened manager retains the running-game lock and applies pending SE after the recorded process exits", async t => {
  const { manager, updater } = await fixture(t);
  let alive = true;
  manager.processIsAlive = () => alive;
  manager.checkSessionProcess = async () => true;
  await fs.writeFile(path.join(manager.dataRoot, "game-session.json"), JSON.stringify({ pid: 12345 }));
  await manager.restoreGameSession();
  assert.equal(manager.gameLocked, true);
  await manager.ensureSystemMods();
  await updater.update(update);
  assert.equal(await version(updater), old);
  assert.equal(updater.status().status, "pending");
  alive = false;
  await new Promise(r => setTimeout(r, 2100));
  await until(() => updater.status().status === "updated");
  assert.equal(manager.gameLocked, false);
  assert.equal(await version(updater), newer);
});

test("a fully swapped but uncommitted SE update is recovered without downgrading", async t => {
  const { manager, updater } = await fixture(t);
  const backup = { folder: "backup-11111111-1111-1111-1111-111111111111", version: old };
  await updater.save({ transaction: { fromVersion: old, toVersion: newer, backup } });
  await fs.rename(updater.target, updater.folder(backup.folder));
  await writeMod(updater.target, newer);
  const reopened = new SECoreUpdater(manager, updater.options); await reopened.initialize();
  await manager.ensureSystemMods(false);
  assert.equal(await version(reopened), newer);
  assert.equal(reopened.status().backupVersion, old);
});

test("a reused PID for a different executable does not lock a reopened manager", async t => {
  const { manager } = await fixture(t);
  manager.processIsAlive = () => true;
  manager.checkSessionProcess = async () => false;
  await fs.writeFile(path.join(manager.dataRoot, "game-session.json"), JSON.stringify({ pid: 12345, executable: "old-game.exe" }));
  await manager.restoreGameSession();
  assert.equal(manager.gameLocked, false);
});

test("archive import allows plugin files only and rejects unsafe paths, collisions and links", async t => {
  const { root } = await fixture(t);
  const create = async (extra, folder) => {
    const zip = new AdmZip(); zip.addFile("BepInEx/plugins/000shcdese/SHCDESE.dll", Buffer.from("core"));
    for (const [name, content] of extra) zip.addFile(name, Buffer.from(content));
    const archive = path.join(root, folder + ".zip"); zip.writeZip(archive);
    return extractSE(archive, path.join(root, folder));
  };
  await create([["BepInEx/core/BepInEx.dll", "skip"], ["BepInEx/config/test.cfg", "skip"], ["install.bat", "skip"], ["msvcp140.dll", "keep"]], "good");
  assert.deepEqual((await fs.readdir(path.join(root, "good/BepInEx"))), ["plugins"]);
  await assert.rejects(create([["BepInEx/plugins/x/data:ads", "bad"]], "unsafe"), /unsafe/i);
  await assert.rejects(create([["bepinex/plugins/000shcdese/shcdese.dll", "duplicate"]], "duplicate"), /duplicate/i);
  await assert.rejects(create([["BepInEx/plugins/SCDEMultiplayerCompatibility/a.dll", "bad"]], "overlap"), /overlaps/);
});

test("download retries ten times, rejects non-official assets and never retains partial files", async t => {
  const { root } = await fixture(t); const file = path.join(root, "download.zip"); let calls = 0;
  await assert.rejects(downloadSE(update, file, { retryDelayMs: 0, fetch: async () => { calls++; throw new Error("offline"); } }), /offline/);
  assert.equal(calls, 10);
  await assert.rejects(fs.stat(file), /ENOENT/);
  await assert.rejects(downloadSE({ ...update, downloadUrl: "https://evil.example/a.zip" }, file), /official/);
  await assert.rejects(downloadSE(update, file, { fetch: async () => new Response("", { headers: { "content-length": String(129 * 1024 * 1024) } }) }), /limit/);
  const digest = await downloadSE(update, file, { fetch: async () => new Response("a valid downloaded stream") });
  assert.equal(digest.length, 64);
  assert.equal(await fs.readFile(file, "utf8"), "a valid downloaded stream");
});
