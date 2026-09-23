const fs = require("node:fs/promises");
const path = require("node:path");
const { execFileSync } = require("node:child_process");
const AdmZip = require("adm-zip");
const crypto = require("node:crypto");
const { checkReleaseUpdates } = require("../src/core/release-updates");
const { prepareSE, prepareExtractedSE } = require("../src/core/se-core-package");
const { listFiles } = require("../src/core/deployer");
const { detectSteamGame } = require("../src/core/detect-game");

// A build must obtain a fresh official candidate. Never fall back to an old bundle.
async function prepareBundle(root, options = {}) {
  const signal = options.signal || AbortSignal.timeout(5 * 60 * 1000);
  const result = await (options.check || checkReleaseUpdates)([
    { id: "shcde-script-extender", version: "0.0.0" },
  ], { signal, timeoutMs: 15000, retryDelayMs: 1000 });
  const update = result.updates[0];
  if (result.failures.length || !update?.downloadUrl) {
    throw new Error("Cannot obtain the latest official SE release; build stopped: " + JSON.stringify(result));
  }
  console.log(`Preparing latest official SE ${update.version}...`);
  const releaseRoot = path.join(root, "release");
  await fs.mkdir(releaseRoot, { recursive: true });
  const temporary = await fs.mkdtemp(path.join(releaseRoot, "bundled-se-build-"));
  const candidate = path.join(temporary, "candidate");
  const output = path.join(releaseRoot, "bundled-se");
  const backup = path.join(temporary, "previous");
  try {
    await fs.mkdir(candidate);
    const preparation = {
      signal, helperRoot: path.join(releaseRoot, "se-update-helper"),
      runtimeRoot: path.join(root, "third_party/BepInEx_win_x64_5.4.23.5/BepInEx/core"),
      gameManagedRoot: options.gameManagedRoot,
    };
    let manifest;
    if (options.sourceDirectory) {
      const source = path.resolve(options.sourceDirectory);
      const core = "BepInEx/plugins/000shcdese/SHCDESE.dll";
      const info = JSON.parse(await fs.readFile(path.join(source, "BepInEx/plugins/000shcdese/info.json"), "utf8"));
      if (info.GUID !== "000shcdese" || info.Version !== update.version) {
        throw new Error("The supplied SE distribution does not match the latest official release.");
      }
      // Reject links and copy only runtime payload, never the SDK, launch scripts or shared loader.
      for (const file of await listFiles(source)) {
        if (!file.toLowerCase().startsWith("bepinex/plugins/") && file.toLowerCase() !== "msvcp140.dll") continue;
        const target = path.join(candidate, "payload", file);
        await fs.mkdir(path.dirname(target), { recursive: true });
        await fs.copyFile(path.join(source, file), target);
      }
      manifest = await prepareExtractedSE(update, candidate, preparation, {
        source: "Explicitly supplied extracted release; original ZIP hash unavailable",
        sha256: null,
        upstreamDllSha256: crypto.createHash("sha256").update(await fs.readFile(path.join(source, core))).digest("hex"),
      });
    } else {
      manifest = await (options.prepare || prepareSE)(update, candidate, preparation);
    }
    const next = path.join(temporary, "bundle");
    await fs.mkdir(next);
    const zip = new AdmZip();
    zip.addLocalFolder(candidate);
    zip.writeZip(path.join(next, "shcde-script-extender.scdemod"));
    await fs.writeFile(path.join(next, "manifest.json"), JSON.stringify(manifest, null, 2));
    // Publish the matched archive/manifest together, only after all validation succeeds.
    let hadPrevious = false;
    try { await fs.rename(output, backup); hadPrevious = true; }
    catch (error) { if (error.code !== "ENOENT") throw error; }
    try { await fs.rename(next, output); }
    catch (error) {
      if (hadPrevious) await fs.rename(backup, output);
      throw error;
    }
    console.log(`BUNDLED_SE_READY ${manifest.version}`);
    return manifest;
  } finally {
    await fs.rm(temporary, { recursive: true, force: true });
  }
}

async function beforePack(context) {
  const root = context.packager.info.appDir;
  const gameDir = process.env.SCDE_GAME_DIR || (await detectSteamGame())[0];
  if (!gameDir) throw new Error("Install SCDE or set SCDE_GAME_DIR for SE compatibility validation.");
  execFileSync(process.execPath, [path.join(root, "tools/build-se-update-helper.js")], {
    cwd: root, windowsHide: true, stdio: "inherit",
  });
  return prepareBundle(root, {
    gameManagedRoot: path.join(gameDir, "Stronghold Crusader Definitive Edition_Data/Managed"),
    sourceDirectory: process.env.SCDE_SE_DISTRIBUTION || undefined,
  });
}

module.exports = beforePack;
module.exports.prepareBundle = prepareBundle;
