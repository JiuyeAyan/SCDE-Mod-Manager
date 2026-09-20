const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const { componentWarnings } = require("../src/core/se-compatibility");
const { seVersionBounds } = require("../src/core/se-package");

test("old and new SE bounds intersect, validate and ignore unrelated dependencies", () => {
  assert.deepEqual(seVersionBounds({ MinimumScriptExtenderVersion: "2.3.0", MaximumScriptExtenderVersion: "3.0",
    Dependencies: [{ GUID: " 000SHCDESE ", MinimumVersion: " 2.7.1 ", MaximumVersion: "2.8" }, { GUID: "other", MinimumVersion: "9.0" }] }),
  { minimumVersion: "2.7.1", maximumVersion: "2.8" });
  assert.throws(() => seVersionBounds({ MinimumScriptExtenderVersion: "2.9", MaximumScriptExtenderVersion: "2.8" }), /inconsistent/);
  assert.throws(() => seVersionBounds({ Dependencies: [{ GUID: "000shcdese", MinimumVersion: "invalid" }] }), /invalid/);
});

test("component checks read only enabled pack metadata, support modern bounds and skip links", async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-components-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const plugins = path.join(root, "payload/BepInEx/plugins/Pack");
  await fs.mkdir(plugins, { recursive: true });
  const outside = path.join(root, "unrelated"); await fs.mkdir(outside);
  await fs.writeFile(path.join(outside, "info.json"), "invalid JSON must not be read");
  await fs.symlink(outside, path.join(plugins, "linked"), "junction");
  await fs.writeFile(path.join(plugins, "info.json"), JSON.stringify({ GUID: "Child", Name: "Feature", Dependencies: [{ GUID: "000shcdese", MaximumVersion: "2.6.0" }] }));
  const mods = [{ id: "pack", name: "Pack", folder: root, scriptExtender: { guid: "Pack" } }];
  assert.deepEqual(await componentWarnings(mods, [], "2.7.2"), []);
  assert.equal((await componentWarnings(mods, ["pack"], "2.7.2+scdemm.1"))[0].name, "Feature");
  assert.deepEqual(await componentWarnings(mods, ["pack"], "2.6.0+scdemm.1"), []);
});
