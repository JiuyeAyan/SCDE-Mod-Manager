const fs = require("node:fs/promises");
const nativeFs = require("node:fs");
const crypto = require("node:crypto");
const path = require("node:path");
const yauzl = require("yauzl");
const { GAME_APP_ID } = require("./constants");
const { validateManifest } = require("./mod-package");
const { readSEPackageMetadata } = require("./se-package");
const { normalizePackageSha256 } = require("./manifest-contract");
const { compareVersions } = require("./workshop-updates");

// A manifest is metadata, not game content. Bound decompression of untrusted listings.
const MAX_MANIFEST_BYTES = 256 * 1024;
const MAX_ARCHIVE_ENTRIES = 100000;

async function readPackageMetadata(filePath, signal) {
  if ([".map", ".semod"].includes(path.extname(filePath).toLowerCase())) return readSEPackageMetadata(filePath, signal);
  signal?.throwIfAborted();
  const zip = await yauzl.openPromise(filePath, { lazyEntries: true, autoClose: false });
  let stream;
  const abort = () => stream?.destroy(signal.reason);
  signal?.addEventListener("abort", abort, { once: true });
  try {
    signal?.throwIfAborted();
    if (zip.entryCount > MAX_ARCHIVE_ENTRIES) throw new Error("Too many archive entries");
    let manifest;
    let payload = false;
    for await (const entry of zip.eachEntry()) {
      signal?.throwIfAborted();
      const name = entry.fileName.replace(/^\.\//, "");
      if (name.startsWith("payload/") && !name.endsWith("/")) payload = true;
      if (name.toLowerCase() !== "manifest.json") continue;
      if (manifest) throw new Error("Duplicate manifest.json");
      if (entry.uncompressedSize > MAX_MANIFEST_BYTES) throw new Error("manifest.json is too large");
      stream = await zip.openReadStreamPromise(entry);
      signal?.throwIfAborted();
      const chunks = [];
      let bytes = 0;
      for await (const chunk of stream) {
        bytes += chunk.length;
        if (bytes > MAX_MANIFEST_BYTES) throw new Error("manifest.json is too large");
        chunks.push(chunk);
      }
      manifest = validateManifest(JSON.parse(Buffer.concat(chunks).toString("utf8")));
      stream = null;
    }
    if (!manifest || !payload) throw new Error("Missing manifest.json or payload files");
    // Only a digest of the actual archive is usable, never one claimed inside it.
    delete manifest.packageSha256;
    return { ...manifest, path: filePath };
  } finally {
    signal?.removeEventListener("abort", abort);
    stream?.destroy();
    zip.close();
  }
}

async function scanWorkshop(libraries, { signal, onItem = () => {}, installedMods = [], excludedIds = new Set() } = {}) {
  const installedById = new Map(installedMods.map(mod => [mod.id, mod]));
  const roots = [...new Map(libraries.map((library) => {
    const root = path.resolve(library, "steamapps", "workshop", "content", GAME_APP_ID);
    return [root.toLowerCase(), root];
  })).values()];
  const foundRoots = [];
  const issues = [];
  let count = 0;
  const visit = async (directory) => {
    signal?.throwIfAborted();
    let entries;
    try { entries = await fs.readdir(directory, { withFileTypes: true }); }
    catch (error) {
      if (error.code !== "ENOENT") issues.push({ path: directory, error: error.message });
      return;
    }
    for (const entry of entries) {
      signal?.throwIfAborted();
      // Never follow junctions/symlinks outside the subscription tree.
      if (entry.isSymbolicLink()) continue;
      const target = path.join(directory, entry.name);
      if (entry.isDirectory()) await visit(target);
      else if (entry.isFile() && [".scdemod", ".map", ".semod"].includes(path.extname(entry.name).toLowerCase())) {
        try {
          const item = await readPackageMetadata(target, signal);
          signal?.throwIfAborted();
          if (!item) continue; // Ordinary maps are not installable global SE Mods.
          const installed = installedById.get(item.id);
          if (path.extname(target).toLowerCase() === ".scdemod" && installed && !excludedIds.has(item.id) &&
              normalizePackageSha256(installed.packageSha256) &&
              (item.version === installed.version || compareVersions(item.version, installed.version) === 0)) {
            const hash = crypto.createHash("sha256");
            for await (const chunk of nativeFs.createReadStream(target, { signal })) hash.update(chunk);
            signal?.throwIfAborted();
            item.packageSha256 = hash.digest("hex");
          }
          onItem(item);
          count++;
        } catch (error) {
          signal?.throwIfAborted();
          issues.push({ path: target, error: error.message });
        }
      }
    }
  };
  for (const root of roots) {
    try {
      const stat = await fs.lstat(root);
      if (!stat.isDirectory() || stat.isSymbolicLink()) continue;
      foundRoots.push(root);
      await visit(root);
    } catch (error) {
      signal?.throwIfAborted();
      if (error.code !== "ENOENT") issues.push({ path: root, error: error.message });
    }
  }
  return { roots: foundRoots, searchedRoots: roots, count, issues };
}

module.exports = { readPackageMetadata, scanWorkshop };
