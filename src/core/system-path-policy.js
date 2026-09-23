const SYSTEM_IDS = ["bepinex-runtime", "shcde-script-extender", "scde-multiplayer-compatibility"];
const key = value => value.replaceAll("\\", "/").toLowerCase();
const inside = (file, root) => file === root || file.startsWith(root + "/");

function systemPathPolicy(inventories, systemIds = SYSTEM_IDS) {
  const systems = new Set([...SYSTEM_IDS, ...systemIds]);
  const trees = new Map([
    ["bepinex/core", "bepinex-runtime"], ["bepinex/patchers", "bepinex-runtime"],
    ["bepinex/plugins/000shcdese", "shcde-script-extender"],
    ["bepinex/plugins/uuimgui", "shcde-script-extender"],
    ["bepinex/plugins/scdemultiplayercompatibility", "scde-multiplayer-compatibility"],
    ["_scde_manager", "@manager"],
  ]);
  const exact = new Map(["winhttp.dll", "doorstop_config.ini", ".doorstop_version"].map(file => [file, "bepinex-runtime"]));
  const sourceRoots = new Set(exact.keys());
  for (const { mod, files } of inventories) {
    if (!systems.has(mod.id)) continue;
    for (const file of files) {
      const normalized = key(file);
      const previous = exact.get(normalized);
      if (previous && previous !== mod.id) throw new Error(`System packages share an exclusive path: ${file}`);
      exact.set(normalized, mod.id);
      if (!normalized.includes("/")) sourceRoots.add(normalized);
      const plugin = /^(bepinex\/plugins\/[^/]+)\//.exec(normalized);
      if (plugin) {
        const previousTree = trees.get(plugin[1]);
        if (previousTree && previousTree !== mod.id) throw new Error(`System packages share an exclusive plugin folder: ${file}`);
        trees.set(plugin[1], mod.id);
      }
    }
  }
  const reservedPaths = [...trees, ...exact];
  return {
    validate(mod, files) {
      for (const file of files) {
        const normalized = key(file);
        for (const [reserved, id] of reservedPaths) {
          // A file cannot stand in for a protected parent directory (or vice versa).
          if (id !== mod.id && (inside(normalized, reserved) || inside(reserved, normalized))) {
            throw new Error(`Reserved system path: ${file} (owned by ${id}; package ${mod.id}).`);
          }
        }
      }
    },
    excludesSource(file) {
      const normalized = key(file);
      return inside(normalized, "bepinex") || inside(normalized, "_scde_manager") || sourceRoots.has(normalized);
    },
  };
}

module.exports = { SYSTEM_IDS, systemPathPolicy };
