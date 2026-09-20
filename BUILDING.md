# Building the Manager

This repository is a publication snapshot, not the full gameplay-Mod workspace. The Windows Portable EXE in Releases is the already-built 0.2.8 artifact; publishing this snapshot does not rebuild or change that EXE.

## Manager

Use Windows x64, Node.js with support for the test flags below, npm, and the Windows .NET Framework compiler at `%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\csc.exe`.

```powershell
npm ci
npm start
npm run dist
```

The build uses the three checked-in `.scdemod` packages under `release/`. Its prebuild step recompiles the SE guard helper from source. `npm run dist` builds only the portable Windows EXE. A rebuilt EXE need not have the same hash as the published artifact.

## Tests

The source workspace's `test/core.test.js` also contains three source-inspection tests for separate Fog/Advanced Control projects. Those three tests and their path constants are excluded from this publication; the Manager and built-in component tests remain.

```powershell
npm test
```

This is not a substitute for real-game or two-player testing. The original workspace and its tests remain unchanged.

## Built-in components

- `mods/scde-multiplayer-compatibility`: source and build scripts for the shipped multiplayer component. Building it requires assemblies from your own legitimate SCDE installation and the matching BepInEx runtime under `third_party/BepInEx_win_x64_5.4.23.5`; pass the actual game path to `build.ps1`. Do not redistribute game assemblies.
- `mods/bepinex-runtime`: package manifest, packaging scripts and original component notices. The prepared runtime package is in `release/`.
- `mods/shcde-script-extender-adapter`: the guard source, equivalent source patch and packaging code. The baseline builder expects the official SE 2.6.0 distribution at `mods/SHCDESE`, matching runtime/game references, and `SCDE_GAME_DIR` pointing to your game.
- `mods/shcde-script-extender-v2.6.0`: upstream core/native-crash-handler source and build scripts for the bundled baseline. Standalone authoring tools, example Mods, reverse-engineering files and the duplicated `deps` distribution are excluded. For a complete upstream build, obtain the corresponding v2.6.0 source distribution from the upstream project and follow its CONTRIBUTING.md; supply game references from your own copy. This is not a claim that a complete SE rebuild was tested from this snapshot.

No original game installation, staged game copy, private credentials, player profile or local logs belong in this repository.
