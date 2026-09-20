const fs = require("node:fs");
const path = require("node:path");
const crypto = require("node:crypto");
const AdmZip = require("adm-zip");
const { spawnSync } = require("node:child_process");
const project = path.resolve(__dirname, "../..");
const source = path.join(project, "mods/SHCDESE");
const build = path.join(__dirname, "build");
fs.mkdirSync(build, { recursive: true });
fs.copyFileSync(path.join(project, "tools/Mono.Cecil.dll"), path.join(build, "Mono.Cecil.dll"));
const guard = path.join(build, "EarlyManagedGuard.exe");
const patched = path.join(build, "SHCDESE.dll");
const game = process.env.SCDE_GAME_DIR || "D:/0_zhuangji/Softwares/Steam/steamapps/common/Stronghold Crusader Definitive Edition";
fs.copyFileSync(path.join(__dirname, "EarlyManagedGuard.runtimeconfig.json"), path.join(build, "EarlyManagedGuard.runtimeconfig.json"));
for (const [command, args] of [
  ["C:/Windows/Microsoft.NET/Framework64/v4.0.30319/csc.exe", ["/nologo", "/target:exe", "/out:" + guard, "/reference:" + path.join(build, "Mono.Cecil.dll"), path.join(__dirname, "EarlyManagedGuard.cs")]],
  ["dotnet", [guard, path.join(source, "BepInEx/plugins/000shcdese/SHCDESE.dll"), patched,
    path.join(game, "Stronghold Crusader Definitive Edition_Data/Managed"), path.join(project, "third_party/BepInEx_win_x64_5.4.23.5/BepInEx/core")]],
]) {
  const result = spawnSync(command, args, { stdio: "inherit", windowsHide: true });
  if (result.error || result.status !== 0) throw result.error || new Error("SE guard build failed");
}
const zip = new AdmZip();
const manifest = JSON.parse(fs.readFileSync(path.join(__dirname, "manifest.json"), "utf8"));
zip.addFile("manifest.json", Buffer.from(JSON.stringify(manifest, null, 2)));
function addTree(directory) {
  for (const entry of fs.readdirSync(directory, { withFileTypes: true })) {
    if (entry.isSymbolicLink()) throw new Error(`Unexpected link: ${entry.name}`);
    const file = path.join(directory, entry.name);
    if (entry.isDirectory()) addTree(file);
    else if (entry.isFile()) {
      const relative = path.relative(source, file).split(path.sep).join("/");
      zip.addFile(`payload/${relative}`, fs.readFileSync(relative === "BepInEx/plugins/000shcdese/SHCDESE.dll" ? patched : file));
    }
  }
}
// Only this allowlist is deployed. No game files, launch scripts, configs or BepInEx/core.
addTree(path.join(source, "BepInEx/plugins"));
zip.addFile("payload/msvcp140.dll", fs.readFileSync(path.join(source, "msvcp140.dll")));
const notices = "payload/BepInEx/plugins/000shcdese/SCDEMM-notices/";
zip.addFile(notices + "LGPL-3.0.txt", fs.readFileSync(path.join(project, "mods/shcde-script-extender-v2.6.0/LICENSE.txt")));
for (const name of ["GPL-3.0.txt", "THIRD_PARTY_NOTICES.txt"]) zip.addFile(notices + name, fs.readFileSync(path.join(__dirname, name)));
for (const name of ["early-managed-guard.patch", "EarlyManagedGuard.cs", "EarlyManagedGuard.runtimeconfig.json"]) zip.addFile(notices + name, fs.readFileSync(path.join(__dirname, name)));
const upstreamSource = fs.readFileSync(path.join(project, "mods/shcde-script-extender-v2.6.0/src/SHCDESE.BepInEx/API/Components/Archive/MapModManager.cs"), "utf8");
const entry = /private void TryUpdateModsFromRemote\(\)\s*\{/;
if (!entry.test(upstreamSource)) throw new Error("SE source guard location not found");
zip.addFile(notices + "MapModManager.cs", Buffer.from(upstreamSource.replace(entry,
  '$&\n        // SCDE Mod Manager owns deployment before other plugins initialize Steam.\n        if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SCDEModManagerLaunchId"))) return;')));
const output = path.join(project, "release", `shcde-script-extender-${manifest.version}.scdemod`);
zip.writeZip(output);
console.log(`SCRIPT_EXTENDER_PACKAGE_OK entries=${zip.getEntryCount()} sha256=${crypto.createHash("sha256").update(fs.readFileSync(output)).digest("hex")} path=${output}`);
