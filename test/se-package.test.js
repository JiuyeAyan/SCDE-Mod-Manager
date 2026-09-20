const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const Zip = require("adm-zip");
const { readPackageMetadata, scanWorkshop } = require("../src/core/workshop");
const { installModPackage, readInstalledMods } = require("../src/core/mod-package");
const { ModManager } = require("../src/core/manager");
const { findWorkshopUpdates } = require("../src/core/workshop-updates");

async function fixture(t, extension = ".map", files = {}) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-se-import-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const file = path.join(root, "steamapps/workshop/content/3024040/123/Example" + extension);
  await fs.mkdir(path.dirname(file), { recursive: true });
  const info = { GUID: "Example_SE", Name: "SE Example", Author: "Example Author", Version: "1.2.3", Manifest: extension === ".map" ? 1 : 0, NetworkMode: 1, VersionCheckUrl: "https://github.com/example/mod" };
  const zip = new Zip();
  zip.addFile("info.json", Buffer.from(JSON.stringify(info)));
  zip.addFile(extension === ".map" ? "BepInEx/plugins/Example_SE/mod.dll" : "Scripts/init.lua", Buffer.from("test content"));
  for (const [name, content] of Object.entries(files)) zip.addFile(name, Buffer.from(content));
  await fs.writeFile(file, Buffer.concat([extension === ".map" ? Buffer.alloc(2 * 1024 * 1024) : Buffer.alloc(0), zip.toBuffer()]));
  return { root, file, info };
}

test("SE map archives are discovered and installed with original plugin layout and metadata", async (t) => {
  const { root, file, info } = await fixture(t);
  const items = [];
  const result = await scanWorkshop([root], { onItem: item => items.push(item) });
  assert.equal(result.count, 1);
  assert.equal(items[0].name, info.Name);
  const modsRoot = path.join(root, "mods");
  const installed = await installModPackage(file, modsRoot);
  assert.equal(installed.id, items[0].id);
  const [mod] = await readInstalledMods(modsRoot);
  assert.equal(mod.scriptExtender.guid, info.GUID);
  assert.equal(mod.scriptExtender.versionCheckUrl, info.VersionCheckUrl);
  assert.deepEqual(mod.dependencies, [{ id: "shcde-script-extender", version: "" }]);
  const payload = path.join(mod.folder, "payload/BepInEx/plugins/Example_SE");
  assert.equal(await fs.readFile(path.join(payload, "mod.dll"), "utf8"), "test content");
  assert.equal(JSON.parse(await fs.readFile(path.join(payload, "info.json"), "utf8")).GUID, info.GUID);
  assert.equal((await fs.readdir(path.join(mod.folder, "payload"))).length, 1);
});

test("SE resource packages remain packed so embedded DLLs cannot become BepInEx plugins", async (t) => {
  const { root, file } = await fixture(t, ".semod");
  const mod = await installModPackage(file, path.join(root, "mods"));
  assert.deepEqual(await fs.readFile(path.join(root, "mods", mod.id, "payload/BepInEx/plugins/Example_SE.semod")), await fs.readFile(file));
});

test("SE 2.7 dependency bounds survive import and refresh previously imported metadata", async t => {
  const { root, file, info } = await fixture(t);
  const data = await fs.readFile(file), zip = new Zip(data.subarray(2 * 1024 * 1024));
  info.Dependencies = [{ GUID: "000shcdese", MinimumVersion: "2.7.2", MaximumVersion: "2.8.0" }];
  info.MinimumScriptExtenderVersion = "2.3.0";
  zip.updateFile("info.json", Buffer.from(JSON.stringify(info)));
  await fs.writeFile(file, Buffer.concat([data.subarray(0, 2 * 1024 * 1024), zip.toBuffer()]));
  const metadata = await readPackageMetadata(file);
  assert.equal(metadata.scriptExtender.minimumVersion, "2.7.2");
  assert.equal(metadata.scriptExtender.maximumVersion, "2.8.0");
  const modsRoot = path.join(root, "mods");
  await installModPackage(file, modsRoot);
  const manifestPath = path.join(modsRoot, metadata.id, "manifest.json");
  const previous = JSON.parse(await fs.readFile(manifestPath));
  previous.scriptExtender.minimumVersion = "2.3.0";
  previous.scriptExtender.maximumVersion = "";
  delete previous.scriptExtender.metadataVersion;
  await fs.writeFile(manifestPath, JSON.stringify(previous));
  const [installed] = await readInstalledMods(modsRoot);
  assert.equal(installed.scriptExtender.minimumVersion, "2.7.2");
  const { resolveEnabledMods } = require("../src/core/manager");
  assert.throws(() => resolveEnabledMods([installed, { id: "shcde-script-extender", version: "2.6.0+scdemm.1" }], [installed.id]), /2\.7\.2/);
  assert.equal(resolveEnabledMods([installed, { id: "shcde-script-extender", version: "2.7.2+scdemm.1" }], [installed.id]).length, 2);
  assert.equal(JSON.parse(await fs.readFile(manifestPath)).scriptExtender.minimumVersion, "2.3.0", "refresh must not edit the installed package");
});

test("SE import rejects runtime replacement and Windows unsafe paths", async (t) => {
  for (const invalid of ["BepInEx/core/BepInEx.dll", "BepInEx/plugins/000shcdese/replace.dll", "BepInEx/plugins/Example_SE/file.txt:stream", "BepInEx/plugins/Example_SE/AUX.txt"]) {
    const { root, file } = await fixture(t, ".map", { [invalid]: "unsafe" });
    await assert.rejects(installModPackage(file, path.join(root, "mods")), /unsafe|reserved|unsupported/i);
  }
});

test("SE listing reads archive metadata without reading the map prefix or payload", async (t) => {
  const { file } = await fixture(t);
  const nativeFs = require("node:fs");
  const read = nativeFs.read;
  let bytes = 0;
  const stub = t.mock.method(nativeFs, "read", (...args) => {
    const callback = args.pop();
    return read(...args, (error, count, buffer) => { bytes += count || 0; callback(error, count, buffer); });
  });
  assert.equal((await readPackageMetadata(file)).version, "1.2.3");
  assert.ok(bytes < 256 * 1024, `Read ${bytes} bytes`);
  stub.mock.restore();
  await fs.rename(file, file + ".moved");
});

test("SE defaults survive redeployment and updates retain disabled state while newer packages are detected", async (t) => {
  const { root, file } = await fixture(t, ".map", { "BepInEx/config/example.cfg": "default" });
  const manager = new ModManager(path.join(root, "manager"));
  const runtime = new Zip();
  runtime.addFile("manifest.json", Buffer.from(JSON.stringify({ id: "shcde-script-extender", name: "SE", version: "2.6.0" })));
  runtime.addFile("payload/BepInEx/plugins/000shcdese/SE.dll", Buffer.from("fixture"));
  const runtimePath = path.join(root, "runtime.scdemod"); await fs.writeFile(runtimePath, runtime.toBuffer());
  await manager.installPackages([runtimePath]);
  const result = await manager.installPackages([file]);
  assert.equal(result.failures.length, 0);
  assert.equal(result.activationFailures.length, 0);
  const id = result.imported[0].id;
  assert.ok(result.state.enabledMods.includes(id));
  const game = path.join(root, "original"); await fs.mkdir(game);
  await fs.writeFile(path.join(game, require("../src/core/constants").GAME_EXECUTABLE), "fixture");
  await manager.setGameDirectory(game); await manager.prepareStage();
  const config = path.join(manager.stageDir, "BepInEx/config/example.cfg");
  assert.equal(await fs.readFile(config, "utf8"), "default");
  await fs.writeFile(config, "player config"); await manager.redeploy();
  assert.equal(await fs.readFile(config, "utf8"), "player config");
  await manager.setEnabled(id, false);
  const data = await fs.readFile(file), zip = new Zip(data.subarray(2 * 1024 * 1024));
  const info = JSON.parse(zip.readAsText("info.json")); info.Version = "1.2.4";
  zip.updateFile("info.json", Buffer.from(JSON.stringify(info)));
  await fs.writeFile(file, Buffer.concat([data.subarray(0, 2 * 1024 * 1024), zip.toBuffer()]));
  assert.equal(findWorkshopUpdates(await readInstalledMods(manager.modsRoot), [await readPackageMetadata(file)]).length, 1);
  const updated = await manager.installPackages([file]);
  assert.equal(updated.failures.length, 0);
  assert.equal(updated.state.enabledMods.includes(id), false);
  assert.equal(await fs.readFile(config, "utf8"), "player config");
});
