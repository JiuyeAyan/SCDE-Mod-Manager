const test = require("node:test");
const assert = require("node:assert/strict");
const { repositoryEndpoint, checkReleaseUpdates } = require("../src/core/release-updates");
const core = { id: "shcde-script-extender", name: "Script Extender", version: "2.6.0" };
const response = (tag) => new Response(JSON.stringify({ tag_name: tag }), { status: 200 });

test("release endpoints accept only supported public HTTPS repositories", () => {
  assert.match(repositoryEndpoint("https://gitlab.com/group/project").api, /^https:\/\/gitlab.com\/api\/v4\/projects\/group%2Fproject/);
  assert.match(repositoryEndpoint("https://github.com/owner/project/releases").api, /^https:\/\/api.github.com\/repos\/owner\/project/);
  for (const url of ["http://github.com/a/b", "https://localhost/a/b", "https://github.com.evil/a/b", "https://me@github.com/a/b", "https://github.com:8443/a/b", "https://github.com/a/b?token=secret"]) {
    assert.throws(() => repositoryEndpoint(url), /unsupported/i);
  }
});

test("SE core and installed SE Mod releases are compared without downloading or installing", async () => {
  const requested = [];
  const result = await checkReleaseUpdates([core, { id: "se-example", name: "Example", version: "1.0", scriptExtender: { versionCheckUrl: "https://github.com/owner/project" } }, { id: "se-no-url", version: "1", scriptExtender: {} }], {
    fetch: async (url) => { requested.push(url); return response(url.includes("gitlab.com") ? "v2.7.0" : "v1.1"); },
  });
  assert.equal(requested.length, 2);
  assert.equal(result.updates.length, 2);
  assert.equal(result.updates[0].version, "2.7.0");
  assert.ok(result.updates.every(item => /^https:\/\/(github|gitlab)\.com\//.test(item.url)));
});

test("network failure gets exactly ten bounded attempts then one aggregate result", async () => {
  let calls = 0;
  const result = await checkReleaseUpdates([core], { fetch: async () => { calls++; throw new Error("offline"); }, retryDelayMs: 0 });
  assert.equal(calls, 10);
  assert.equal(result.failures.length, 1);
  assert.equal(result.failures[0].reason, "network");
  assert.equal(result.failures[0].attempts, 10);
});

test("a timed-out request can be cancelled and never waits indefinitely", async () => {
  const controller = new AbortController();
  const promise = checkReleaseUpdates([core], { signal: controller.signal, fetch: (_url, { signal }) => new Promise((_resolve, reject) => signal.addEventListener("abort", () => reject(signal.reason), { once: true })), timeoutMs: 5, retryDelayMs: 0 });
  const result = await promise;
  assert.equal(result.failures[0].attempts, 10);
  controller.abort();
  await assert.rejects(checkReleaseUpdates([core], { signal: controller.signal }), /abort/i);
});

test("invalid releases, HTTP errors and oversized responses are not misreported as newer versions", async () => {
  for (const fetch of [async () => response("unrelated-latest"), async () => new Response("missing", { status: 404 }), async () => new Response("x".repeat(1048577))]) {
    const result = await checkReleaseUpdates([core], { fetch, retryDelayMs: 0 });
    assert.equal(result.updates.length, 0);
    assert.equal(result.failures.length, 1);
    assert.equal(result.failures[0].attempts, 1);
    assert.notEqual(result.failures[0].reason, "network");
  }
});
