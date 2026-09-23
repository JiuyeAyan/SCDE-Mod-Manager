const assert = require("node:assert/strict");
const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const nativeFs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const test = require("node:test");
const AdmZip = require("adm-zip");
const { installModPackage, normalizeEntryName, readInstalledMods, validateManifest } = require("../src/core/mod-package");
const { readPackageMetadata, scanWorkshop } = require("../src/core/workshop");
const { findWorkshopUpdates } = require("../src/core/workshop-updates");

const manifest = (changes = {}) => ({ id: "example.mod", name: "Example", version: "1.0", ...changes });

async function fixture(t) {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-manifest-contract-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  return root;
}

async function archive(file, metadata = manifest(), payload = "old") {
  const zip = new AdmZip();
  zip.addFile("manifest.json", Buffer.from(JSON.stringify(metadata)));
  zip.addFile("payload/BepInEx/plugins/example.mod/plugin.dll", Buffer.from(payload));
  await fs.mkdir(path.dirname(file), { recursive: true });
  zip.writeZip(file);
  return crypto.createHash("sha256").update(await fs.readFile(file)).digest("hex");
}

test("manifest schema and installer digest survive normalization and reloading", async (t) => {
  assert.equal(validateManifest(manifest()).schemaVersion, 1);
  assert.equal(validateManifest(manifest({ schemaVersion: 1 })).schemaVersion, 1);
  for (const version of [0, 2, "1", null]) {
    assert.throws(() => validateManifest(manifest({ schemaVersion: version })), /schemaVersion/);
  }
  for (const id of ["con", "nul.txt", "com1", "trailing.", 123]) {
    assert.throws(() => validateManifest(manifest({ id })), /id/);
    assert.throws(() => validateManifest(manifest({ dependencies: [{ id }] })), /id/);
  }
  const root = await fixture(t);
  const file = path.join(root, "package.scdemod");
  const digest = await archive(file, manifest({ packageSha256: "f".repeat(64) }));
  const installed = await installModPackage(file, path.join(root, "mods"));
  assert.equal(installed.packageSha256, digest);
  assert.equal((await readInstalledMods(path.join(root, "mods")))[0].packageSha256, digest);
  assert.equal((await readPackageMetadata(file)).packageSha256, undefined, "archive metadata cannot authenticate its own digest");
  assert.equal(validateManifest(manifest({ packageSha256: digest.toUpperCase() })).packageSha256, digest);
  assert.equal(validateManifest(manifest({ packageSha256: "invalid" })).packageSha256, undefined);
});

test("dependency contract keeps exact and unconstrained forms and validates inclusive ranges", () => {
  const { dependencyVersionMatches } = require("../src/core/manifest-contract");
  const normalize = (dependency) => validateManifest(manifest({ dependencies: [{ id: "runtime.mod", ...dependency }] })).dependencies[0];
  assert.deepEqual(normalize({ version: " 5.4.23.5 " }), { id: "runtime.mod", version: "5.4.23.5" });
  const unconstrained = normalize({});
  assert.deepEqual(normalize(unconstrained), unconstrained, "normalized legacy dependencies must be readable again");
  assert.equal(dependencyVersionMatches("development", unconstrained), true);
  assert.equal(dependencyVersionMatches("1.0.0", normalize({ version: "1.0" })), false, "exact versions remain exact strings");
  const bounds = normalize({ minimumVersion: " 2.6.0 ", maximumVersion: "2.8.0" });
  assert.equal(bounds.minimumVersion, "2.6.0");
  for (const version of ["2.6.0", "2.7.0", "2.8.0+scdemm.1"]) assert.equal(dependencyVersionMatches(version, bounds), true);
  for (const version of ["2.5.9", "2.8.1", "unknown"]) assert.equal(dependencyVersionMatches(version, bounds), false);
  assert.equal(dependencyVersionMatches("1.0.0-rc.1", normalize({ minimumVersion: "1.0.0" })), false);
  for (const invalid of [
    { minimumVersion: "latest" }, { maximumVersion: "" }, { minimumVersion: 2 },
    { minimumVersion: "3", maximumVersion: "2" }, { version: "2.0", minimumVersion: "1" },
  ]) assert.throws(() => normalize(invalid), /version|Version/);
});

test("persistent path contract is versioned, relative, non-overlapping and Windows-safe", () => {
  const normalize = (persistentPaths) => validateManifest(manifest({ persistentPaths }));
  assert.equal(validateManifest(manifest()).persistentPaths, undefined);
  assert.deepEqual(normalize({ version: 1, paths: ["BepInEx\\plugins\\example.mod\\settings.json"] }).persistentPaths,
    { version: 1, paths: ["BepInEx/plugins/example.mod/settings.json"] });
  for (const declaration of [null, [], { version: 2, paths: [] }, { version: "1", paths: [] },
    { version: 1, paths: "settings.json" }, { version: 1, paths: ["../settings.json"] },
    { version: 1, paths: ["C:/settings.json"] }, { version: 1, paths: ["/settings.json"] },
    { version: 1, paths: ["a/../b"] }, { version: 1, paths: ["a//b"] },
    { version: 1, paths: ["a/./b"] }, { version: 1, paths: ["a/file:stream"] },
    { version: 1, paths: ["a/NUL.txt"] }, { version: 1, paths: ["a/file."] },
    { version: 1, paths: ["a/*"] }, { version: 1, paths: ["a", "A/b"] },
    { version: 1, paths: ["a/B", "A/b"] },
    { version: 1, paths: ["a", "a-b", "a/b"] },
  ]) assert.throws(() => normalize(declaration), /persistentPaths/);
});

test("prepared-package rejection runs before replacing an existing installation", async (t) => {
  const root = await fixture(t);
  const file = path.join(root, "package.scdemod");
  const mods = path.join(root, "mods");
  await archive(file);
  await installModPackage(file, mods);
  await archive(file, manifest({ version: "2.0" }), "new");
  let inspected = false;
  await assert.rejects(installModPackage(file, mods, { validatePrepared: async (metadata, temporaryRoot) => {
    inspected = true;
    assert.equal(metadata.version, "2.0");
    assert.equal(await fs.readFile(path.join(temporaryRoot, "payload/BepInEx/plugins/example.mod/plugin.dll"), "utf8"), "new");
    throw new Error("Reserved runtime payload");
  } }), /Reserved runtime/);
  assert.equal(inspected, true);
  assert.equal((await readInstalledMods(mods))[0].version, "1.0");
  assert.deepEqual(await fs.readdir(mods), ["example.mod"]);
});

test("scdemod payload entry names reject Windows aliases before extracting or replacing installed files", async t => {
  assert.equal(normalizeEntryName("./payload/"), "payload/");
  assert.equal(normalizeEntryName("payload\\BepInEx\\plugins\\example.mod\\"), "payload/BepInEx/plugins/example.mod/");
  assert.equal(normalizeEntryName("./payload/BepInEx/plugins/example.mod/plugin.dll"), "payload/BepInEx/plugins/example.mod/plugin.dll");
  const root = await fixture(t);
  const file = path.join(root, "package.scdemod");
  const mods = path.join(root, "mods");
  await archive(file); await installModPackage(file, mods);
  const manifestPath = path.join(mods, "example.mod/manifest.json");
  const before = await fs.readFile(manifestPath, "utf8");
  for (const alias of ["payload/BepInEx/core./bad.dll", "payload/BepInEx/core /bad.dll",
    "payload/winhttp.dll:stream", "payload/NUL.txt", "payload/BepInEx/plugins/example.mod/COM1.cfg",
    "payload/BepInEx//plugins/example.mod/bad.dll", "payload/BepInEx/./core/bad.dll"]) {
    assert.throws(() => normalizeEntryName(alias), /unsafe|不安全/, alias);
    const zip = new AdmZip();
    zip.addFile("manifest.json", Buffer.from(JSON.stringify(manifest({ version: "2.0" }))));
    zip.addFile("payload/placeholder", Buffer.from("must not extract"));
    // Bypass the ZIP writer's path cleanup so the archive really contains the alias.
    zip.getEntry("payload/placeholder").entryName = alias;
    zip.writeZip(file);
    await assert.rejects(installModPackage(file, mods), /unsafe|不安全/, alias);
    assert.equal(await fs.readFile(manifestPath, "utf8"), before);
    assert.deepEqual(await fs.readdir(mods), ["example.mod"]);
  }
});

test("Workshop hashes only installed matching-version packages and offers changed bytes without installing", async (t) => {
  const root = await fixture(t);
  const folder = path.join(root, "steamapps/workshop/content/3024040/123");
  const currentFile = path.join(folder, "current.scdemod");
  const oldDigest = await archive(currentFile);
  const installed = [manifest({ packageSha256: oldDigest }), manifest({ id: "excluded.mod", packageSha256: oldDigest }),
    manifest({ id: "legacy.mod" })];
  const newDigest = await archive(currentFile, manifest({ packageSha256: oldDigest }), "revised same version");
  await archive(path.join(folder, "newer.scdemod"), manifest({ version: "2" }));
  await archive(path.join(folder, "older.scdemod"), manifest({ version: "0.9" }));
  await archive(path.join(folder, "unknown.scdemod"), manifest({ id: "never.imported" }));
  await archive(path.join(folder, "excluded.scdemod"), manifest({ id: "excluded.mod" }));
  await archive(path.join(folder, "legacy.scdemod"), manifest({ id: "legacy.mod" }));
  const originalStream = nativeFs.createReadStream;
  const hashed = [];
  nativeFs.createReadStream = function(file, ...args) {
    hashed.push(file);
    return originalStream.call(this, file, ...args);
  };
  const candidates = [];
  try {
    await scanWorkshop([root], { installedMods: installed, excludedIds: new Set(["excluded.mod"]), onItem: item => candidates.push(item) });
  } finally { nativeFs.createReadStream = originalStream; }
  assert.deepEqual(hashed, [currentFile]);
  assert.equal(candidates.find(item => item.path === currentFile).packageSha256, newDigest);
  const updates = findWorkshopUpdates(installed, candidates, new Set(["excluded.mod"]));
  assert.equal(updates.length, 2);
  assert.equal(updates.find(item => item.path === currentFile).updateReason, "content-changed");
  assert.equal(updates.find(item => item.path === currentFile).installedVersion, "1.0");
  assert.equal(installed[0].packageSha256, oldDigest, "checking does not replace installed content or its record");
  assert.deepEqual(findWorkshopUpdates(installed, [{ ...installed[0], packageSha256: oldDigest }]), []);
  assert.deepEqual(findWorkshopUpdates(installed, [{ ...installed[0], packageSha256: undefined }]), []);
});

test("same-version Workshop hashing remains cancellable", async (t) => {
  const root = await fixture(t);
  const file = path.join(root, "steamapps/workshop/content/3024040/123/package.scdemod");
  const digest = await archive(file, manifest(), crypto.randomBytes(256 * 1024));
  const controller = new AbortController();
  const originalStream = nativeFs.createReadStream;
  nativeFs.createReadStream = function(...args) {
    const stream = originalStream.apply(this, args);
    stream.once("data", () => controller.abort());
    return stream;
  };
  try {
    await assert.rejects(scanWorkshop([root], { signal: controller.signal,
      installedMods: [manifest({ packageSha256: digest })] }), /abort/i);
  } finally { nativeFs.createReadStream = originalStream; }
});
