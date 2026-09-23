const fs = require("node:fs/promises");
const path = require("node:path");
const { safeJoin } = require("./paths");
const { normalizePersistentPaths } = require("./manifest-contract");
const lower = value => value.toLowerCase();
const inside = (file, root) => file === root || file.startsWith(root + "/");
const executable = /\.(dll|exe|so|dylib|pdb|semod|map|lua|luac)$/i;

async function assertUnlinked(root, relative = "") {
  let current = root;
  for (const part of ["", ...relative.replaceAll("\\", "/").split("/").filter(Boolean)]) {
    if (part) current = path.join(current, part);
    try { if ((await fs.lstat(current)).isSymbolicLink()) throw new Error(`Linked deployment/data path is not supported: ${current}`); }
    catch (error) { if (error.code === "ENOENT") return; throw error; }
  }
}

async function persistenceRules(inventories) {
  const owners = new Map();
  for (const { mod, files } of inventories) for (const file of files) {
    const match = /^(BepInEx\/plugins\/[^/]+)\//i.exec(file);
    if (match) {
      const ids = owners.get(lower(match[1])) || new Set(); ids.add(mod.id); owners.set(lower(match[1]), ids);
    }
  }
  const rules = [];
  for (const { mod, files } of inventories) {
    for (const relative of normalizePersistentPaths(mod.persistentPaths)?.paths || []) {
      const match = /^(BepInEx\/plugins\/[^/]+)\/.+/i.exec(relative);
      const ids = match && owners.get(lower(match[1]));
      if (!ids || ids.size !== 1 || !ids.has(mod.id) || executable.test(relative)) {
        throw new Error(`Persistent path must be user data inside ${mod.id}'s exclusive plugin folder: ${relative}`);
      }
      rules.push({ root: relative, modId: mod.id });
    }
    // Migrate only the documented SE settings formats, never entire plugin/data trees.
    if (mod.id === "shcde-script-extender" || mod.scriptExtender ||
        (mod.dependencies || []).some(dep => dep.id === "shcde-script-extender")) {
      const directories = new Set(files.filter(file => /^BepInEx\/plugins\/.+\/.+\.dll$/i.test(file)).map(file => path.posix.dirname(file)));
      for (const directory of directories) rules.push({ root: directory + "/LobbyModSettings", modId: mod.id, extensions: /\.(msgpack|bin)$/i });
    }
    // Rawra's Fixes GUID is 'fixes'; inspect its bounded metadata, not the display name.
    for (const file of files.filter(file => /^BepInEx\/plugins\/.+\/info\.json$/i.test(file))) {
      const metadataPath = safeJoin(path.join(mod.folder, "payload"), file);
      if ((await fs.stat(metadataPath)).size > 256 * 1024) continue;
      let info;
      try { info = JSON.parse(await fs.readFile(metadataPath, "utf8")); } catch { continue; }
      if (info.GUID === "fixes") rules.push({ root: path.posix.dirname(file) + "/data/hopsFarmWhitelist.json", modId: mod.id });
    }
  }
  return rules;
}

async function collectPersistentFiles(stageDir, rules) {
  const found = new Map();
  async function visit(relative, rule) {
    await assertUnlinked(stageDir, relative);
    const file = safeJoin(stageDir, relative);
    let stat;
    try { stat = await fs.lstat(file); } catch (error) { if (error.code === "ENOENT") return; throw error; }
    if (stat.isDirectory()) {
      for (const entry of await fs.readdir(file)) await visit(relative + "/" + entry, rule);
    } else if (stat.isFile() && (!rule.extensions || rule.extensions.test(relative))) {
      if (rule.modId && executable.test(relative)) throw new Error(`Executable file cannot be restored as persistent user data: ${relative}`);
      found.set(lower(relative), relative);
    }
  }
  for (const rule of rules) await visit(rule.root, rule);
  return [...found.values()];
}

function assertNoPersistentCollisions(inventories, enabledIds, persistentFiles) {
  const saved = persistentFiles.map(lower);
  for (const { mod, files } of inventories) {
    if (!enabledIds.includes(mod.id)) continue;
    for (const file of files) {
      const normalized = lower(file);
      if (inside(normalized, "bepinex/config")) continue; // Existing config-default policy is unchanged.
      if (saved.some(item => inside(item, normalized) || inside(normalized, item))) {
        throw new Error(`Packaged content collides with persistent user data: ${file} (${mod.name || mod.id}). No data was overwritten.`);
      }
    }
  }
}

module.exports = { assertUnlinked, persistenceRules, collectPersistentFiles, assertNoPersistentCollisions };
