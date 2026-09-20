const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const { ModManager } = require("../src/core/manager");
const { SECoreUpdater } = require("../src/core/se-core-updater");
const { GAME_EXECUTABLE } = require("../src/core/constants");
const SE = "shcde-script-extender";

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-toggle-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const specs = [
    { id: "bepinex-runtime", version: "5.4.23.5" },
    { id: SE, version: "2.6.0+scdemm.1" },
    { id: "scde-multiplayer-compatibility", version: "0.3.1" },
    { id: "direct", version: "1", dependencies: [{ id: SE }] },
    { id: "nested", version: "1", dependencies: [{ id: "direct" }] },
    { id: "independent", version: "1" },
  ];
  const options = { systemPackages: specs.slice(0, 3) };
  const manager = new ModManager(path.join(root, "manager"), options);
  await manager.initialize();
  for (const spec of specs) {
    const folder = path.join(manager.modsRoot, spec.id);
    await fs.mkdir(path.join(folder, "payload/BepInEx/plugins", spec.id), { recursive: true });
    await fs.writeFile(path.join(folder, "payload/BepInEx/plugins", spec.id, "plugin.dll"), spec.id);
    await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify({ name: spec.id, ...spec }));
  }
  await manager.configStore.update(c => ({ ...c, enabledMods: specs.map(s => s.id) }));
  const game = path.join(root, "game"); await fs.mkdir(game);
  await fs.writeFile(path.join(game, GAME_EXECUTABLE), "fake");
  await manager.setGameDirectory(game); await manager.prepareStage();
  return { root, manager, options };
}

test("SE off cascades through dependencies, leaves independent Mods, and persists through reopen", async t => {
  const { root, manager, options } = await fixture(t);
  const state = await manager.setEnabled(SE, false);
  assert.deepEqual(state.disabledDependents.sort(), ["direct", "nested"]);
  assert.ok(state.enabledMods.includes("independent"));
  for (const id of [SE, "direct", "nested"]) {
    assert.ok(!state.enabledMods.includes(id));
    await assert.rejects(fs.access(path.join(manager.stageDir, "BepInEx/plugins", id, "plugin.dll")), { code: "ENOENT" });
  }
  const reopened = new ModManager(manager.dataRoot, options);
  const restored = await reopened.ensureSystemMods();
  assert.deepEqual(restored.enabledMods, state.enabledMods);
  assert.equal(restored.mods.find(mod => mod.id === SE).required, true);
  await assert.rejects(reopened.removeMod(SE));
  await assert.rejects(reopened.moveMod(SE, 1));
  await assert.rejects(fs.access(path.join(root, "game/BepInEx")), { code: "ENOENT" });
});

test("enabling an SE-dependent Mod requires confirmation and cancellation changes nothing", async t => {
  const { manager } = await fixture(t);
  await manager.setEnabled(SE, false);
  let asked;
  manager.confirmEnableSE = async name => { asked = name; return false; };
  assert.equal(await manager.setEnabled("nested", true), null);
  assert.equal(asked, "nested");
  assert.ok(!(await manager.getState()).enabledMods.includes(SE));
  manager.confirmEnableSE = async () => true;
  const state = await manager.setEnabled("nested", true);
  for (const id of [SE, "direct", "nested"]) assert.ok(state.enabledMods.includes(id));
  assert.equal((await manager.configStore.load()).seEnabled, true);
  manager.gameLocked = true;
  await assert.rejects(manager.setEnabled(SE, false), /GAME_RUNNING/);
});

test("SE auto-update retains backups but never enables a disabled SE or its Mods", async t => {
  const { manager } = await fixture(t);
  await manager.setEnabled(SE, false);
  const updater = new SECoreUpdater(manager, { prepare: async (_, folder) => {
    await fs.mkdir(path.join(folder, "payload"));
    const manifest = { id: SE, name: "SE", version: "2.8.0+scdemm.1" };
    await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify(manifest)); return manifest;
  } });
  await updater.initialize();
  updater.state.skippedVersion = "2.8.0";
  await updater.update({ version: "2.8.0" });
  assert.equal(updater.status().status, "updated");
  const state = await manager.ensureSystemMods();
  assert.equal(state.mods.find(mod => mod.id === SE).version, "2.8.0+scdemm.1");
  for (const id of [SE, "direct", "nested"]) assert.ok(!state.enabledMods.includes(id));
  assert.equal((await manager.configStore.load()).seEnabled, false);
});
