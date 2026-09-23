const crypto = require("node:crypto");
const fs = require("node:fs/promises");
const path = require("node:path");
const AdmZip = require("adm-zip");
const { safeJoin } = require("./paths");
const { readSEPackageMetadata, stageSEPackage, refreshInstalledSEMetadata, safeArchivePath } = require("./se-package");
const { MANIFEST_SCHEMA_VERSION, normalizeDependencyVersions, normalizePackageSha256, normalizePersistentPaths } = require("./manifest-contract");

const MOD_ID_PATTERN = /^[a-z0-9][a-z0-9._-]{1,63}$/;
const WINDOWS_RESERVED_ID = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i;

function isValidModId(id) {
  return typeof id === "string" && MOD_ID_PATTERN.test(id) && !id.endsWith(".") && !WINDOWS_RESERVED_ID.test(id);
}

function validateManifest(manifest) {
  if (!manifest || typeof manifest !== "object" || Array.isArray(manifest)) {
    throw new Error("manifest.json 必须是 JSON 对象。");
  }
  if (manifest.schemaVersion !== undefined && manifest.schemaVersion !== MANIFEST_SCHEMA_VERSION) {
    throw new Error(`Unsupported manifest.schemaVersion: ${manifest.schemaVersion}`);
  }
  if (!isValidModId(manifest.id)) {
    throw new Error("manifest.id 只能包含 2–64 位小写字母、数字、点、横线或下划线。");
  }
  for (const field of ["name", "version"]) {
    if (typeof manifest[field] !== "string" || !manifest[field].trim()) {
      throw new Error(`manifest.${field} 不能为空。`);
    }
  }

  const dependencies = manifest.dependencies === undefined ? [] : manifest.dependencies;
  if (!Array.isArray(dependencies)) {
    throw new Error("manifest.dependencies 必须是数组。");
  }
  const dependencyIds = new Set();
  const normalizedDependencies = dependencies.map((dependency) => {
    if (!dependency || typeof dependency !== "object" || Array.isArray(dependency)) {
      throw new Error("manifest.dependencies 中的每一项都必须是对象。");
    }
    if (!isValidModId(dependency.id)) {
      throw new Error("依赖 Mod 的 id 格式无效。");
    }
    if (dependency.id === manifest.id) {
      throw new Error("Mod 不能依赖自身。");
    }
    if (dependencyIds.has(dependency.id)) {
      throw new Error(`依赖 Mod 重复：${dependency.id}`);
    }
    dependencyIds.add(dependency.id);

    return {
      id: dependency.id,
      ...normalizeDependencyVersions(dependency),
    };
  });

  const persistentPaths = normalizePersistentPaths(manifest.persistentPaths);
  const packageSha256 = normalizePackageSha256(manifest.packageSha256);
  return {
    schemaVersion: MANIFEST_SCHEMA_VERSION,
    id: manifest.id,
    name: manifest.id === "bepinex-runtime" ? "BepInEx Runtime" : manifest.name.trim(),
    version: manifest.version.trim(),
    author: typeof manifest.author === "string" ? manifest.author.trim() : "",
    description:
      manifest.id === "bepinex-runtime"
        ? "Shared BepInEx 5 Mono x64 runtime for SCDE runtime code Mods."
        : typeof manifest.description === "string" ? manifest.description.trim() : "",
    gameVersion:
      typeof manifest.gameVersion === "string" ? manifest.gameVersion.trim() : "",
    dependencies: normalizedDependencies,
    ...(persistentPaths ? { persistentPaths } : {}),
    ...(packageSha256 ? { packageSha256 } : {}),
    ...(manifest.scriptExtender && typeof manifest.scriptExtender.guid === "string" ? {
      scriptExtender: { ...Object.fromEntries(["guid", "versionCheckUrl", "minimumVersion", "maximumVersion"]
        .map(key => [key, typeof manifest.scriptExtender[key] === "string" ? manifest.scriptExtender[key] : ""])),
        ...(manifest.scriptExtender.metadataVersion === 2 ? { metadataVersion: 2 } : {}) },
    } : {}),
  };
}

function normalizeEntryName(entryName) {
  const normalized = entryName.replaceAll("\\", "/").replace(/^\.\//, "");
  try {
    safeArchivePath(normalized);
  } catch {
    throw new Error(`Mod 包中包含不安全路径：${entryName}`);
  }
  return normalized;
}

async function readInstalledMods(modsRoot) {
  await fs.mkdir(modsRoot, { recursive: true });
  const entries = await fs.readdir(modsRoot, { withFileTypes: true });
  const mods = [];

  for (const entry of entries) {
    if (!entry.isDirectory() || entry.name.startsWith(".")) continue;
    try {
      const manifestPath = path.join(modsRoot, entry.name, "manifest.json");
      const manifest = await refreshInstalledSEMetadata(
        validateManifest(JSON.parse(await fs.readFile(manifestPath, "utf8"))), path.join(modsRoot, entry.name));
      mods.push({ ...manifest, folder: path.join(modsRoot, entry.name) });
    } catch {
      // Ignore incomplete folders; imports are staged atomically before appearing here.
    }
  }

  return mods.sort((a, b) => a.name.localeCompare(b.name, "zh-CN"));
}

async function installModPackage(packagePath, modsRoot, options = {}) {
  if ([".map", ".semod"].includes(path.extname(packagePath).toLowerCase())) {
    const metadata = await readSEPackageMetadata(packagePath);
    if (!metadata) throw new Error("No installable Script Extender Mod in this file");
    return installPreparedMod(metadata, modsRoot, (temporaryRoot) => stageSEPackage(packagePath, temporaryRoot, metadata), options);
  }
  if (path.extname(packagePath).toLowerCase() !== ".scdemod") {
    throw new Error("请选择扩展名为 .scdemod 的 Mod 包。");
  }

  const archiveBytes = await fs.readFile(packagePath);
  const digest = crypto.createHash("sha256").update(archiveBytes).digest("hex");
  const zip = new AdmZip(archiveBytes);
  const entries = zip.getEntries();
  const normalizedEntries = new Map();

  for (const entry of entries) {
    const normalized = normalizeEntryName(entry.entryName);
    if (normalizedEntries.has(normalized.toLowerCase())) {
      throw new Error(`Mod 包中有重复路径：${normalized}`);
    }
    normalizedEntries.set(normalized.toLowerCase(), { entry, normalized });
  }

  const manifestEntry = normalizedEntries.get("manifest.json");
  if (!manifestEntry || manifestEntry.entry.isDirectory) {
    throw new Error("Mod 包根目录缺少 manifest.json。");
  }

  let manifest;
  try {
    manifest = validateManifest(JSON.parse(manifestEntry.entry.getData().toString("utf8")));
  } catch (error) {
    throw new Error(`manifest.json 无效：${error.message}`);
  }

  const payloadFiles = [...normalizedEntries.values()].filter(
    ({ entry, normalized }) => !entry.isDirectory && normalized.startsWith("payload/")
  );
  if (payloadFiles.length === 0) {
    throw new Error("Mod 包的 payload 目录中没有文件。");
  }

  return installPreparedMod(manifest, modsRoot, async (temporaryRoot) => {
    for (const { entry, normalized } of payloadFiles) {
      const relative = normalized.slice("payload/".length);
      const destination = safeJoin(path.join(temporaryRoot, "payload"), relative);
      await fs.mkdir(path.dirname(destination), { recursive: true });
      await fs.writeFile(destination, entry.getData());
    }
    return { ...manifest, packageSha256: digest };
  }, options);
}

async function installPreparedMod(manifest, modsRoot, prepare, { validatePrepared } = {}) {
  await fs.mkdir(modsRoot, { recursive: true });
  const targetRoot = path.join(modsRoot, manifest.id);
  const temporaryRoot = path.join(modsRoot, `.install-${manifest.id}-${crypto.randomUUID()}`);
  const backupRoot = path.join(modsRoot, `.backup-${manifest.id}-${crypto.randomUUID()}`);
  let installed;
  try {
    await fs.mkdir(path.join(temporaryRoot, "payload"), { recursive: true });
    installed = await prepare(temporaryRoot);
    if (validatePrepared) await validatePrepared(installed, temporaryRoot);
    await fs.writeFile(path.join(temporaryRoot, "manifest.json"), `${JSON.stringify(installed, null, 2)}\n`, "utf8");

    let hadPreviousVersion = false;
    try {
      await fs.rename(targetRoot, backupRoot);
      hadPreviousVersion = true;
    } catch (error) {
      if (error.code !== "ENOENT") throw error;
    }

    try {
      await fs.rename(temporaryRoot, targetRoot);
      if (hadPreviousVersion) await fs.rm(backupRoot, { recursive: true, force: true });
    } catch (error) {
      if (hadPreviousVersion) await fs.rename(backupRoot, targetRoot);
      throw error;
    }
  } finally {
    await fs.rm(temporaryRoot, { recursive: true, force: true });
    // The successful swap already removes the backup. Preserve it if rollback failed.
  }

  return installed;
}

module.exports = {
  installModPackage,
  installPreparedMod,
  normalizeEntryName,
  readInstalledMods,
  validateManifest,
};
