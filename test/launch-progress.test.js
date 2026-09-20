const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const { LaunchProgress } = require("../src/core/launch-progress");

test("plugin progress accepts split UTF-8 lines, measures intervals, and does not confuse chainloader with ready", () => {
  let now = 1000;
  const progress = new LaunchProgress(() => {}, () => now);
  progress.launchId = "this-launch";
  const line = Buffer.from("[Info   :   BepInEx] Loading [测试 Mod 1.0]\r\n");
  progress.consume(line.subarray(0, 31)); progress.consume(line.subarray(31));
  assert.equal(progress.items[0].name, "测试 Mod 1.0");
  now = 2400;
  progress.consume(Buffer.from("[Info   :   BepInEx] Loading [Second 2.0]\n"));
  assert.equal(progress.items[0].endedAt - progress.items[0].startedAt, 1400);
  now = 3000;
  progress.consume(Buffer.from("[Message:   BepInEx] Chainloader startup complete\n"));
  assert.equal(progress.phase, "menu"); assert.equal(progress.stopped, false);
  progress.consume(Buffer.from("[Info:Reporter] SCDEMM_STARTUP_READY old-launch\n"));
  assert.equal(progress.phase, "menu");
  progress.consume(Buffer.from("[Info:Reporter] SCDEMM_STARTUP_READY this-launch\n"));
  assert.equal(progress.phase, "ready"); assert.equal(progress.stopped, true);
});

test("startup monitor ignores old log and ready marker, reads only bounded increments, then stops on current ready marker", async () => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scde-progress-"));
  const progress = new LaunchProgress(() => {});
  try {
    await fs.mkdir(path.join(root, "BepInEx")); await fs.mkdir(path.join(root, "_scde_manager"));
    const log = path.join(root, "BepInEx/LogOutput.log");
    const ready = path.join(root, "_scde_manager/startup-ready.txt");
    await fs.writeFile(log, "[Info   :   BepInEx] Loading [Old 1.0]\n");
    await fs.writeFile(ready, "previous");
    await progress.watch(root, "current"); clearTimeout(progress.timer);
    await progress.poll(); clearTimeout(progress.timer);
    assert.equal(progress.items.length, 0);
    await fs.writeFile(log, "[Info   :   BepInEx] Loading [New 1.0]\n" + "x".repeat(200000));
    await fs.utimes(log, new Date(), new Date(Date.now() + 2000));
    await progress.poll(); clearTimeout(progress.timer);
    assert.equal(progress.items[0].name, "New 1.0"); assert.equal(progress.offset, 65536);
    assert.ok(progress.pending.length <= 8192);
    await fs.writeFile(ready, "current");
    await progress.poll();
    assert.equal(progress.phase, "ready"); assert.equal(progress.stopped, true);
    const offset = progress.offset; await progress.poll(); assert.equal(progress.offset, offset);
  } finally { progress.stop(); await fs.rm(root, { recursive: true, force: true }); }
});

test("missing log never blocks a game; exit is not reported as successful load", async () => {
  const progress = new LaunchProgress(() => {});
  await progress.watch(path.join(os.tmpdir(), "missing-scde-progress-log"), "id");
  clearTimeout(progress.timer);
  await progress.poll(); clearTimeout(progress.timer);
  assert.equal(progress.phase, "runtime");
  progress.consume(Buffer.from("[Info   :   BepInEx] Loading [Slow Mod 1.0]\n"));
  progress.finish("exited");
  assert.equal(progress.items[0].endedAt, undefined);
  assert.equal(progress.phase, "exited"); assert.equal(progress.stopped, true);
});

test("unconfirmed startup releases its polling timer without claiming success", async () => {
  let now = 0;
  const progress = new LaunchProgress(() => {}, () => now);
  await progress.watch(path.join(os.tmpdir(), "missing-scde-progress-timeout"), "id");
  clearTimeout(progress.timer);
  now = 15 * 60 * 1000 + 1;
  await progress.poll();
  assert.equal(progress.phase, "unconfirmed"); assert.equal(progress.stopped, true);
});
