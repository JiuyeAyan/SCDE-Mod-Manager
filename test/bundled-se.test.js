const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const AdmZip = require("adm-zip");
const { prepareBundle } = require("../tools/prepare-bundled-se");

test("each build obtains fresh SE and publishes only the validated version; failures preserve the last bundle", async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-bundle-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const output = path.join(root, "release/bundled-se");
  let checks = 0, preparations = 0;
  const check = async () => { checks++; return { failures: [], updates: [{ version: "2.8.0", downloadUrl: "official-fixture" }] }; };
  const prepare = async (update, candidate) => {
    preparations++;
    assert.equal(update.version, "2.8.0");
    const manifest = { id: "shcde-script-extender", version: "2.8.0+scdemm.1" };
    await fs.mkdir(path.join(candidate, "payload"));
    await fs.writeFile(path.join(candidate, "payload/fixture.dll"), "prepared fixture");
    await fs.writeFile(path.join(candidate, "manifest.json"), JSON.stringify(manifest));
    return manifest;
  };
  await prepareBundle(root, { check, prepare });
  await prepareBundle(root, { check, prepare });
  assert.equal(checks, 2); assert.equal(preparations, 2);
  const manifest = JSON.parse(await fs.readFile(path.join(output, "manifest.json")));
  const archivePath = path.join(output, "shcde-script-extender.scdemod");
  const before = await fs.readFile(archivePath);
  assert.deepEqual(JSON.parse(new AdmZip(before).readAsText("manifest.json")), manifest);
  assert.equal(manifest.version, "2.8.0+scdemm.1");
  await assert.rejects(prepareBundle(root, { check: async () => ({ failures: [{ reason: "network" }], updates: [] }), prepare }), /build stopped/);
  await assert.rejects(prepareBundle(root, { check, prepare: async () => { throw new Error("API incompatible"); } }), /API incompatible/);
  assert.deepEqual(await fs.readFile(archivePath), before);
  assert.deepEqual(JSON.parse(await fs.readFile(path.join(output, "manifest.json"))), manifest);
  assert.deepEqual(await fs.readdir(path.join(root, "release")), ["bundled-se"]);
});

test("both directory and portable builds use the latest-SE hook and never ship the historical fixed bundle", () => {
  const config = require("../package.json");
  assert.equal(config.build.beforePack, "tools/prepare-bundled-se.js");
  assert.ok(config.build.extraResources.some(item => item.from === "release/bundled-se"));
  assert.ok(!JSON.stringify(config.build.extraResources).includes("2.6.0"));
});

test("an explicitly supplied distribution cannot bypass the latest-release version check", async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-local-bundle-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const sourceDirectory = path.join(root, "source");
  const infoPath = path.join(sourceDirectory, "BepInEx/plugins/000shcdese/info.json");
  await fs.mkdir(path.dirname(infoPath), { recursive: true });
  await fs.writeFile(infoPath, JSON.stringify({ GUID: "000shcdese", Version: "2.6.0" }));
  const before = await fs.readFile(infoPath);
  await assert.rejects(prepareBundle(root, { sourceDirectory,
    check: async () => ({ failures: [], updates: [{ version: "2.8.0", downloadUrl: "official-fixture" }] }),
  }), /does not match the latest official release/);
  assert.deepEqual(await fs.readFile(infoPath), before);
  assert.deepEqual(await fs.readdir(path.join(root, "release")), []);
});
