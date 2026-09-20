const fs = require("node:fs/promises");
const path = require("node:path");
const { GAME_APP_ID, GAME_EXECUTABLE } = require("./constants");
const { safeJoin } = require("./paths");

const STAGE_DIRECTORY_SUFFIX = " - SCDE Modded";
const BEPINEX_PLUGIN_CACHE = "BepInEx/cache/chainloader_typeloader.dat";

function resolveStageDirectory(gameDir) {
  if (!gameDir || typeof gameDir !== "string") return "";
  const resolved = path.resolve(gameDir);
  return path.join(path.dirname(resolved), `${path.basename(resolved)}${STAGE_DIRECTORY_SUFFIX}`);
}

function assertExpectedStageDirectory(gameDir, stageDir) {
  const expected = resolveStageDirectory(gameDir);
  const actual = path.resolve(stageDir || "");
  const normalize = (value) => (process.platform === "win32" ? value.toLowerCase() : value);
  if (!expected || normalize(actual) !== normalize(expected)) {
    throw new Error("游戏副本必须位于原游戏目录旁边的 SCDE Modded 目录中。");
  }
}

async function pathExists(target) {
  try {
    await fs.access(target);
    return true;
  } catch {
    return false;
  }
}

async function filesHaveSameContents(first, second) {
  const [firstStat, secondStat] = await Promise.all([fs.stat(first), fs.stat(second)]);
  if (firstStat.size !== secondStat.size) return false;

  const firstHandle = await fs.open(first, "r");
  let secondHandle;
  try {
    secondHandle = await fs.open(second, "r");
    const firstBuffer = Buffer.allocUnsafe(64 * 1024);
    const secondBuffer = Buffer.allocUnsafe(64 * 1024);
    let position = 0;
    while (position < firstStat.size) {
      const length = Math.min(firstBuffer.length, firstStat.size - position);
      const [firstRead, secondRead] = await Promise.all([
        firstHandle.read(firstBuffer, 0, length, position),
        secondHandle.read(secondBuffer, 0, length, position),
      ]);
      if (
        firstRead.bytesRead === 0 ||
        firstRead.bytesRead !== secondRead.bytesRead ||
        !firstBuffer
          .subarray(0, firstRead.bytesRead)
          .equals(secondBuffer.subarray(0, secondRead.bytesRead))
      ) {
        return false;
      }
      position += firstRead.bytesRead;
    }
    return true;
  } finally {
    if (secondHandle) await Promise.all([firstHandle.close(), secondHandle.close()]);
    else await firstHandle.close();
  }
}

async function listFiles(root, current = root) {
  if (!(await pathExists(root))) return [];
  const entries = await fs.readdir(current, { withFileTypes: true });
  const files = [];

  for (const entry of entries) {
    const absolute = path.join(current, entry.name);
    if (entry.isSymbolicLink()) {
      throw new Error(`Mod payload 不允许符号链接：${absolute}`);
    }
    if (entry.isDirectory()) {
      files.push(...(await listFiles(root, absolute)));
    } else if (entry.isFile()) {
      files.push(path.relative(root, absolute).split(path.sep).join("/"));
    }
  }
  return files.sort();
}

async function validateGameDirectory(gameDir) {
  if (!gameDir || typeof gameDir !== "string") {
    return { valid: false, reason: "尚未选择游戏目录。" };
  }
  const executable = path.join(path.resolve(gameDir), GAME_EXECUTABLE);
  if (!(await pathExists(executable))) {
    return { valid: false, reason: `目录中找不到 ${GAME_EXECUTABLE}` };
  }
  return { valid: true, executable };
}

async function prepareStage({ gameDir, stageDir, mods, enabledIds }) {
  const validation = await validateGameDirectory(gameDir);
  if (!validation.valid) throw new Error(validation.reason);
  assertExpectedStageDirectory(gameDir, stageDir);

  // Configs belong to players, including editable translations created before first launch.
  // Keep recovery copies outside the directory being rebuilt and retain them on failure.
  const configPath = path.join(stageDir, "BepInEx/config");
  let configBackup;
  if (await pathExists(configPath)) {
    for (const folder of [stageDir, path.join(stageDir, "BepInEx"), configPath]) {
      if ((await fs.lstat(folder)).isSymbolicLink()) throw new Error("Linked game config directory is not supported.");
    }
    await listFiles(configPath); // Reject links instead of copying data outside the game copy.
    configBackup = await fs.mkdtemp(path.join(path.dirname(stageDir), "scdemm-config-backup-"));
    await fs.cp(configPath, path.join(configBackup, "config"), { recursive: true });
  }
  try {
    await fs.rm(stageDir, { recursive: true, force: true });
    await fs.mkdir(path.dirname(stageDir), { recursive: true });
    await fs.cp(path.resolve(gameDir), stageDir, {
      recursive: true,
      dereference: true,
      force: true,
      errorOnExist: false,
      preserveTimestamps: true,
    });

    const appIdPath = path.join(stageDir, "steam_appid.txt");
    if (!(await pathExists(appIdPath))) {
      await fs.writeFile(appIdPath, `${GAME_APP_ID}\n`, "utf8");
    }

    if (configBackup) await fs.cp(path.join(configBackup, "config"), configPath, { recursive: true, force: true });
    const result = await applyMods({ gameDir, stageDir, mods, enabledIds, previousActiveFiles: [] });
    if (configBackup) await fs.rm(configBackup, { recursive: true, force: true });
    return result;
  } catch (error) {
    if (configBackup) error.message += ` (Configs retained at ${configBackup})`;
    throw error;
  }
}

async function applyMods({ gameDir, stageDir, mods, enabledIds, previousActiveFiles }) {
  const stageValidation = await validateGameDirectory(stageDir);
  if (!stageValidation.valid) {
    throw new Error("游戏副本尚未准备，请先点击“准备游戏副本”。");
  }

  for (const relative of previousActiveFiles || []) {
    if (/^BepInEx[\\/]config[\\/][^\\/]+[\\/]lang[\\/]/i.test(relative)) continue;
    const original = safeJoin(gameDir, relative);
    const staged = safeJoin(stageDir, relative);
    if (await pathExists(original)) {
      await fs.mkdir(path.dirname(staged), { recursive: true });
      await fs.copyFile(original, staged);
    } else {
      await fs.rm(staged, { force: true });
    }
  }

  const modById = new Map(mods.map((mod) => [mod.id, mod]));
  const owners = new Map();
  const activeFiles = [];
  const activeFileKeys = new Set();
  const conflicts = [];

  for (const id of enabledIds) {
    const mod = modById.get(id);
    if (!mod) continue;
    const payloadRoot = path.join(mod.folder, "payload");
    const files = await listFiles(payloadRoot);

    for (const relative of files) {
      const source = safeJoin(payloadRoot, relative);
      const destination = safeJoin(stageDir, relative);
      if ((mod.scriptExtender && /^BepInEx\/config\//i.test(relative)) || /^BepInEx\/config\/[^/]+\/lang\//i.test(relative)) {
        // SE ships defaults, but players own their live configuration after the first deployment.
        await fs.mkdir(path.dirname(destination), { recursive: true });
        if (!(await pathExists(destination))) await fs.copyFile(source, destination);
        continue;
      }
      const relativeKey = relative.toLowerCase();
      const previousOwner = owners.get(relativeKey);
      let needsCopy = true;
      if (previousOwner) {
        if (await filesHaveSameContents(previousOwner.source, source)) {
          needsCopy = false;
        } else {
          conflicts.push({ path: relative, overwritten: previousOwner.id, winner: id });
        }
      }
      owners.set(relativeKey, { id, source });
      if (needsCopy) {
        await fs.mkdir(path.dirname(destination), { recursive: true });
        await fs.copyFile(source, destination);
      }
      if (!activeFileKeys.has(relativeKey)) {
        activeFileKeys.add(relativeKey);
        activeFiles.push(relative);
      }
    }
  }

  await fs.rm(safeJoin(stageDir, BEPINEX_PLUGIN_CACHE), { force: true });

  return { activeFiles, conflicts };
}

module.exports = {
  applyMods,
  filesHaveSameContents,
  listFiles,
  pathExists,
  prepareStage,
  resolveStageDirectory,
  validateGameDirectory,
};
