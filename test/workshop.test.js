const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const test = require("node:test");
const AdmZip = require("adm-zip");
const { readPackageMetadata, scanWorkshop } = require("../src/core/workshop");
const { detectSteamLibraries } = require("../src/core/detect-game");
const { ModManager } = require("../src/core/manager");
const { installModPackage } = require("../src/core/mod-package");
const { GAME_EXECUTABLE } = require("../src/core/constants");

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-workshop-test-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  return root;
}
async function archive(file, id = "test.mod", description = "Dense metadata") {
  const zip = new AdmZip();
  zip.addFile("manifest.json", Buffer.from(JSON.stringify({ id, name: id, version: "1.0", description })));
  zip.addFile("payload/test.txt", Buffer.from("test payload"));
  await fs.mkdir(path.dirname(file), { recursive: true });
  zip.writeZip(file);
}
test("Workshop discovery traverses subscribed subfolders across libraries only for app 3024040", async (t) => {
  const root = await fixture(t);
  const libraries = [path.join(root, "library-one"), path.join(root, "library-two")];
  const folder = (library, app = "3024040") => path.join(library, "steamapps", "workshop", "content", app);
  await archive(path.join(folder(libraries[0]), "101", "nested", "first.scdemod"), "test.first");
  await archive(path.join(folder(libraries[1]), "202", "second.SCDEMOD"), "test.second");
  await archive(path.join(folder(libraries[0], "999"), "303", "other.scdemod"), "test.other");
  await fs.writeFile(path.join(folder(libraries[0]), "101", "map.map"), "map");
  await fs.writeFile(path.join(folder(libraries[0]), "101", "broken.scdemod"), "not zip");
  const items = [];
  const result = await scanWorkshop([...libraries, libraries[0]], { onItem: (item) => items.push(item) });
  assert.equal(result.count, 2);
  assert.equal(result.roots.length, 2);
  assert.equal(result.issues.length, 1);
  assert.deepEqual(items.map((item) => item.id).sort(), ["test.first", "test.second"]);
  assert.equal(items[0].description, "Dense metadata");
});
test("missing subscription folders are an empty result, and manual metadata can be read elsewhere", async (t) => {
  const root = await fixture(t);
  assert.equal((await scanWorkshop([root])).count, 0);
  const file = path.join(root, "manual.scdemod");
  await archive(file);
  assert.equal((await readPackageMetadata(file)).id, "test.mod");
});
test("Workshop scanning skips junctions and supports cancellation", async (t) => {
  const root = await fixture(t);
  const workshop = path.join(root, "steamapps", "workshop", "content", "3024040");
  const outside = path.join(root, "outside");
  await archive(path.join(outside, "hidden.scdemod"));
  await fs.mkdir(workshop, { recursive: true });
  await fs.symlink(outside, path.join(workshop, "junction"), "junction");
  assert.equal((await scanWorkshop([root])).count, 0);
  await archive(path.join(workshop, "one", "first.scdemod"), "test.first");
  await archive(path.join(workshop, "two", "second.scdemod"), "test.second");
  const controller = new AbortController();
  let found = 0;
  await assert.rejects(scanWorkshop([root], { signal: controller.signal, onItem: () => {
    found++; controller.abort();
  } }), /abort/i);
  assert.equal(found, 1);
});
test("oversized metadata and invalid packages are rejected without extracting payloads", async (t) => {
  const root = await fixture(t);
  const large = path.join(root, "large.scdemod");
  await archive(large, "test.large", "x".repeat(300000));
  await assert.rejects(readPackageMetadata(large), /too large/);
  const empty = path.join(root, "empty.scdemod");
  const zip = new AdmZip(); zip.addFile("payload/data", Buffer.from("data")); zip.writeZip(empty);
  await assert.rejects(readPackageMetadata(empty), /Missing/);
  const aborted = new AbortController(); aborted.abort();
  await assert.rejects(readPackageMetadata(empty, aborted.signal), /abort/i);
  // No extracted files or persistent archive handles: the entire fixture can be renamed.
  const renamed = `${large}.moved`;
  await fs.rename(large, renamed);
});
test("the configured game library remains discoverable without a registry entry", async (t) => {
  const root = await fixture(t);
  const gameDir = path.join(root, "steamapps", "common", "SCDE");
  assert.ok((await detectSteamLibraries(gameDir)).includes(root));
});
test("ensuring unchanged system Mods does not redeploy payloads at manager startup", async (t) => {
  const root = await fixture(t);
  const file = path.join(root, "runtime.scdemod"); await archive(file, "test.runtime");
  const gameDir = path.join(root, "original"); await fs.mkdir(gameDir);
  await fs.writeFile(path.join(gameDir, GAME_EXECUTABLE), "fixture");
  const manager = new ModManager(path.join(root, "manager"), { systemPackages: [
    { id: "test.runtime", version: "1.0", packagePath: file },
  ] });
  await manager.setGameDirectory(gameDir); await manager.ensureSystemMods(); await manager.prepareStage();
  let deployments = 0;
  manager.redeploy = async () => { deployments++; };
  assert.equal((await manager.ensureSystemMods()).stageReady, true);
  assert.equal(deployments, 0);
});

test("listing metadata does not read a large package payload into memory", async (t) => {
  const root = await fixture(t);
  const file = path.join(root, "large-payload.scdemod");
  const zip = new AdmZip();
  zip.addFile("manifest.json", Buffer.from(JSON.stringify({ id: "large.payload", name: "Large", version: "1" })));
  zip.addFile("payload/random.bin", require("node:crypto").randomBytes(4 * 1024 * 1024));
  zip.writeZip(file);
  const nativeFs = require("node:fs");
  const originalRead = nativeFs.read;
  let readBytes = 0;
  nativeFs.read = function (...args) {
    const callback = args.pop();
    return originalRead.call(this, ...args, (error, bytes, buffer) => {
      if (!error) readBytes += bytes;
      callback(error, bytes, buffer);
    });
  };
  try { assert.equal((await readPackageMetadata(file)).id, "large.payload"); }
  finally { nativeFs.read = originalRead; }
  assert.ok((await fs.stat(file)).size > 4 * 1024 * 1024);
  assert.ok(readBytes < 256 * 1024, `Metadata scan unexpectedly read ${readBytes} bytes`);
});

test("a failed import rollback preserves the backup instead of cleaning it away", async (t) => {
  const root = await fixture(t);
  const file = path.join(root, "package.scdemod"); await archive(file);
  const modsRoot = path.join(root, "mods"); await installModPackage(file, modsRoot);
  const originalRename = fs.rename;
  fs.rename = async (from, to) => {
    if (path.basename(from).startsWith(".install-") || path.basename(from).startsWith(".backup-")) {
      throw new Error("Simulated sharing violation");
    }
    return originalRename(from, to);
  };
  try { await assert.rejects(installModPackage(file, modsRoot), /sharing violation/); }
  finally { fs.rename = originalRename; }
  const backup = (await fs.readdir(modsRoot)).find((name) => name.startsWith(".backup-"));
  assert.ok(backup);
  assert.equal(await fs.readFile(path.join(modsRoot, backup, "payload", "test.txt"), "utf8"), "test payload");
});
