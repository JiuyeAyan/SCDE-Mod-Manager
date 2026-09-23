const fs = require("node:fs/promises");
const path = require("node:path");
const { StringDecoder } = require("node:string_decoder");

// Timings are observations of the existing BepInEx log, not a profiler or a completion estimate.
class LaunchProgress {
  constructor(onChange, now = Date.now) {
    this.now = now;
    this.onChange = onChange;
    this.startedAt = now();
    this.phase = "preparing";
    this.items = [];
    this.active = null;
    this.decoder = new StringDecoder("utf8");
    this.pending = "";
    this.offset = 0;
    this.stopped = false;
    this.emit();
  }

  snapshot() {
    return { phase: this.phase, startedAt: this.startedAt, endedAt: this.endedAt,
      items: this.items.map(item => ({ ...item })), detail: this.detail || "" };
  }

  emit() { this.onChange(this.snapshot()); }

  completeActive(at) {
    if (this.active) this.active.endedAt = at;
    this.active = null;
  }

  consume(bytes) {
    if (this.stopped) return;
    const lines = (this.pending + this.decoder.write(bytes)).split(/\r?\n/);
    this.pending = lines.pop().slice(-8192);
    for (const line of lines) {
      if (this.stopped) break;
      const match = line.match(/^\[Info\s*:\s*BepInEx\]\s+Loading \[(.+)\]\s*$/);
      if (match) {
        this.completeActive(this.now());
        this.phase = "plugins";
        this.active = { name: match[1].slice(0, 300), startedAt: this.now() };
        this.items.push(this.active);
        if (this.items.length > 512) this.items.shift();
      } else if (/^\[Message\s*:\s*BepInEx\]\s+Chainloader startup complete\s*$/.test(line)) {
        this.completeActive(this.now());
        this.phase = "menu";
      } else if (this.launchId && line.endsWith("SCDEMM_STARTUP_READY " + this.launchId)) {
        this.finish("ready");
      } else if (this.launchId && line.includes("SCDEMM_STARTUP_FAILED " + this.launchId + " ")) {
        this.finish("failed", line.split("SCDEMM_STARTUP_FAILED " + this.launchId + " ")[1].slice(0, 2000));
      }
    }
  }

  async watch(stageDir, launchId) {
    this.launchId = launchId;
    this.logPath = path.join(stageDir, "BepInEx", "LogOutput.log");
    this.readyPath = path.join(stageDir, "_scde_manager", "startup-ready.txt");
    this.failedPath = path.join(stageDir, "_scde_manager", "startup-failed.txt");
    this.initialStat = await fs.stat(this.logPath).catch(() => null);
    this.phase = "runtime";
    this.emit();
    this.schedule();
  }

  schedule() {
    if (!this.stopped) {
      this.timer = setTimeout(() => this.poll(), 500);
      this.timer.unref?.();
    }
  }

  async poll() {
    if (this.stopped) return;
    try {
      const stat = await fs.stat(this.logPath);
      // Ignore a previous session's log until the new process has rewritten it.
      if (this.fresh || !this.initialStat || stat.mtimeMs !== this.initialStat.mtimeMs || stat.ino !== this.initialStat.ino) {
        this.fresh = true;
        if (stat.size < this.offset) { this.offset = 0; this.pending = ""; this.decoder = new StringDecoder("utf8"); }
        const size = Math.min(64 * 1024, Math.max(0, stat.size - this.offset));
        if (size) {
          const file = await fs.open(this.logPath, "r");
          try {
            const buffer = Buffer.alloc(size);
            const { bytesRead } = await file.read(buffer, 0, size, this.offset);
            this.offset += bytesRead;
            if (!this.stopped) this.consume(buffer.subarray(0, bytesRead));
          } finally { await file.close(); }
        }
      }
    } catch { /* Missing/locked logs must never delay or stop the game. */ }
    try {
      const stat = await fs.stat(this.failedPath);
      if (stat.size < 8192) {
        const message = await fs.readFile(this.failedPath, "utf8");
        if (message.startsWith(this.launchId + "\n")) this.finish("failed", message.slice(this.launchId.length + 1, this.launchId.length + 2001));
      }
    } catch { /* No failure reported for this launch. */ }
    try {
      const stat = await fs.stat(this.readyPath);
      if (stat.size < 256 && (await fs.readFile(this.readyPath, "utf8")) === this.launchId) this.finish("ready");
    } catch { /* The reporter has not reached the main menu yet. */ }
    if (this.stopped) return;
    if (this.now() - this.startedAt > 15 * 60 * 1000) this.finish("unconfirmed");
    else { this.emit(); this.schedule(); }
  }

  finish(phase, detail = "") {
    if (this.stopped) return;
    this.stop();
    this.endedAt = this.now();
    // An interrupted initialization is not a successful plugin load.
    if (phase === "ready") this.completeActive(this.endedAt);
    this.phase = phase;
    this.detail = detail;
    this.emit();
  }

  stop() { this.stopped = true; clearTimeout(this.timer); }
}

module.exports = { LaunchProgress };
