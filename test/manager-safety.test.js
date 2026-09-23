const assert = require("node:assert/strict");
const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const { EventEmitter } = require("node:events");
const AdmZip = require("adm-zip");
const { ModManager } = require("../src/core/manager");
const { GAME_EXECUTABLE } = require("../src/core/constants");
const { createCompatibilityProfile } = require("../src/core/compatibility-profile");

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manager-safety-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = path.join(root, "original-game");
  await fs.mkdir(gameDir);
  await fs.writeFile(path.join(gameDir, GAME_EXECUTABLE), "game fixture");
  return { root, gameDir };
}

async function write(file, content) {
  await fs.mkdir(path.dirname(file), { recursive: true });
  await fs.writeFile(file, content);
}

async function archive(root, id, version, files, extra = {}, name = id) {
  const zip = new AdmZip();
  zip.addFile("manifest.json", Buffer.from(JSON.stringify({ id, name: id, version, ...extra })));
  for (const [relative, content] of Object.entries(files)) zip.addFile("payload/" + relative, Buffer.from(content));
  const file = path.join(root, name + ".scdemod");
  zip.writeZip(file);
  return file;
}

test("public imports cannot replace a configured built-in component ID", async t => {
  const { root } = await fixture(t);
  const original = await archive(root, "bepinex-runtime", "5.4.23.5", { "winhttp.dll": "trusted runtime" }, {}, "bundled");
  const manager = new ModManager(path.join(root, "manager"), { systemPackages: [
    { id: "bepinex-runtime", version: "5.4.23.5", packagePath: original },
  ] });
  await manager.ensureSystemMods();
  const folder = path.join(manager.modsRoot, "bepinex-runtime");
  const before = await fs.readFile(path.join(folder, "manifest.json"), "utf8");
  const replacement = await archive(root, "bepinex-runtime", "99", { "winhttp.dll": "untrusted replacement" }, {}, "public");
  const result = await manager.installPackages([replacement]);
  assert.equal(result.imported.length, 0);
  assert.match(result.failures[0].error, /Reserved system package ID/);
  assert.equal(await fs.readFile(path.join(folder, "manifest.json"), "utf8"), before);
  assert.equal(await fs.readFile(path.join(folder, "payload/winhttp.dll"), "utf8"), "trusted runtime");
  assert.deepEqual(await fs.readdir(manager.modsRoot), ["bepinex-runtime"]);
});

test("ordinary packages cannot claim runtime and manager paths before replacing their old version", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(path.join(root, "manager"));
  const file = await archive(root, "example.mod", "1", { "BepInEx/plugins/example.mod/plugin.dll": "old plugin" });
  await manager.installPackage(file);
  const folder = path.join(manager.modsRoot, "example.mod");
  const before = await fs.readFile(path.join(folder, "manifest.json"), "utf8");
  for (const reserved of ["winhttp.dll", "BepInEx/core/other.dll", "BepInEx/patchers/other.dll",
    "BepInEx/plugins/000SHCDESE/Plugin.dll", "BepInEx/plugins/UUImGui/Plugin.dll",
    "BepInEx/plugins/SCDEMultiplayerCompatibility/Plugin.dll", "_scde_manager/active-mods.json"]) {
    await archive(root, "example.mod", "2", { [reserved]: "replacement" });
    const result = await manager.installPackages([file]);
    assert.equal(result.imported.length, 0, reserved);
    assert.match(result.failures[0].error, /Reserved system path/, reserved);
    assert.equal(await fs.readFile(path.join(folder, "manifest.json"), "utf8"), before);
    assert.equal(await fs.readFile(path.join(folder, "payload/BepInEx/plugins/example.mod/plugin.dll"), "utf8"), "old plugin");
  }
  assert.deepEqual(await fs.readdir(manager.modsRoot), ["example.mod"]);
});

test("manager checks consent hashes against imported archive bytes before the installation swap", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(path.join(root, "manager"));
  const file = await archive(root, "example.mod", "1", { "example.txt": "old" });
  await manager.installPackage(file);
  const manifestPath = path.join(manager.modsRoot, "example.mod/manifest.json");
  const before = await fs.readFile(manifestPath, "utf8");
  await archive(root, "example.mod", "1", { "example.txt": "approved revision" });
  const digest = crypto.createHash("sha256").update(await fs.readFile(file)).digest("hex");
  await archive(root, "example.mod", "1", { "example.txt": "changed since approval" });
  const result = await manager.installPackages([file], { expectedDigests: { [path.resolve(file)]: digest } });
  assert.equal(result.imported.length, 0);
  assert.equal(result.failures[0].code, "MOD_CHANGED_RESCAN");
  assert.equal(await fs.readFile(manifestPath, "utf8"), before);
  assert.equal(await fs.readFile(path.join(manager.modsRoot, "example.mod/payload/example.txt"), "utf8"), "old");
  const freshDigest = crypto.createHash("sha256").update(await fs.readFile(file)).digest("hex");
  assert.equal((await manager.installPackages([file], { expectedDigests: { [path.resolve(file)]: freshDigest } })).failures.length, 0);
});

test("manager activation enforces inclusive dependency ranges without silently enabling rejected Mods", async t => {
  const { root } = await fixture(t);
  const manager = new ModManager(path.join(root, "manager"));
  const runtime = await archive(root, "example.runtime", "2.6.0+build1", { "runtime.txt": "runtime" });
  await manager.installPackage(runtime);
  for (const [id, requirement, accepted] of [
    ["valid.mod", { minimumVersion: "2.6.0", maximumVersion: "2.8.0" }, true],
    ["minimum.mod", { minimumVersion: "2.7.0" }, false],
    ["maximum.mod", { maximumVersion: "2.5.0" }, false],
    ["exact.mod", { version: "2.6.0" }, false],
  ]) {
    const file = await archive(root, id, "1", { [id + ".txt"]: id }, { dependencies: [{ id: "example.runtime", ...requirement }] });
    const result = await manager.installPackages([file]);
    assert.equal(result.failures.length, 0);
    assert.equal(result.activationFailures.length, accepted ? 0 : 1, id);
    assert.equal(result.state.enabledMods.includes(id), accepted, id);
    if (!accepted) await assert.rejects(manager.setEnabled(id, true), /example\.runtime/);
  }
  const state = await manager.getState();
  assert.ok(state.enabledMods.indexOf("example.runtime") < state.enabledMods.indexOf("valid.mod"));
});

test("local deployment receipt changes with order or archive hash while network identity stays stable", async t => {
  const { root, gameDir } = await fixture(t);
  const manager = new ModManager(path.join(root, "manager"));
  await manager.setGameDirectory(gameDir);
  for (const id of ["first.mod", "second.mod"]) {
    await manager.installPackage(await archive(root, id, "1", { [id + ".txt"]: id }));
  }
  await manager.prepareStage();
  const initial = await manager.getState();
  const baseline = createCompatibilityProfile(initial.mods, initial.enabledMods);
  const reordered = createCompatibilityProfile(initial.mods, [...initial.enabledMods].reverse());
  assert.equal(baseline.fingerprint, reordered.fingerprint);
  assert.notEqual(baseline.deploymentFingerprint, reordered.deploymentFingerprint);
  await manager.configStore.update(config => ({ ...config, enabledMods: [...config.enabledMods].reverse() }));
  assert.equal((await manager.getState()).stageReady, false);
  await manager.configStore.update(config => ({ ...config, enabledMods: initial.enabledMods }));
  assert.equal((await manager.getState()).stageReady, true);
  const manifestPath = path.join(manager.modsRoot, "first.mod/manifest.json");
  const manifest = JSON.parse(await fs.readFile(manifestPath, "utf8"));
  manifest.packageSha256 = "f".repeat(64);
  await fs.writeFile(manifestPath, JSON.stringify(manifest));
  const changed = await manager.getState();
  assert.equal(changed.stageReady, false);
  const revision = createCompatibilityProfile(changed.mods, changed.enabledMods);
  assert.equal(baseline.fingerprint, revision.fingerprint);
  assert.notEqual(baseline.deploymentFingerprint, revision.deploymentFingerprint);
});

test("an old stage rebuilds once with preserved config and no original BepInEx contamination, then uses the fast path", async t => {
  const { root, gameDir } = await fixture(t);
  await write(path.join(gameDir, "winhttp.dll"), "foreign original loader");
  await write(path.join(gameDir, "BepInEx/plugins/foreign/plugin.dll"), "foreign plugin");
  await write(path.join(gameDir, "BepInEx/config/User.cfg"), "original config");
  const children = [];
  const manager = new ModManager(path.join(root, "manager"), { isSteamRunning: async () => true,
    spawn: () => {
      const child = new EventEmitter(); child.pid = 24000 + children.length; child.unref = () => {};
      children.push(child); process.nextTick(() => child.emit("spawn")); return child;
    } });
  await manager.setGameDirectory(gameDir);
  await manager.installPackage(await archive(root, "example.mod", "1", { "BepInEx/plugins/example.mod/plugin.dll": "managed plugin" }));
  await manager.prepareStage();
  const stage = manager.stageDir;
  await write(path.join(stage, "BepInEx/config/User.cfg"), "player settings");
  await write(path.join(stage, "winhttp.dll"), "previous contaminated loader");
  await write(path.join(stage, "BepInEx/plugins/foreign/plugin.dll"), "previous contaminated plugin");
  await manager.configStore.update(config => { delete config.stageIsolationVersion; return config; });
  assert.equal((await manager.getState()).stageValid, true);
  assert.equal((await manager.getState()).stageReady, false);
  const prepare = manager.prepareStage.bind(manager);
  let rebuilds = 0;
  manager.prepareStage = async () => { rebuilds++; return prepare(); };
  await manager.launch();
  assert.equal(rebuilds, 1);
  assert.equal(await fs.readFile(path.join(stage, "BepInEx/config/User.cfg"), "utf8"), "player settings");
  await assert.rejects(fs.access(path.join(stage, "winhttp.dll")), { code: "ENOENT" });
  await assert.rejects(fs.access(path.join(stage, "BepInEx/plugins/foreign/plugin.dll")), { code: "ENOENT" });
  assert.equal(await fs.readFile(path.join(gameDir, "winhttp.dll"), "utf8"), "foreign original loader");
  assert.equal(await fs.readFile(path.join(gameDir, "BepInEx/config/User.cfg"), "utf8"), "original config");
  assert.ok((await manager.configStore.load()).stageIsolationVersion > 0);
  children[0].emit("exit", 0);
  const payload = path.join(stage, "BepInEx/plugins/example.mod/plugin.dll");
  const fixed = new Date("2001-02-03T04:05:06.000Z"); await fs.utimes(payload, fixed, fixed);
  manager.prepareStage = async () => assert.fail("a verified current stage must not be rebuilt");
  manager.redeploy = async () => assert.fail("a verified current stage must not redeploy payloads");
  const next = await manager.launch();
  assert.equal(next.state.stageReady, true);
  assert.equal(children.length, 2);
  assert.equal((await fs.stat(payload)).mtimeMs, fixed.getTime());
});
