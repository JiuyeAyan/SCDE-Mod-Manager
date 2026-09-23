# Building the Manager 0.2.10

This repository is a curated publication snapshot, not the full gameplay-Mod workspace. Updating this source does not replace existing GitHub Release EXEs.

## Manager

Use Windows x64, Node.js 24 (tested), npm, and the Windows .NET Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`. Application packaging needs a legitimate local SCDE installation and network access to official SE releases. Set `SCDE_GAME_DIR` if Steam detection is unavailable.

Restore the shared BepInEx build references from the included runtime package; no game files are copied by this command:

```powershell
npm ci
node -e "const Z=require('adm-zip');new Z('release/bepinex-runtime-5.4.23.5.scdemod').extractEntryTo('payload/', 'third_party/BepInEx_win_x64_5.4.23.5', false, true)"
npm test
npm start
npm run dist
```

The prepared resources contain BepInEx 5.4.23.5, MMC 0.4.0 and SE 2.8.0+scdemm.1. `npm start` uses those resources. Packaging runs `tools/prepare-bundled-se.js`: it rebuilds the guard helper, obtains the latest official SE release, validates it against the shared runtime and local game assemblies, and replaces `release/bundled-se` only after success. Download/validation failure stops packaging; there is no silent fallback to an old bundle.

If you manually downloaded the current official release, set `SCDE_SE_DISTRIBUTION` to its extracted directory containing `BepInEx`. This explicit option still queries the latest official version and runs the same validation. The checked-in 2.8.0 bundle was prepared from an owner-supplied extracted release; provenance records its input DLL hash and that the original ZIP hash is unavailable. Structural validation is not a guarantee of all third-party gameplay compatibility.

`npm run dist` produces only `dist/SCDE-Mod-Manager-Portable.exe`. `npm run pack` is for development inspection, not a second player distribution format. A rebuilt EXE need not have the same hash as a previously published artifact.

## Tests

The source workspace's `test/core.test.js` also contains three source-inspection tests for separate Fog/Advanced Control projects. Those three tests and their path constants are excluded from this publication; the Manager and built-in component tests remain.

```powershell
npm test
```

All 148 tests in this publication passed on Windows with Node.js 24.19.0. This is not a substitute for real-game or two-player testing. The original workspace and its tests remain unchanged.

## Built-in components

- `mods/scde-multiplayer-compatibility`: source and build scripts for MMC 0.4.0. Building it requires assemblies from your own legitimate SCDE installation and the matching BepInEx runtime under `third_party/BepInEx_win_x64_5.4.23.5`; pass the actual game path to `build.ps1`. Its separate regression fixture also needs a suitable `dotnet` runtime. Do not redistribute game assemblies.
- `mods/bepinex-runtime`: package manifest, packaging scripts and original component notices. The prepared runtime package is in `release/`.
- `mods/shcde-script-extender-adapter`: guard source, equivalent source patch and historical baseline packaging code. `build.js` is the older fixed-2.6.0 utility; current Manager packaging uses the latest-SE hook described above.
- `mods/shcde-script-extender-v2.6.0`: historical directory name containing the current locally supplied upstream core/native-crash-handler source subset. Standalone authoring tools, example Mods, reverse-engineering files and the duplicated `deps` distribution are excluded. For a complete upstream build, obtain the corresponding source/dependencies from the upstream project and follow its CONTRIBUTING.md; supply game references from your own copy. This is not a claim that a complete SE rebuild was tested from this snapshot.

No original game installation, staged game copy, private credentials, player profile or local logs belong in this repository.
