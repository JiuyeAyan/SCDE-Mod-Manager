const fs = require("node:fs/promises");
const path = require("node:path");
const { GAME_APP_ID, GAME_EXECUTABLE } = require("./constants");
const { safeJoin } = require("./paths");
const { systemPathPolicy } = require("./system-path-policy");
const { assertUnlinked, persistenceRules, collectPersistentFiles, assertNoPersistentCollisions } = require("./persistent-data");

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
  if (current === root) await assertUnlinked(root);
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

async function inventoryMods(mods) {
  return Promise.all(mods.map(async mod => ({ mod, files: await listFiles(path.join(mod.folder, "payload")) })));
}

async function deploymentPlan(stageDir, mods, enabledIds, systemIds) {
  await assertUnlinked(stageDir);
  const inventories = await inventoryMods(mods);
  const policy = systemPathPolicy(inventories, systemIds);
  for (const { mod, files } of inventories) if (enabledIds.includes(mod.id)) policy.validate(mod, files);
  const rules = [{ root: "BepInEx/config" }, ...await persistenceRules(inventories)];
  const persistentFiles = await collectPersistentFiles(stageDir, rules);
  assertNoPersistentCollisions(inventories, enabledIds, persistentFiles);
  return { inventories, policy, persistentFiles };
}

async function prepareStage({ gameDir, stageDir, mods, enabledIds, systemIds }) {
  const validation = await validateGameDirectory(gameDir);
  if (!validation.valid) throw new Error(validation.reason);
  assertExpectedStageDirectory(gameDir, stageDir);

  // Preflight before destroying a previous copy. Recovery data lives beside, not inside, it.
  const plan = await deploymentPlan(stageDir, mods, enabledIds, systemIds);
  let dataBackup;
  try {
    if (plan.persistentFiles.length) {
      dataBackup = await fs.mkdtemp(path.join(path.dirname(stageDir), "scdemm-data-backup-"));
      for (const relative of plan.persistentFiles) {
        const target = safeJoin(dataBackup, relative);
        await fs.mkdir(path.dirname(target), { recursive: true });
        await fs.copyFile(safeJoin(stageDir, relative), target);
      }
    }
    await fs.rm(stageDir, { recursive: true, force: true });
    await fs.mkdir(path.dirname(stageDir), { recursive: true });
    await fs.cp(path.resolve(gameDir), stageDir, {
      recursive: true,
      dereference: true,
      force: true,
      errorOnExist: false,
      preserveTimestamps: true,
      filter: source => !plan.policy.excludesSource(path.relative(gameDir, source).split(path.sep).join("/")),
    });

    const appIdPath = path.join(stageDir, "steam_appid.txt");
    if (!(await pathExists(appIdPath))) {
      await fs.writeFile(appIdPath, `${GAME_APP_ID}\n`, "utf8");
    }

    const result = await applyMods({ gameDir, stageDir, mods, enabledIds, systemIds, previousActiveFiles: [], plan });
    if (dataBackup) {
      for (const relative of plan.persistentFiles) {
        await assertUnlinked(stageDir, relative);
        const target = safeJoin(stageDir, relative);
        await fs.mkdir(path.dirname(target), { recursive: true });
        await fs.copyFile(safeJoin(dataBackup, relative), target);
      }
      await fs.rm(dataBackup, { recursive: true, force: true });
    }
    return result;
  } catch (error) {
    if (dataBackup) error.message += ` (User-data recovery backup retained at ${dataBackup})`;
    throw error;
  }
}

async function applyMods({ gameDir, stageDir, mods, enabledIds, previousActiveFiles, systemIds, plan }) {
  assertExpectedStageDirectory(gameDir, stageDir);
  const stageValidation = await validateGameDirectory(stageDir);
  if (!stageValidation.valid) {
    throw new Error("游戏副本尚未准备，请先点击“准备游戏副本”。");
  }

  plan ||= await deploymentPlan(stageDir, mods, enabledIds, systemIds);
  // Validate every destination before restoring/removing the first file.
  for (const relative of [...previousActiveFiles || [], ...plan.inventories.filter(item => enabledIds.includes(item.mod.id)).flatMap(item => item.files), BEPINEX_PLUGIN_CACHE]) {
    safeJoin(stageDir, relative);
    await assertUnlinked(stageDir, relative);
  }
  const receipts = ["_scde_manager/active-mods.json", "_scde_manager/active-mods.lobby"];
  for (const relative of receipts) await assertUnlinked(stageDir, relative);
  // A failed partial redeploy must never reuse a receipt from the previous successful deployment.
  for (const relative of receipts) await fs.rm(safeJoin(stageDir, relative), { force: true });
  const persistent = new Set(plan.persistentFiles.map(file => file.toLowerCase()));
  for (const relative of previousActiveFiles || []) {
    if (/^BepInEx[\\/]config[\\/]/i.test(relative) || persistent.has(relative.replaceAll("\\", "/").toLowerCase())) continue;
    const original = safeJoin(gameDir, relative);
    const staged = safeJoin(stageDir, relative);
    if (!plan.policy.excludesSource(relative) && await pathExists(original)) {
      await fs.mkdir(path.dirname(staged), { recursive: true });
      await fs.copyFile(original, staged);
    } else {
      await fs.rm(staged, { force: true });
    }
  }

  const modById = new Map(plan.inventories.map(item => [item.mod.id, item]));
  const owners = new Map();
  const activeFiles = [];
  const activeFileKeys = new Set();
  const conflicts = [];

  for (const id of enabledIds) {
    const inventory = modById.get(id);
    if (!inventory) continue;
    const { mod, files } = inventory;
    const payloadRoot = path.join(mod.folder, "payload");

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
  inventoryMods,
  listFiles,
  pathExists,
  prepareStage,
  resolveStageDirectory,
  validateGameDirectory,
};
