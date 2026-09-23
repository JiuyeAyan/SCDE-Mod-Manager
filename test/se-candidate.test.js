const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const { execFileSync } = require("node:child_process");
const AdmZip = require("adm-zip");
const { prepareSE } = require("../src/core/se-core-package");

test("SE candidate preflight rejects broken reflected APIs and shared-runtime dependencies before writing", {
  skip: process.platform !== "win32" ? "The shipped helper targets Windows .NET Framework" : false,
}, async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-se-candidate-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const cecil = path.join(root, "Mono.Cecil.dll");
  await fs.copyFile(path.join(__dirname, "../tools/Mono.Cecil.dll"), cecil);
  const executable = path.join(root, "SECandidateValidationTests.exe");
  execFileSync(path.join(process.env.WINDIR || "C:/Windows", "Microsoft.NET/Framework64/v4.0.30319/csc.exe"), [
    "/nologo", "/target:exe", "/main:SECandidateValidationTests", "/out:" + executable, "/reference:" + cecil,
    path.join(__dirname, "../mods/shcde-script-extender-adapter/EarlyManagedGuard.cs"),
    path.join(__dirname, "fixtures/SECandidateValidationTests.cs"),
  ], { windowsHide: true });
  const output = execFileSync(executable, [root], { encoding: "utf8", windowsHide: true, timeout: 30000 });
  assert.match(output, /SE_CANDIDATE_TESTS_OK cases=20/);
});

test("a failed SE candidate preflight never publishes a prepared manifest or replaces the input DLL", async t => {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), "scdemm-se-rejected-"));
  t.after(() => fs.rm(root, { recursive: true, force: true }));
  const zip = new AdmZip();
  const dll = "BepInEx/plugins/000shcdese/SHCDESE.dll";
  zip.addFile(dll, Buffer.from("unchanged candidate"));
  await assert.rejects(prepareSE({ version: "2.8.0",
    downloadUrl: "https://gitlab.com/api/v4/projects/74440776/packages/generic/shcdese/2.8.0/SHCDESE.zip",
  }, root, { gameManagedRoot: root, runtimeRoot: root, helperRoot: root,
    fetch: async () => new Response(zip.toBuffer()),
    execFile: async (_executable, args) => {
      assert.equal(args[3], root);
      throw Object.assign(new Error("preflight rejected"), { stderr: "Unsupported SE registry singleton." });
    },
  }), /SE compatibility preparation failed: Unsupported SE registry singleton/);
  assert.equal(await fs.readFile(path.join(root, "payload", dll), "utf8"), "unchanged candidate");
  await assert.rejects(fs.access(path.join(root, "manifest.json")), { code: "ENOENT" });
  await assert.rejects(fs.access(path.join(root, "payload", dll + ".patched")), { code: "ENOENT" });
});
