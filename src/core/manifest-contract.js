const { compareVersions } = require("./workshop-updates");

const MANIFEST_SCHEMA_VERSION = 1;

function normalizePackageSha256(value) {
  return typeof value === "string" && /^[a-f0-9]{64}$/i.test(value) ? value.toLowerCase() : undefined;
}

function normalizeDependencyVersions(dependency) {
  const result = { version: "" };
  for (const key of ["version", "minimumVersion", "maximumVersion"]) {
    const value = dependency[key];
    if (value === undefined) continue;
    if (typeof value !== "string" || (key !== "version" && (!value.trim() || compareVersions(value.trim(), "0") === null))) {
      throw new Error(`Dependency ${dependency.id}: invalid ${key}.`);
    }
    result[key] = value.trim();
  }
  if (result.version && (result.minimumVersion || result.maximumVersion)) {
    throw new Error(`Dependency ${dependency.id}: use either version or minimumVersion/maximumVersion, not both.`);
  }
  if (result.minimumVersion && result.maximumVersion && compareVersions(result.minimumVersion, result.maximumVersion) === 1) {
    throw new Error(`Dependency ${dependency.id}: minimumVersion exceeds maximumVersion.`);
  }
  return result;
}

function dependencyVersionMatches(installedVersion, dependency) {
  if (dependency.version) return installedVersion === dependency.version;
  for (const [key, rejected] of [["minimumVersion", -1], ["maximumVersion", 1]]) {
    if (dependency[key] && [null, rejected].includes(compareVersions(installedVersion, dependency[key]))) return false;
  }
  return true;
}

function normalizePersistentPaths(declaration) {
  if (declaration === undefined) return undefined;
  if (!declaration || typeof declaration !== "object" || Array.isArray(declaration) ||
      declaration.version !== 1 || !Array.isArray(declaration.paths)) {
    throw new Error("manifest.persistentPaths must contain version: 1 and a paths array.");
  }
  const paths = declaration.paths.map(value => {
    const normalized = typeof value === "string" ? value.replaceAll("\\", "/") : "";
    if (!normalized || normalized.split("/").some(part => !part || part === "." || part === ".." ||
        /[<>:\"|?*\x00-\x1f]/.test(part) || /[. ]$/.test(part) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(part))) {
      throw new Error(`manifest.persistentPaths contains an unsafe relative path: ${value}`);
    }
    return normalized;
  });
  const seen = new Set();
  for (const relative of paths.map(value => value.toLowerCase()).sort()) {
    const parts = relative.split("/");
    for (let length = 1; length <= parts.length; length++) {
      if (seen.has(parts.slice(0, length).join("/"))) {
        throw new Error("manifest.persistentPaths contains duplicate or overlapping paths.");
      }
    }
    seen.add(relative);
  }
  return { version: 1, paths };
}

module.exports = { MANIFEST_SCHEMA_VERSION, dependencyVersionMatches, normalizeDependencyVersions,
  normalizePackageSha256, normalizePersistentPaths };
