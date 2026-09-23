const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const { GAME_EXECUTABLE } = require("../src/core/constants");
const { prepareStage, applyMods, resolveStageDirectory } = require("../src/core/deployer");
const { ModManager } = require("../src/core/manager");

async function write(root, relative, contents = "fixture") {
  const file = path.join(root, relative);
  await fs.mkdir(path.dirname(file), { recursive: true });
  await fs.writeFile(file, contents);
}
const read = (root, relative) => fs.readFile(path.join(root, relative), "utf8");
const absent = (root, relative) => assert.rejects(fs.access(path.join(root, relative)), { code: "ENOENT" });

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-deploy-safety-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const gameDir = path.join(root, "original");
  await write(gameDir, GAME_EXECUTABLE, "fake game");
  await write(gameDir, "data/rules.dat", "vanilla rules");
  const options = { gameDir, stageDir: resolveStageDirectory(gameDir), mods: [], enabledIds: [], previousActiveFiles: [] };
  async function mod(id, files, metadata = {}) {
    const record = { id, name: id, version: "1.0", folder: path.join(root, "mods", id), ...metadata };
    await fs.mkdir(path.join(record.folder, "payload"), { recursive: true });
    for (const [relative, value] of Object.entries(files)) await write(path.join(record.folder, "payload"), relative, value);
    options.mods.push(record); return record;
  }
  return { root, options, mod };
}

test("dirty original loaders and BepInEx are neither cloned nor restored when managed files are disabled", async t => {
  const { options, mod } = await fixture(t);
  await mod("bepinex-runtime", { "winhttp.dll": "managed loader", "doorstop_config.ini": "managed config",
    "BepInEx/core/BepInEx.dll": "managed runtime" });
  await mod("shcde-script-extender", { "msvcp140.dll": "managed native", "BepInEx/plugins/000shcdese/SHCDESE.dll": "managed SE" });
  for (const file of ["winhttp.dll", "doorstop_config.ini", ".doorstop_version", "msvcp140.dll",
    "BepInEx/core/BepInEx.dll", "BepInEx/plugins/unmanaged/plugin.dll", "BepInEx/config/unmanaged.cfg",
    "_scde_manager/active-profile.json"]) await write(options.gameDir, file, "dirty original");
  options.enabledIds = ["bepinex-runtime", "shcde-script-extender"];
  const initial = await prepareStage(options);
  assert.equal(await read(options.stageDir, "winhttp.dll"), "managed loader");
  assert.equal(await read(options.stageDir, "BepInEx/core/BepInEx.dll"), "managed runtime");
  assert.equal(await read(options.stageDir, "data/rules.dat"), "vanilla rules");
  for (const file of [".doorstop_version", "BepInEx/plugins/unmanaged/plugin.dll", "BepInEx/config/unmanaged.cfg",
    "_scde_manager/active-profile.json"]) await absent(options.stageDir, file);
  await applyMods({ ...options, enabledIds: [], previousActiveFiles: initial.activeFiles });
  for (const file of initial.activeFiles) await absent(options.stageDir, file);
  assert.equal(await read(options.gameDir, "winhttp.dll"), "dirty original", "original installation stays read-only");
});

test("disabled installed system packages reserve their actual paths before rebuild or redeployment writes", async t => {
  const { options, mod } = await fixture(t);
  options.systemIds = ["extra-runtime"];
  await mod("extra-runtime", { "vendor/support.bin": "system file", "BepInEx/plugins/NewSystem/core.dll": "system plugin" });
  await prepareStage(options);
  await write(options.stageDir, "data/rules.dat", "unchanged stage");
  await write(options.stageDir, "keep.txt", "stage sentinel");
  const intruder = await mod("ordinary.mod", { "aaa-before-check.txt": "must not write", "vendor/support.bin": "overwrite" });
  options.enabledIds = [intruder.id];
  for (const operation of [prepareStage, applyMods]) {
    await assert.rejects(operation({ ...options, previousActiveFiles: ["data/rules.dat"] }), /Reserved system path/i);
    assert.equal(await read(options.stageDir, "data/rules.dat"), "unchanged stage");
    assert.equal(await read(options.stageDir, "keep.txt"), "stage sentinel");
    await absent(options.stageDir, "aaa-before-check.txt");
  }
  await fs.rm(path.join(intruder.folder, "payload/vendor/support.bin"));
  await write(path.join(intruder.folder, "payload"), "BepInEx/plugins/NewSystem/not-in-system-package.dll", "overwrite folder");
  await assert.rejects(applyMods(options), /Reserved system path/i);
});

test("reserved paths also reject ordinary files at their ancestors or below reserved files", async t => {
  for (const relative of ["BepInEx", "BepInEx/plugins", "vendor", "winhttp.dll/fake.json"]) {
    await t.test(relative, async child => {
      const { options, mod } = await fixture(child);
      options.systemIds = ["extra-runtime"];
      await mod("extra-runtime", { "vendor/support.bin": "reserved file" });
      await prepareStage(options);
      await write(options.stageDir, "keep.txt", "sentinel before rejection");
      await write(options.stageDir, "data/rules.dat", "unchanged stage rules");
      const intruder = await mod("ordinary.mod", { [relative]: "not a permitted overlay" });
      options.enabledIds = [intruder.id];
      for (const operation of [prepareStage, applyMods]) {
        await assert.rejects(operation({ ...options, previousActiveFiles: ["data/rules.dat"] }), /Reserved system path/i);
        assert.equal(await read(options.stageDir, "keep.txt"), "sentinel before rejection");
        assert.equal(await read(options.stageDir, "data/rules.dat"), "unchanged stage rules");
      }
    });
  }
});

test("ordinary Mod conflicts keep the existing enabled-order last-wins behavior", async t => {
  const { options, mod } = await fixture(t);
  await mod("first.mod", { "data/rules.dat": "first" });
  await mod("second.mod", { "data/rules.dat": "second" });
  options.enabledIds = ["first.mod", "second.mod"];
  const deployed = await prepareStage(options);
  assert.equal(await read(options.stageDir, "data/rules.dat"), "second");
  assert.deepEqual(deployed.conflicts, [{ path: "data/rules.dat", overwritten: "first.mod", winner: "second.mod" }]);
  await applyMods({ ...options, enabledIds: ["second.mod", "first.mod"], previousActiveFiles: deployed.activeFiles });
  assert.equal(await read(options.stageDir, "data/rules.dat"), "first");
});

test("declared user data and config survive rebuild without preserving undeclared plugin caches or stale DLLs", async t => {
  const { options, mod } = await fixture(t);
  const owner = await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "version one" }, {
    persistentPaths: { version: 1, paths: ["BepInEx/plugins/Example/player-data"] },
  });
  options.enabledIds = [owner.id];
  await prepareStage(options);
  await write(options.stageDir, "BepInEx/config/modmanager/lang/custom.json", "player translation");
  await write(options.stageDir, "BepInEx/plugins/Example/player-data/preferences.json", "player preferences");
  await write(options.stageDir, "BepInEx/plugins/Example/cache/transient.json", "discard");
  await write(options.stageDir, "BepInEx/plugins/Example/obsolete.dll", "discard");
  await write(options.stageDir, "BepInEx/cache/chainloader_typeloader.dat", "discard");
  await write(path.join(owner.folder, "payload"), "BepInEx/plugins/Example/plugin.dll", "version two");
  await prepareStage(options);
  assert.equal(await read(options.stageDir, "BepInEx/config/modmanager/lang/custom.json"), "player translation");
  assert.equal(await read(options.stageDir, "BepInEx/plugins/Example/player-data/preferences.json"), "player preferences");
  assert.equal(await read(options.stageDir, "BepInEx/plugins/Example/plugin.dll"), "version two");
  for (const relative of ["BepInEx/plugins/Example/cache/transient.json", "BepInEx/plugins/Example/obsolete.dll",
    "BepInEx/cache/chainloader_typeloader.dat"]) await absent(options.stageDir, relative);
});

test("a failed user-data restore retains the saved bytes and reports their recovery directory", async t => {
  const { root, options, mod } = await fixture(t);
  const relative = "BepInEx/plugins/Example/preferences.json";
  await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "plugin" }, {
    persistentPaths: { version: 1, paths: [relative] },
  });
  options.enabledIds = ["example.mod"];
  await prepareStage(options);
  await write(options.stageDir, relative, "irreplaceable player settings");
  const copyFile = fs.copyFile;
  let failedRestore = false;
  fs.copyFile = async function(source, destination, ...args) {
    if (path.resolve(destination) === path.resolve(options.stageDir, relative) &&
        path.relative(root, source).startsWith("scdemm-data-backup-")) {
      failedRestore = true;
      throw new Error("simulated restore write failure");
    }
    return copyFile.call(this, source, destination, ...args);
  };
  let failure;
  try { await assert.rejects(prepareStage(options), error => { failure = error; return /simulated restore write failure/.test(error.message); }); }
  finally { fs.copyFile = copyFile; }
  assert.equal(failedRestore, true, "failure was injected after the original stage had been rebuilt");
  const backups = (await fs.readdir(root)).filter(name => name.startsWith("scdemm-data-backup-"));
  assert.equal(backups.length, 1);
  const recovery = path.join(root, backups[0]);
  assert.ok(failure.message.includes(`User-data recovery backup retained at ${recovery}`));
  assert.equal(await read(recovery, relative), "irreplaceable player settings");
  await absent(options.stageDir, relative);
  assert.equal(await read(options.stageDir, "BepInEx/plugins/Example/plugin.dll"), "plugin");
});

test("a partial redeploy of an unchanged profile cannot leave the old receipt claiming the stage is ready", async t => {
  const { root, options, mod } = await fixture(t);
  const pluginFile = "BepInEx/plugins/Example/plugin.dll";
  await mod("bepinex-runtime", { "winhttp.dll": "managed loader", "BepInEx/core/BepInEx.dll": "runtime" });
  await mod("example.mod", { [pluginFile]: "managed plugin" });
  for (const record of options.mods) await write(record.folder, "manifest.json", JSON.stringify(record));
  const manager = new ModManager(root);
  await manager.initialize();
  await manager.setGameDirectory(options.gameDir);
  await manager.configStore.update(config => ({ ...config, enabledMods: ["bepinex-runtime", "example.mod"] }));
  await manager.prepareStage();
  assert.equal((await manager.getState()).stageReady, true);
  const copyFile = fs.copyFile;
  fs.copyFile = async function(source, destination, ...args) {
    if (path.resolve(destination) === path.resolve(options.stageDir, pluginFile)) throw new Error("simulated plugin copy failure");
    return copyFile.call(this, source, destination, ...args);
  };
  try { await assert.rejects(manager.redeploy(), /simulated plugin copy failure/); }
  finally { fs.copyFile = copyFile; }
  assert.equal(await read(options.stageDir, "winhttp.dll"), "managed loader", "the lightweight probe was already redeployed");
  await absent(options.stageDir, pluginFile);
  assert.equal((await manager.getState()).stageReady, false, "a leftover receipt must not certify a partial deployment");
  await manager.redeploy();
  assert.equal((await manager.getState()).stageReady, true);
  assert.equal(await read(options.stageDir, pluginFile), "managed plugin");
});

test("legacy SE settings follow each DLL directory and only Rawra's fixes GUID receives the whitelist migration", async t => {
  const { options, mod } = await fixture(t);
  await mod("se.pack", {
    "BepInEx/plugins/Pack/first/plugin.dll": "first", "BepInEx/plugins/Pack/second/plugin.dll": "second",
    "BepInEx/plugins/Pack/fixes/fixes.dll": "fixes",
    "BepInEx/plugins/Pack/fixes/info.json": JSON.stringify({ GUID: "fixes", Name: "Renamed display title" }),
    "BepInEx/plugins/Pack/impostor/info.json": JSON.stringify({ GUID: "different", Name: "Rawra's Fixes" }),
  }, { dependencies: [{ id: "shcde-script-extender" }] });
  options.enabledIds = ["se.pack"];
  await prepareStage(options);
  const retained = ["BepInEx/plugins/Pack/first/LobbyModSettings/lobby.msgpack",
    "BepInEx/plugins/Pack/second/LobbyModSettings/preset.bin", "BepInEx/plugins/Pack/fixes/data/hopsFarmWhitelist.json"];
  const discarded = ["BepInEx/plugins/Pack/first/LobbyModSettings/unrelated.json",
    "BepInEx/plugins/Pack/first/LobbyModSettings/injected.dll", "BepInEx/plugins/Pack/no-dll/LobbyModSettings/preset.bin",
    "BepInEx/plugins/Pack/impostor/data/hopsFarmWhitelist.json", "BepInEx/plugins/Pack/fixes/data/other-cache.json"];
  for (const relative of [...retained, ...discarded]) await write(options.stageDir, relative, relative);
  await prepareStage(options);
  for (const relative of retained) assert.equal(await read(options.stageDir, relative), relative);
  for (const relative of discarded) await absent(options.stageDir, relative);
});

test("a payload collision with persistent player data rejects rebuild and apply before any mutation", async t => {
  const { options, mod } = await fixture(t);
  const owner = await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "plugin" }, {
    persistentPaths: { version: 1, paths: ["BepInEx/plugins/Example/preferences.json"] },
  });
  options.enabledIds = [owner.id];
  await prepareStage(options);
  await write(options.stageDir, "BepInEx/plugins/Example/preferences.json", "live preferences");
  await write(options.stageDir, "data/rules.dat", "stage sentinel");
  await write(path.join(owner.folder, "payload"), "BepInEx/plugins/Example/preferences.json", "package replacement");
  await write(path.join(owner.folder, "payload"), "aaa-before-check.txt", "must not write");
  for (const operation of [prepareStage, applyMods]) {
    await assert.rejects(operation({ ...options, previousActiveFiles: ["data/rules.dat"] }), /collides with persistent user data/i);
    assert.equal(await read(options.stageDir, "BepInEx/plugins/Example/preferences.json"), "live preferences");
    assert.equal(await read(options.stageDir, "data/rules.dat"), "stage sentinel");
    await absent(options.stageDir, "aaa-before-check.txt");
  }
});

test("persistent declarations cannot claim another plugin's folder or executable files", async t => {
  const { options, mod } = await fixture(t);
  const owner = await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "plugin" });
  options.enabledIds = [owner.id];
  await prepareStage(options);
  await write(options.stageDir, "keep.txt", "sentinel");
  for (const relative of ["BepInEx/plugins/Other/settings.json", "BepInEx/plugins/Example/plugin.dll",
    "BepInEx/plugins/Example/Scripts/init.lua", "BepInEx/plugins/Example/Scripts/init.luac"]) {
    owner.persistentPaths = { version: 1, paths: [relative] };
    await assert.rejects(prepareStage(options), /Persistent path must be user data/i);
    assert.equal(await read(options.stageDir, "keep.txt"), "sentinel");
  }
});

test("declaring a data folder cannot restore executable SE Lua files hidden inside that folder", async t => {
  const { options, mod } = await fixture(t);
  const owner = await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "plugin" });
  options.enabledIds = [owner.id];
  await prepareStage(options);
  await write(options.stageDir, "keep.txt", "sentinel");
  await write(options.stageDir, "BepInEx/plugins/Example/Scripts/init.lua", "old executable Lua code");
  owner.persistentPaths = { version: 1, paths: ["BepInEx/plugins/Example/Scripts"] };
  await assert.rejects(prepareStage(options), /Executable file cannot be restored as persistent user data/i);
  assert.equal(await read(options.stageDir, "keep.txt"), "sentinel");
  assert.equal(await read(options.stageDir, "BepInEx/plugins/Example/Scripts/init.lua"), "old executable Lua code");
});

async function junctionOrSkip(t, target, link) {
  try { await fs.symlink(target, link, process.platform === "win32" ? "junction" : "dir"); return true; }
  catch (error) {
    if (["EPERM", "EACCES", "ENOTSUP"].includes(error.code)) { t.skip("This account cannot create test links"); return false; }
    throw error;
  }
}

test("linked persistent data is rejected before rebuild and cannot capture external files", async t => {
  const { root, options, mod } = await fixture(t);
  await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "plugin" }, {
    persistentPaths: { version: 1, paths: ["BepInEx/plugins/Example/player-data"] },
  });
  options.enabledIds = ["example.mod"];
  await prepareStage(options);
  const external = path.join(root, "external");
  await write(external, "private.json", "external data");
  if (!await junctionOrSkip(t, external, path.join(options.stageDir, "BepInEx/plugins/Example/player-data"))) return;
  await assert.rejects(prepareStage(options), /Linked.*path|symbolic|符号链接/i);
  assert.equal(await read(external, "private.json"), "external data");
  assert.equal(await read(options.stageDir, "BepInEx/plugins/Example/plugin.dll"), "plugin");
});

test("linked payload and linked deployment destinations never write outside the game copy", async t => {
  const { root, options, mod } = await fixture(t);
  const owner = await mod("example.mod", { "BepInEx/plugins/Example/plugin.dll": "plugin" });
  options.enabledIds = [owner.id];
  await prepareStage(options);
  const external = path.join(root, "external");
  await write(external, "private.json", "external data");
  const payloadLink = path.join(owner.folder, "payload/linked");
  if (!await junctionOrSkip(t, external, payloadLink)) return;
  await assert.rejects(applyMods(options), /Linked.*path|symbolic|符号链接/i);
  await fs.unlink(payloadLink);
  await fs.rm(path.join(options.stageDir, "BepInEx/plugins/Example"), { recursive: true });
  if (!await junctionOrSkip(t, external, path.join(options.stageDir, "BepInEx/plugins/Example"))) return;
  await assert.rejects(applyMods(options), /Linked.*path|symbolic|符号链接/i);
  assert.equal(await read(external, "private.json"), "external data");
  await absent(external, "plugin.dll");
});
