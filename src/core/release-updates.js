const { setTimeout: delay } = require("node:timers/promises");
const { compareVersions } = require("./workshop-updates");

const SE_REPOSITORY = "https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender";
const MAX_RESPONSE = 1024 * 1024;

function officialSEAsset(release, version) {
  const expected = `https://gitlab.com/api/v4/projects/74440776/packages/generic/shcdese/${encodeURIComponent(version)}/SHCDESE.zip`;
  return release?.assets?.links?.some(link => (link.direct_asset_url || link.url) === expected) ? expected : null;
}

function repositoryEndpoint(value) {
  const invalid = () => { throw new Error("Unsupported release repository URL"); };
  if (typeof value !== "string" || value.length > 2048) return invalid();
  let url;
  try { url = new URL(value); } catch { return invalid(); }
  if (url.protocol !== "https:" || url.username || url.password || url.port || url.search || url.hash) return invalid();
  const host = url.hostname.replace(/^www\./, "");
  let parts;
  try { parts = decodeURIComponent(url.pathname).split("/").filter(Boolean); } catch { return invalid(); }
  if (host === "github.com" && parts[2] === "releases") parts = parts.slice(0, 2);
  if (host === "gitlab.com") {
    const suffix = parts.indexOf("-");
    if (suffix >= 2 && parts[suffix + 1] === "releases") parts = parts.slice(0, suffix);
  }
  if (parts.length < 2) return invalid();
  parts[parts.length - 1] = parts.at(-1).replace(/\.git$/i, "");
  if (parts.some(part => !/^[a-zA-Z0-9_.-]+$/.test(part) || [".", "..", "-"].includes(part))) return invalid();
  const project = parts.join("/");
  if (host === "github.com" && parts.length === 2) return {
    api: `https://api.github.com/repos/${project}/releases/latest`, releases: `https://github.com/${project}/releases`,
  };
  if (host === "gitlab.com") return {
    api: `https://gitlab.com/api/v4/projects/${encodeURIComponent(project)}/releases?per_page=1`, releases: `https://gitlab.com/${project}/-/releases`,
  };
  return invalid();
}

async function latestRelease(endpoint, options) {
  for (let attempt = 1; attempt <= 10; attempt++) {
    options.signal?.throwIfAborted();
    const controller = new AbortController();
    const cancel = () => controller.abort(options.signal.reason);
    options.signal?.addEventListener("abort", cancel, { once: true });
    const timer = setTimeout(() => controller.abort(new Error("Release check timed out")), options.timeoutMs ?? 8000);
    let retryDelay = options.retryDelayMs ?? 3000;
    try {
      const response = await (options.fetch || fetch)(endpoint.api, {
        signal: controller.signal, redirect: "error",
        headers: { "User-Agent": "SCDE-Mod-Manager-Release-Check", Accept: "application/json" },
      });
      if (!response.ok) {
        await response.body?.cancel();
        const retryable = response.status >= 500 || [408, 429].includes(response.status);
        if (response.status === 429) retryDelay = Math.max(retryDelay, Math.min(30000, Number(response.headers.get("retry-after") || 0) * 1000));
        throw Object.assign(new Error(`HTTP ${response.status}`), { reason: "server", permanent: !retryable });
      }
      const chunks = []; let size = 0;
      if (Number(response.headers.get("content-length")) > MAX_RESPONSE) {
        await response.body?.cancel();
        throw Object.assign(new Error("Response too large"), { reason: "metadata", permanent: true });
      }
      for await (const chunk of response.body) {
        size += chunk.length;
        if (size > MAX_RESPONSE) throw Object.assign(new Error("Response too large"), { reason: "metadata", permanent: true });
        chunks.push(chunk);
      }
      let tag, release;
      try {
        const json = JSON.parse(Buffer.concat(chunks).toString("utf8"));
        release = Array.isArray(json) ? json[0] : json;
        tag = release?.tag_name;
      } catch { /* report invalid metadata below */ }
      if (typeof tag !== "string" || tag.length > 128 || compareVersions(tag.trim().replace(/^V/, "v"), "0") === null) {
        throw Object.assign(new Error("Invalid release version"), { reason: "metadata", permanent: true });
      }
      const version = tag.trim().replace(/^[vV]/, "");
      return { version, url: endpoint.releases,
        ...(endpoint.api === repositoryEndpoint(SE_REPOSITORY).api ? { downloadUrl: officialSEAsset(release, version) } : {}) };
    } catch (error) {
      options.signal?.throwIfAborted();
      if (error.permanent || attempt === 10) return { failure: { reason: error.reason || "network", attempts: attempt } };
    } finally {
      clearTimeout(timer);
      options.signal?.removeEventListener("abort", cancel);
    }
    await delay(retryDelay, undefined, { signal: options.signal });
  }
}

async function checkReleaseUpdates(installed, options = {}) {
  options.signal?.throwIfAborted();
  const targets = installed.filter(mod => mod.id === "shcde-script-extender" || mod.scriptExtender?.versionCheckUrl);
  const result = { updates: [], failures: [] };
  const checks = new Map();
  // At most two in-flight repositories; failures never enter the launch path.
  let next = 0;
  await Promise.all([0, 1].map(async () => {
    while (next < targets.length) {
      const mod = targets[next++];
      let endpoint;
      try { endpoint = repositoryEndpoint(mod.id === "shcde-script-extender" ? SE_REPOSITORY : mod.scriptExtender.versionCheckUrl); }
      catch { result.failures.push({ id: mod.id, name: mod.name, reason: "metadata", attempts: 0 }); continue; }
      if (!checks.has(endpoint.api)) checks.set(endpoint.api, latestRelease(endpoint, options));
      const remote = await checks.get(endpoint.api);
      if (remote.failure) result.failures.push({ id: mod.id, name: mod.name, ...remote.failure });
      else if (compareVersions(remote.version, mod.version) === 1) {
        result.updates.push({ id: mod.id, name: mod.name, installedVersion: mod.version, ...remote });
      }
    }
  }));
  return result;
}

module.exports = { repositoryEndpoint, checkReleaseUpdates };
