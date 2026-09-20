const fs = require("node:fs/promises");
const nativeFs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const { pipeline } = require("node:stream/promises");
const { Readable } = require("node:stream");
const yauzl = require("yauzl");
const { safeJoin } = require("./paths");
const { compareVersions } = require("./workshop-updates");

const MAX_INFO = 256 * 1024;
const MAX_PAYLOAD = 1024 * 1024 * 1024;
const RESERVED = new Set(["000shcdese", "uuimgui", "scdemultiplayercompatibility"]);

function safeArchivePath(name) {
  const normalized = name.replaceAll("\\", "/").replace(/\/$/, "");
  if (!normalized || normalized.split("/").some(part => !part || part === "." || part === ".." ||
    /[<>:"|?*\x00-\x1f]/.test(part) || /[. ]$/.test(part) || /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(?:\.|$)/i.test(part))) {
    throw new Error(`SE unsafe archive path: ${name}`);
  }
  return normalized;
}

function seVersionBounds(info) {
  // Older packs use top-level bounds; SE 2.7 declares them on the core GUID.
  const coreRequirements = [{ MinimumVersion: info.MinimumScriptExtenderVersion, MaximumVersion: info.MaximumScriptExtenderVersion },
    ...(Array.isArray(info.Dependencies) ? info.Dependencies.filter(item => typeof item?.GUID === "string" && item.GUID.trim().toLowerCase() === "000shcdese") : [])];
  let minimumVersion = "", maximumVersion = "";
  for (const requirement of coreRequirements) {
    for (const field of ["MinimumVersion", "MaximumVersion"]) {
      const value = requirement[field];
      if (value == null || (typeof value === "string" && !value.trim())) continue;
      if (typeof value !== "string" || compareVersions(value.trim(), "0") === null) throw new Error(`SE invalid ${field}`);
      if (field === "MinimumVersion" && (!minimumVersion || compareVersions(value.trim(), minimumVersion) === 1)) minimumVersion = value.trim();
      if (field === "MaximumVersion" && (!maximumVersion || compareVersions(value.trim(), maximumVersion) === -1)) maximumVersion = value.trim();
    }
  }
  if (minimumVersion && maximumVersion && compareVersions(minimumVersion, maximumVersion) === 1) throw new Error("SE inconsistent version requirements");
  return { minimumVersion, maximumVersion };
}

function metadata(info) {
  if (!info || typeof info !== "object") throw new Error("SE info.json is invalid");
  for (const field of ["GUID", "Name", "Version"]) {
    if (typeof info[field] !== "string" || !info[field].trim()) throw new Error(`SE info.json missing ${field}`);
  }
  const guid = safeArchivePath(info.GUID.trim());
  if (guid.includes("/") || guid.length > 128 || RESERVED.has(guid.toLowerCase())) throw new Error("SE reserved or unsafe GUID");
  if (![0, 1].includes(info.Manifest ?? 0) || ![0, 1].includes(info.NetworkMode ?? 0)) throw new Error("SE unsupported mod type");
  const { minimumVersion, maximumVersion } = seVersionBounds(info);
  return {
    id: "se-" + crypto.createHash("sha256").update(guid.toLowerCase()).digest("hex").slice(0, 32),
    name: info.Name.trim(), version: info.Version.trim(), author: typeof info.Author === "string" ? info.Author : "",
    description: typeof info.Description === "string" ? info.Description : "", gameVersion: "",
    dependencies: [{ id: "shcde-script-extender" }],
    scriptExtender: { guid, versionCheckUrl: typeof info.VersionCheckUrl === "string" ? info.VersionCheckUrl : "",
      minimumVersion, maximumVersion, metadataVersion: 2 },
  };
}

// SE appends a normal ZIP to a binary map. Read its tail/central directory, not the full map.
async function openArchive(filePath) {
  const file = await fs.open(filePath, "r");
  try {
    const { size } = await file.stat();
    if (size > MAX_PAYLOAD) throw new Error("SE package is too large");
    const tail = Buffer.alloc(Math.min(size, 65557));
    await file.read(tail, 0, tail.length, size - tail.length);
    let end = tail.length - 22;
    while (end >= 0 && !(tail.readUInt32LE(end) === 0x06054b50 && end + 22 + tail.readUInt16LE(end + 20) === tail.length)) end--;
    if (end < 0) { await file.close(); return null; }
    if (tail.readUInt16LE(end + 4) || tail.readUInt16LE(end + 6) || tail.readUInt32LE(end + 16) === 0xffffffff) {
      throw new Error("SE unsupported split/ZIP64 map archive");
    }
    const offset = size - tail.length + end - tail.readUInt32LE(end + 12) - tail.readUInt32LE(end + 16);
    if (offset < 0) throw new Error("SE invalid archive offset");
    class SliceReader extends yauzl.RandomAccessReader {
      _readStreamForRange(start, finish) {
        return Readable.from((async function* () {
          for (let position = start; position < finish;) {
            const buffer = Buffer.alloc(Math.min(65536, finish - position));
            const { bytesRead } = await file.read(buffer, 0, buffer.length, offset + position);
            if (!bytesRead) throw new Error("SE unexpected end of archive");
            position += bytesRead;
            yield buffer.subarray(0, bytesRead);
          }
        })());
      }
      read(buffer, start, length, position, callback) { nativeFs.read(file.fd, buffer, start, length, offset + position, callback); }
    }
    const zip = await yauzl.fromRandomAccessReaderPromise(new SliceReader(), size - offset, { lazyEntries: true, autoClose: false });
    return { zip, close: async () => { zip.close(); await file.close(); } };
  } catch (error) { await file.close(); throw error; }
}

async function inspectArchive(archive, filePath, signal) {
  const { zip } = archive;
  if (zip.entryCount > 100000) throw new Error("SE too many archive entries");
  const entries = [], names = new Set();
  let info, total = 0;
  for await (const entry of zip.eachEntry()) {
    signal?.throwIfAborted();
    const name = safeArchivePath(entry.fileName), key = name.toLowerCase();
    if (names.has(key)) throw new Error(`SE duplicate archive path: ${name}`);
    names.add(key);
    if ((entry.externalFileAttributes >>> 16 & 0xf000) === 0xa000) throw new Error("SE unsafe symbolic link");
    if (entry.fileName.endsWith("/")) continue;
    total += entry.uncompressedSize;
    if (entry.uncompressedSize > 256 * 1024 * 1024 || total > MAX_PAYLOAD) throw new Error("SE payload is too large");
    entries.push({ entry, name });
    if (key !== "info.json") continue;
    if (entry.uncompressedSize > MAX_INFO) throw new Error("SE info.json is too large");
    const stream = await zip.openReadStreamPromise(entry);
    const abort = () => stream.destroy(signal.reason);
    signal?.addEventListener("abort", abort, { once: true });
    try {
      const chunks = []; let bytes = 0;
      for await (const chunk of stream) {
        signal?.throwIfAborted(); bytes += chunk.length;
        if (bytes > MAX_INFO) throw new Error("SE info.json is too large");
        chunks.push(chunk);
      }
      info = JSON.parse(Buffer.concat(chunks).toString("utf8"));
    } finally { signal?.removeEventListener("abort", abort); stream.destroy(); }
  }
  if (!info) return null;
  const manifest = metadata(info);
  const packed = path.extname(filePath).toLowerCase() === ".semod";
  if (packed && (info.Manifest ?? 0) !== 0) throw new Error("SE .semod supports asset/Lua mods only");
  if (!packed && !entries.some(item => /^BepInEx\/plugins\//i.test(item.name))) return null;
  for (const { name } of entries) {
    if (packed || !/^BepInEx\//i.test(name)) continue;
    const parts = name.split("/");
    if (parts[1].toLowerCase() === "plugins" && parts.length >= 4 && !RESERVED.has(parts[2].toLowerCase())) continue;
    if (parts[1].toLowerCase() === "config" && /\.(cfg|toml|json|ini|xml|yaml|yml|txt)$/i.test(name)) continue;
    throw new Error(`SE reserved or unsupported deployment path: ${name}`);
  }
  return { manifest, info, entries, packed };
}

async function readSEPackageMetadata(filePath, signal) {
  signal?.throwIfAborted();
  const archive = await openArchive(filePath);
  if (!archive) return null;
  try {
    const result = await inspectArchive(archive, filePath, signal);
    return result ? { ...result.manifest, path: filePath, format: "se" } : null;
  } finally { await archive.close(); }
}

async function refreshInstalledSEMetadata(manifest, folder) {
  if (!manifest.scriptExtender || manifest.scriptExtender.metadataVersion === 2) return manifest;
  const guid = safeArchivePath(manifest.scriptExtender.guid);
  if (guid.includes("/")) throw new Error("SE unsafe installed GUID");
  const base = safeJoin(folder, `payload/BepInEx/plugins/${guid}`);
  let refreshed;
  try {
    const file = await fs.open(path.join(base, "info.json"), "r");
    try {
      if ((await file.stat()).size > MAX_INFO) throw new Error("SE info.json too large");
      refreshed = metadata(JSON.parse(await file.readFile("utf8")));
    } finally { await file.close(); }
  } catch (error) {
    if (error.code !== "ENOENT") throw error;
    try { refreshed = await readSEPackageMetadata(base + ".semod"); }
    catch (packedError) { if (packedError.code !== "ENOENT") throw packedError; }
  }
  if (!refreshed) return manifest;
  if (refreshed.id !== manifest.id || refreshed.version !== manifest.version) throw new Error("SE installed metadata does not match its package");
  return { ...manifest, scriptExtender: refreshed.scriptExtender };
}

async function stageSEPackage(filePath, temporaryRoot, expected) {
  const archive = await openArchive(filePath);
  if (!archive) throw new Error("SE mod archive not found");
  try {
    const result = await inspectArchive(archive, filePath);
    if (!result || result.manifest.id !== expected.id || result.manifest.version !== expected.version) throw new Error("MOD_CHANGED_RESCAN");
    const payload = path.join(temporaryRoot, "payload");
    const pluginRoot = `BepInEx/plugins/${result.manifest.scriptExtender.guid}`;
    if (result.packed) {
      const destination = safeJoin(payload, pluginRoot + ".semod");
      await fs.mkdir(path.dirname(destination), { recursive: true });
      await fs.copyFile(filePath, destination);
    } else {
      for (const { entry, name } of result.entries) {
        if (!/^BepInEx\//i.test(name)) continue;
        const destination = safeJoin(payload, name.replace(/^bepinex\/(plugins|config)\//i, (_, folder) => `BepInEx/${folder.toLowerCase()}/`));
        await fs.mkdir(path.dirname(destination), { recursive: true });
        await pipeline(await archive.zip.openReadStreamPromise(entry), nativeFs.createWriteStream(destination, { flags: "wx" }));
      }
      // Match SE MapModManager: root info.json is authoritative and installed beside the plugin.
      const infoPath = safeJoin(payload, pluginRoot + "/info.json");
      await fs.mkdir(path.dirname(infoPath), { recursive: true });
      await fs.writeFile(infoPath, JSON.stringify(result.info, null, 2) + "\n");
    }
    const hash = crypto.createHash("sha256");
    for await (const chunk of nativeFs.createReadStream(filePath)) hash.update(chunk);
    return { ...result.manifest, packageSha256: hash.digest("hex") };
  } finally { await archive.close(); }
}

module.exports = { readSEPackageMetadata, stageSEPackage, safeArchivePath, refreshInstalledSEMetadata, seVersionBounds };
