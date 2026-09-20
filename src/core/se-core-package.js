const fs = require("node:fs/promises");
const nativeFs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { Readable, Transform } = require("node:stream");
const { pipeline } = require("node:stream/promises");
const { setTimeout: delay } = require("node:timers/promises");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");
const yauzl = require("yauzl");
const { safeArchivePath } = require("./se-package");
const { safeJoin } = require("./paths");

const MAX_DOWNLOAD = 128 * 1024 * 1024;
const MAX_EXPANDED = 512 * 1024 * 1024;
const CORE_DLL = "BepInEx/plugins/000shcdese/SHCDESE.dll";

async function downloadSE(update, destination, options = {}) {
  const expected = `https://gitlab.com/api/v4/projects/74440776/packages/generic/shcdese/${encodeURIComponent(update.version)}/SHCDESE.zip`;
  if (update.downloadUrl !== expected) throw new Error("SE official runtime download is unavailable or unsupported.");
  for (let attempt = 1; attempt <= 10; attempt++) {
    options.signal?.throwIfAborted();
    const timeout = AbortSignal.timeout(options.timeoutMs ?? 120000);
    const signal = options.signal ? AbortSignal.any([options.signal, timeout]) : timeout;
    try {
      const response = await (options.fetch || fetch)(expected, { signal, redirect: "error" });
      if (!response.ok) {
        await response.body?.cancel();
        throw Object.assign(new Error(`SE download HTTP ${response.status}`), {
          permanent: response.status < 500 && ![408, 429].includes(response.status),
        });
      }
      if (Number(response.headers.get("content-length")) > MAX_DOWNLOAD) {
        await response.body?.cancel();
        throw Object.assign(new Error("SE archive exceeds the download limit."), { permanent: true });
      }
      let size = 0;
      const hash = crypto.createHash("sha256");
      await pipeline(Readable.fromWeb(response.body), new Transform({ transform(chunk, _encoding, callback) {
        size += chunk.length;
        if (size > MAX_DOWNLOAD) return callback(Object.assign(new Error("SE archive exceeds the download limit."), { permanent: true }));
        hash.update(chunk); callback(null, chunk);
      } }), nativeFs.createWriteStream(destination, { flags: "w" }), { signal });
      return hash.digest("hex");
    } catch (error) {
      await fs.rm(destination, { force: true });
      options.signal?.throwIfAborted();
      if (error.permanent || attempt === 10 || ["ENOSPC", "EACCES", "EPERM"].includes(error.code)) {
        throw Object.assign(error, { attempts: attempt });
      }
    }
    await delay(options.retryDelayMs ?? 3000, undefined, { signal: options.signal });
  }
}

async function extractSE(archive, destination, signal) {
  const zip = await yauzl.openPromise(archive, { lazyEntries: true, autoClose: false });
  try {
    if (zip.entryCount > 10000) throw new Error("SE archive has too many entries.");
    const names = new Set(); let total = 0, coreFound = false;
    for await (const entry of zip.eachEntry()) {
      signal?.throwIfAborted();
      const name = safeArchivePath(entry.fileName), key = name.toLowerCase();
      if (names.has(key) || (entry.externalFileAttributes >>> 16 & 0xf000) === 0xa000) {
        throw new Error("SE duplicate path or symbolic link.");
      }
      names.add(key);
      total += entry.uncompressedSize;
      if (entry.uncompressedSize > MAX_DOWNLOAD || total > MAX_EXPANDED) throw new Error("SE expanded archive exceeds the limit.");
      if (entry.fileName.endsWith("/")) continue;
      // Never overlay the shared BepInEx runtime, user configs or installer scripts.
      if (!key.startsWith("bepinex/plugins/") && key !== "msvcp140.dll") continue;
      if (key.startsWith("bepinex/plugins/scdemultiplayercompatibility/")) throw new Error("SE archive overlaps the manager's multiplayer component.");
      const relative = name.replace(/^bepinex\/plugins\//i, "BepInEx/plugins/");
      const target = safeJoin(destination, relative);
      await fs.mkdir(path.dirname(target), { recursive: true });
      await pipeline(await zip.openReadStreamPromise(entry), nativeFs.createWriteStream(target, { flags: "wx" }), { signal });
      if (key === CORE_DLL.toLowerCase()) coreFound = true;
    }
    if (!coreFound) throw new Error("SE core DLL is missing from the official archive.");
  } finally { zip.close(); }
}

async function prepareSE(update, folder, options) {
  if (!options.gameManagedRoot) throw new Error("Select a valid SCDE game folder and reopen the manager to retry the SE update.");
  await fs.access(options.gameManagedRoot);
  const archive = path.join(folder, "official.zip");
  const sha256 = await downloadSE(update, archive, options);
  const payload = path.join(folder, "payload");
  await extractSE(archive, payload, options.signal);
  const original = safeJoin(payload, CORE_DLL), patched = original + ".patched";
  try {
    await (options.execFile || promisify(execFile))(path.join(options.helperRoot, "EarlyManagedGuard.exe"), [
      original, patched, options.gameManagedRoot, options.runtimeRoot, update.version,
    ], { windowsHide: true, timeout: 30000, maxBuffer: 65536, signal: options.signal });
  } catch (error) { throw new Error("SE compatibility preparation failed: " + String(error.stderr || error.message).trim().slice(0, 600)); }
  await fs.rename(patched, original);
  const notices = path.join(path.dirname(original), "SCDEMM-notices");
  await fs.mkdir(notices, { recursive: true });
  for (const name of ["EarlyManagedGuard.cs", "early-managed-guard.patch", "LGPL-3.0.txt", "GPL-3.0.txt", "THIRD_PARTY_NOTICES.txt"]) {
    await fs.copyFile(path.join(options.helperRoot, name), path.join(notices, name));
  }
  await fs.writeFile(path.join(notices, "UPDATE.json"), JSON.stringify({
    upstreamVersion: update.version, source: update.downloadUrl, sha256,
    upstreamSource: `https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/tree/v${update.version}`,
    compatibility: "Not runtime-verified. The manager-mode updater guard was structurally checked and applied to this copy only.",
  }, null, 2));
  const manifest = {
    id: "shcde-script-extender", name: "Script Extender", version: `${update.version}${update.version.includes("+") ? "." : "+"}scdemm.1`, author: "Rawra",
    description: "SHCDE Script Extender with the manager-mode update guard. Upstream compatibility has not been runtime-verified.",
    dependencies: [{ id: "bepinex-runtime", version: "5.4.23.5" }],
  };
  await fs.writeFile(path.join(folder, "manifest.json"), JSON.stringify(manifest, null, 2));
  await fs.rm(archive);
  return manifest;
}

module.exports = { downloadSE, extractSE, prepareSE };
