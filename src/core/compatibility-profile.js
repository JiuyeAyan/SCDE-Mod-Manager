const crypto = require("node:crypto");

const PROFILE_SCHEMA = 2;

function createCompatibilityProfile(mods, enabledIds) {
  const modById = new Map(mods.map((mod) => [mod.id, mod]));
  const enabledMods = enabledIds
    .map((id) => {
      const mod = modById.get(id);
      if (!mod) throw new Error(`找不到已启用的 Mod：${id}`);
      return { id: mod.id, name: mod.name || mod.id, version: mod.version };
    })
    .sort((first, second) => {
      if (first.id < second.id) return -1;
      if (first.id > second.id) return 1;
      return 0;
    });
  const canonical = enabledMods.map((mod) => `${mod.id}\u0000${mod.version}`).join("\n");
  const fingerprint = crypto.createHash("sha256").update(canonical, "utf8").digest("hex");
  // Local deployment provenance is order/content-sensitive; never use wrapper hashes as network identity.
  const deployment = enabledIds.map(id => {
    const mod = modById.get(id);
    return { id, version: mod.version, packageSha256: mod.packageSha256 || "" };
  });
  const deploymentFingerprint = crypto.createHash("sha256").update(JSON.stringify(deployment), "utf8").digest("hex");

  return {
    schema: PROFILE_SCHEMA,
    fingerprint,
    deploymentFingerprint,
    deployment,
    mods: enabledMods,
  };
}

function createLobbyProfileText(profile) {
  const encode = (value) => Buffer.from(String(value), "utf8").toString("base64");
  return [
    `SCDEMM${profile.schema}|${profile.fingerprint}`,
    ...profile.mods.map(
      (mod) => `${encode(mod.id)}|${encode(mod.version)}|${encode(mod.name)}`
    ),
  ].join("\n");
}

module.exports = { createCompatibilityProfile, createLobbyProfileText, PROFILE_SCHEMA };
