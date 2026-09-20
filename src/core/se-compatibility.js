const fs = require("node:fs/promises");
const path = require("node:path");
const { compareVersions } = require("./workshop-updates");
const { seVersionBounds } = require("./se-package");

// Inspect installed metadata only, on explicit launch/version-switch actions.
// Pack-specific in-game toggles are deliberately not guessed.
async function componentWarnings(mods, enabledIds, version) {
  const warnings = [];
  const incompatible = ({ minimumVersion, maximumVersion }) =>
    (minimumVersion && [null, -1].includes(compareVersions(version, minimumVersion))) ||
    (maximumVersion && [null, 1].includes(compareVersions(version, maximumVersion)));
  for (const mod of mods) {
    if (!mod.scriptExtender || !enabledIds.includes(mod.id)) continue;
    const { minimumVersion, maximumVersion } = mod.scriptExtender;
    if (incompatible(mod.scriptExtender)) warnings.push({ packageName: mod.name, name: mod.name, minimumVersion, maximumVersion });
    const pending = [path.join(mod.folder, "payload/BepInEx/plugins")];
    let examined = 0;
    while (pending.length) {
      const folder = pending.pop();
      let entries;
      try { entries = await fs.readdir(folder, { withFileTypes: true }); }
      catch (error) { if (error.code === "ENOENT") continue; throw error; }
      for (const entry of entries) {
        if (++examined > 10000) throw new Error("SE component metadata scan limit exceeded.");
        if (entry.isSymbolicLink()) continue;
        const file = path.join(folder, entry.name);
        if (entry.isDirectory()) { pending.push(file); continue; }
        if (!entry.isFile() || entry.name.toLowerCase() !== "info.json") continue;
        const handle = await fs.open(file, "r");
        let info;
        try {
          if ((await handle.stat()).size > 256 * 1024) throw new Error("SE component info.json too large.");
          info = JSON.parse(await handle.readFile("utf8"));
        } finally { await handle.close(); }
        if (typeof info.GUID !== "string" || info.GUID.toLowerCase() === mod.scriptExtender.guid.toLowerCase()) continue;
        const bounds = seVersionBounds(info);
        if (incompatible(bounds)) {
          warnings.push({ packageName: mod.name, name: typeof info.Name === "string" ? info.Name : info.GUID, ...bounds });
        }
      }
    }
  }
  return warnings;
}

module.exports = { componentWarnings };
