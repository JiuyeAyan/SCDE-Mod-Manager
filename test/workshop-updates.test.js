const assert = require("node:assert/strict");
const test = require("node:test");
const { compareVersions, findWorkshopUpdates } = require("../src/core/workshop-updates");

test("Workshop versions compare numerically, not lexically, and do not guess labels", () => {
  for (const [a, b, expected] of [
    ["0.2.51", "0.2.48", 1], ["1.10", "1.9", 1], ["1.0", "1.0.0", 0],
    ["v2.0.0", "1.99", 1], ["5.4.23.5", "5.4.23.4", 1],
    ["1.0.0-rc.2", "1.0.0-rc.10", -1], ["1.0.0", "1.0.0-rc.1", 1],
    ["1.0.0+build2", "1.0.0+build1", 0], ["stable", "beta", null],
  ]) assert.equal(compareVersions(a, b), expected, `${a} vs ${b}`);
});

test("update reminders include only installed newer Mods, including disabled Mods", () => {
  const installed = [
    { id: "fog", version: "0.2.48", enabled: false },
    { id: "keyboard", version: "0.2.17" },
    { id: "system", version: "1.0" },
  ];
  const candidates = [
    { id: "fog", version: "0.2.51", path: "new-subscription-file.scdemod" },
    { id: "keyboard", version: "0.2.17" }, { id: "keyboard", version: "0.2.16" },
    { id: "never-imported", version: "9.0" }, { id: "system", version: "2.0" },
  ];
  const before = JSON.stringify(installed);
  const updates = findWorkshopUpdates(installed, candidates, new Set(["system"]));
  assert.equal(updates.length, 1);
  assert.equal(updates[0].id, "fog");
  assert.equal(updates[0].installedVersion, "0.2.48");
  assert.equal(updates[0].path, "new-subscription-file.scdemod");
  assert.equal(JSON.stringify(installed), before);
  assert.deepEqual(findWorkshopUpdates([], candidates), []);
});
