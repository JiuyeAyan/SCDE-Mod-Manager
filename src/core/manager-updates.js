const fs = require("node:fs/promises");
const { createReadStream } = require("node:fs");
const path = require("node:path");
const { createHash } = require("node:crypto");
const { execFile, spawn } = require("node:child_process");
const { promisify } = require("node:util");
const { detectSteamLibraries } = require("./detect-game");
const { compareVersions } = require("./workshop-updates");

const MANAGER_WORKSHOP_ID = "3796682182";
const PORTABLE_NAME = "SCDE-Mod-Manager-Portable.exe";
const powershell = path.join(process.env.SystemRoot || "C:\\Windows", "System32/WindowsPowerShell/v1.0/powershell.exe");

async function readExecutableVersion(file) {
  const { stdout } = await promisify(execFile)(powershell, ["-NoProfile", "-NonInteractive", "-Command",
    "[Console]::OutputEncoding = [Text.UTF8Encoding]::new(); $v = [Diagnostics.FileVersionInfo]::GetVersionInfo($env:SCDEMM_VERSION_FILE); @{product=$v.ProductName;version=$v.ProductVersion} | ConvertTo-Json -Compress"],
  { windowsHide: true, timeout: 5000, maxBuffer: 8192, env: { ...process.env, SCDEMM_VERSION_FILE: file } });
  return JSON.parse(stdout.replace(/^\uFEFF/, "").trim());
}

async function fileStamp(file) {
  const stat = await fs.lstat(file);
  if (!stat.isFile() || stat.isSymbolicLink() || stat.size < 1 || stat.size > 1024 * 1024 * 1024) throw new Error("MANAGER_UPDATE_INVALID_FILE");
  return `${stat.size}:${stat.mtimeMs}:${stat.ctimeMs}`;
}

async function checkManagerUpdate(currentVersion, gameDir, options = {}) {
  let newest = null;
  for (const library of options.libraries || await detectSteamLibraries(gameDir)) {
    // Never enumerate Workshop siblings or search for executables in other Mods.
    const source = path.resolve(library, "steamapps/workshop/content/3024040", MANAGER_WORKSHOP_ID, PORTABLE_NAME);
    try {
      const stamp = await fileStamp(source);
      const info = await (options.readVersion || readExecutableVersion)(source);
      if (stamp !== await fileStamp(source) || info.product !== "SCDE Mod Manager" || compareVersions(info.version, newest?.version || currentVersion) !== 1) continue;
      newest = { source, stamp, version: info.version, currentVersion };
    } catch { /* A missing/incomplete Workshop download must not block launching. */ }
  }
  return newest;
}

async function hashFile(file) {
  const hash = createHash("sha256");
  for await (const chunk of createReadStream(file)) hash.update(chunk);
  return hash.digest("hex");
}

async function prepareManagerUpdate(update, target, options = {}) {
  if (!target || !path.isAbsolute(target) || path.extname(target).toLowerCase() !== ".exe") throw new Error("MANAGER_UPDATE_NOT_PORTABLE");
  if (await fileStamp(update.source) !== update.stamp) throw new Error("MANAGER_UPDATE_SOURCE_CHANGED");
  const inspect = options.readVersion || readExecutableVersion;
  const originalStamp = await fileStamp(target);
  if ((await inspect(target)).product !== "SCDE Mod Manager") throw new Error("MANAGER_UPDATE_INVALID_TARGET");
  // Same-volume replacement; preserve this directory as the user's rollback backup.
  const job = await fs.mkdtemp(path.join(path.dirname(target), ".scdemm-update-"));
  try {
    const candidate = path.join(job, "new.exe");
    await fs.copyFile(update.source, candidate, fs.constants.COPYFILE_EXCL);
    const info = await inspect(candidate);
    if (info.product !== "SCDE Mod Manager" || info.version !== update.version ||
        await fileStamp(update.source) !== update.stamp) throw new Error("MANAGER_UPDATE_SOURCE_CHANGED");
    const [newHash, sourceHash, oldHash] = await Promise.all([hashFile(candidate), hashFile(update.source), hashFile(target)]);
    if (newHash !== sourceHash || await fileStamp(update.source) !== update.stamp || await fileStamp(target) !== originalStamp) throw new Error("MANAGER_UPDATE_SOURCE_CHANGED");
    await fs.copyFile(path.join(__dirname, "manager-update.ps1"), path.join(job, "apply.ps1"));
    await fs.writeFile(path.join(job, "update.json"), JSON.stringify({ target, version: update.version, newHash, oldHash, pid: process.pid }), "utf8");
    return job;
  } catch (error) {
    // Only remove the directory this invocation just created, never the target or source.
    await fs.rm(job, { recursive: true, force: true });
    throw error;
  }
}

async function startManagerUpdate(job) {
  const log = await fs.open(path.join(job, "helper.log"), "a");
  let child;
  let failure;
  try {
    child = spawn(powershell, ["-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", path.join(job, "apply.ps1")],
      { windowsHide: true, stdio: ["ignore", log.fd, log.fd], cwd: process.env.SystemRoot || "C:\\Windows",
        env: { ...process.env, PSModulePath: path.join(path.dirname(powershell), "Modules") } });
    child.on("error", error => { failure = error; });
    child.on("exit", code => { failure = new Error(`MANAGER_UPDATE_HELPER_EXIT ${code}`); });
  } finally { await log.close(); }
  for (let i = 0; i < 100; i++) {
    if (failure) throw failure;
    try { await fs.access(path.join(job, "ready")); child.unref(); return; } catch { /* Wait for helper preflight. */ }
    await new Promise(resolve => setTimeout(resolve, 100));
  }
  // No commit marker: even a slow helper is forbidden from replacing anything.
  child.unref();
  throw new Error("MANAGER_UPDATE_HELPER_TIMEOUT");
}

module.exports = { MANAGER_WORKSHOP_ID, PORTABLE_NAME, readExecutableVersion, checkManagerUpdate, prepareManagerUpdate, startManagerUpdate };
