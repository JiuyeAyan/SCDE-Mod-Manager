const fs = require("node:fs");
const path = require("node:path");
const { execFileSync } = require("node:child_process");
const root = path.resolve(__dirname, "..");
const source = path.join(root, "mods/shcde-script-extender-adapter");
const target = path.join(root, "release/se-update-helper");
fs.mkdirSync(target, { recursive: true });
fs.copyFileSync(path.join(__dirname, "Mono.Cecil.dll"), path.join(target, "Mono.Cecil.dll"));
for (const name of ["EarlyManagedGuard.cs", "early-managed-guard.patch", "GPL-3.0.txt", "Mono.Cecil.LICENSE.txt"]) {
  fs.copyFileSync(path.join(source, name), path.join(target, name));
}
fs.copyFileSync(path.join(source, "UPDATE_NOTICES.txt"), path.join(target, "THIRD_PARTY_NOTICES.txt"));
fs.copyFileSync(path.join(root, "mods/shcde-script-extender-v2.6.0/LICENSE.txt"), path.join(target, "LGPL-3.0.txt"));
execFileSync(path.join(process.env.WINDIR || "C:/Windows", "Microsoft.NET/Framework64/v4.0.30319/csc.exe"), [
  "/nologo", "/target:exe", "/out:" + path.join(target, "EarlyManagedGuard.exe"),
  "/reference:" + path.join(target, "Mono.Cecil.dll"), path.join(source, "EarlyManagedGuard.cs"),
], { stdio: "inherit", windowsHide: true });
console.log("SE update helper built for the Windows .NET Framework; no SDK or .NET 8 needed by players.");
