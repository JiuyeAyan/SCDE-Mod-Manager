const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const { checkManagerUpdate, prepareManagerUpdate, MANAGER_WORKSHOP_ID, PORTABLE_NAME } = require("../src/core/manager-updates");
const { ModManager } = require("../src/core/manager");

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-self-update-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const source = path.join(root, "steamapps/workshop/content/3024040", MANAGER_WORKSHOP_ID, PORTABLE_NAME);
  const target = path.join(root, "portable", PORTABLE_NAME);
  await fs.mkdir(path.dirname(source), { recursive: true });
  await fs.mkdir(path.dirname(target));
  await fs.writeFile(source, "new manager");
  await fs.writeFile(target, "old manager");
  const readVersion = async file => ({ product: "SCDE Mod Manager", version: (await fs.readFile(file, "utf8")) === "old manager" ? "0.2.4" : "0.2.5" });
  return { root, source, target, readVersion };
}

test("manager check reads only the fixed EXE in its own Workshop item; no content hashing or execution", async t => {
  const { root, source, readVersion } = await fixture(t);
  const inspected = [];
  const update = await checkManagerUpdate("0.2.4", "", { libraries: [root], readVersion: async file => { inspected.push(file); return readVersion(file); } });
  assert.deepEqual(inspected, [source]);
  assert.equal(update.version, "0.2.5");
  assert.equal(update.source, source);
  assert.equal(MANAGER_WORKSHOP_ID, "3796682182");
});

test("missing, invalid, same and older manager releases do not offer an update", async t => {
  const { root } = await fixture(t);
  for (const info of [{ product: "Other app", version: "99" }, { product: "SCDE Mod Manager", version: "0.2.4" }, { product: "SCDE Mod Manager", version: "0.1.16" }, { product: "SCDE Mod Manager", version: "not-a-version" }]) {
    assert.equal(await checkManagerUpdate("0.2.4", "", { libraries: [root], readVersion: async () => info }), null);
  }
  assert.equal(await checkManagerUpdate("0.2.4", "", { libraries: [path.join(root, "missing")] }), null);
});

test("confirmed preparation snapshots the selected version without modifying the installed manager or Workshop", async t => {
  const { root, source, target, readVersion } = await fixture(t);
  const update = await checkManagerUpdate("0.2.4", "", { libraries: [root], readVersion });
  const job = await prepareManagerUpdate(update, target, { readVersion });
  const record = JSON.parse(await fs.readFile(path.join(job, "update.json"), "utf8"));
  assert.equal(record.target, target);
  assert.equal(record.version, "0.2.5");
  assert.match(record.newHash, /^[a-f0-9]{64}$/);
  assert.match(record.oldHash, /^[a-f0-9]{64}$/);
  assert.equal(await fs.readFile(target, "utf8"), "old manager");
  assert.equal(await fs.readFile(source, "utf8"), "new manager");
  assert.equal(await fs.readFile(path.join(job, "new.exe"), "utf8"), "new manager");
});

test("a changed candidate or a non-manager target is not accepted for replacement", async t => {
  const { root, source, target, readVersion } = await fixture(t);
  const update = await checkManagerUpdate("0.2.4", "", { libraries: [root], readVersion });
  await fs.appendFile(source, " changed after notification");
  await assert.rejects(prepareManagerUpdate(update, target, { readVersion }), /CHANGED/);
  const fresh = await checkManagerUpdate("0.2.4", "", { libraries: [root], readVersion });
  await assert.rejects(prepareManagerUpdate(fresh, target, { readVersion: async () => ({ product: "Other app", version: "0.2.5" }) }), /INVALID/);
});

test("self-update holds the mutation lock through shutdown and never drains pending SE work", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(path.join(root, "data"));
  let idleCalls = 0;
  manager.onIdle = () => idleCalls++;
  manager.setGameLocked(true);
  await assert.rejects(manager.withModMutation(async () => {}), /GAME_RUNNING/);
  manager.setGameLocked(false);
  idleCalls = 0;
  await manager.withModMutation(async () => { manager.selfUpdateExiting = true; });
  assert.equal(manager.mutationBusy, true);
  assert.equal(idleCalls, 0);
  await assert.rejects(manager.launch(), /MANAGER_BUSY/);
  await assert.rejects(manager.withModMutation(async () => {}), /MANAGER_BUSY/);
});

test("update IPC prepares only after explicit acceptance and never accepts renderer-supplied paths", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(path.join(root, "ipc-data")); await manager.initialize();
  const handlers = new Map(), actions = [];
  const vm = require("node:vm");
  const selected = { source: "fixed-workshop-candidate.exe", version: "0.2.6" };
  const core = {
    checkManagerUpdate: async () => selected,
    prepareManagerUpdate: async (update, target) => { actions.push(["prepare", update, target]); return root; },
    startManagerUpdate: async job => actions.push(["helper", job]),
  };
  const context = { module: { exports: {} }, setImmediate: callback => callback(), require: name => {
    if (name === "electron") return { ipcMain: { handle: (id, handler) => handlers.set(id, handler) } };
    if (name === "./core/manager-updates") return core;
    if (name === "node:fs/promises") return { writeFile: async (file, value) => actions.push(["commit", file, value]) };
    return require(name);
  } };
  vm.runInNewContext(await fs.readFile(path.join(__dirname, "../src/manager-update.js"), "utf8"), context);
  context.module.exports.registerManagerUpdate(manager, { version: "0.2.5", portableFile: "outer-portable.exe", quit: () => actions.push(["quit"]) });
  await handlers.get("manager:check-update")();
  assert.equal(actions.length, 0);
  manager.setGameLocked(true);
  await assert.rejects(handlers.get("manager:install-update")({}, "attacker.exe"), /GAME_RUNNING/);
  assert.equal(actions.length, 0);
  manager.setGameLocked(false);
  await handlers.get("manager:install-update")({}, "attacker.exe");
  assert.deepEqual(actions[0], ["prepare", selected, "outer-portable.exe"]);
  assert.deepEqual(actions.map(item => item[0]), ["prepare", "helper", "commit", "quit"]);
  assert.equal(manager.mutationBusy, true);
});

test("a helper spawn failure is caught even while its log handle is closing", async () => {
  const vm = require("node:vm"), { EventEmitter } = require("node:events");
  const context = { module: { exports: {} }, process, __dirname: path.resolve(__dirname, "../src/core"), setTimeout, require: name => {
    if (name === "node:fs/promises") return { open: async () => ({ fd: 1, close: () => new Promise(resolve => setImmediate(resolve)) }) };
    if (name === "node:child_process") return { execFile() {}, spawn() {
      const child = new EventEmitter();
      queueMicrotask(() => child.emit("error", new Error("HELPER_ACCESS_DENIED")));
      return child;
    } };
    if (name === "./detect-game") return {};
    if (name === "./workshop-updates") return {};
    return require(name);
  } };
  vm.runInNewContext(await fs.readFile(path.join(__dirname, "../src/core/manager-updates.js"), "utf8"), context);
  await assert.rejects(context.module.exports.startManagerUpdate("fixture"), /HELPER_ACCESS_DENIED/);
});
