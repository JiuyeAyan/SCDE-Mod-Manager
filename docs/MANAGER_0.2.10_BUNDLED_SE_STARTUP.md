# Manager 0.2.10: current SE bundle and launch-time game-copy preparation

## Startup behavior

Manager 0.2.9 called `ensureSystemMods(true, true)` while initializing its window.
This could redeploy changed built-ins and invoke the one-time isolation-policy migration,
which recreates an old game copy. The migrated machine's copy directories and receipt
were written on 2026-09-22 around 10:32 and its configuration now has
`stageIsolationVersion: 1`. This supports the first-open/second-open difference, but
there was no per-phase historical timing log to attribute the entire reported delay.

Manager 0.2.10 calls `ensureSystemMods(false, true)` at startup. Opening the manager
installs required components into manager data, but does not deploy/rebuild the game.
Launching creates a missing copy, synchronizes changed Mod payloads, or reuses a current
copy without rewriting it. The existing one-time migration of pre-isolation copies
remains a launch-time safety check; it is not a recurring rebuild of current copies.
Language/config template folders may exist before launch; they are not a copied game.

## Build input

`mods/shcde-script-extender-v2.6.0` contains source whose changelog includes 2.8.0;
its folder name is historical. It does not contain the runtime release DLL.
The user subsequently supplied the unpacked 2.8.0 release under
`mods/SHCDESE/SHCDESE`. Its `SHCDESE.dll` assembly version is `2.8.0.0`, its
`info.json` version is `2.8.0`, and the input DLL SHA-256 is:

`37bb12819ed0ad301d5ecc243fc0b40a80a63cc41ce97d895df4bbda87272cba`

The input distribution/source were read only. `SDK-Workshop-Packager`, vanilla launch
scripts, shared BepInEx loader/core, and upstream configs are not bundled from this input.
The manager's existing five-instruction guard is applied only to the generated SE copy.
The prepared DLL SHA-256 is:

`b6969d931fd29db75acdf4b5def7b063e1c534d47fce6d82a558803c780127d1`

## Future builds

The electron-builder `beforePack` hook runs `tools/prepare-bundled-se.js` for both
`npm run pack` and `npm run dist`. It queries the official latest-release metadata,
downloads that release, checks the actual assembly version, reflected MMC registry
contract, runtime dependencies and updater patch target, and only then publishes the
matched package/manifest under `release/bundled-se`. The app reads this small manifest
instead of hardcoding SE 2.6.0. A network or validation failure stops the build; an old
bundle is not silently used. This network work happens on the developer's build machine,
not in the player's launch path. Successful builds still do not guarantee compatibility
with all future SE or third-party Mod behavior.

Set `SCDE_GAME_DIR` if Steam game detection is unavailable. A developer who has explicitly
downloaded the current release can set `SCDE_SE_DISTRIBUTION` to its unpacked runtime
directory. That opt-in path still checks the latest official version and runs the same
binary preflight; it is not an automatic fallback. The variable was supplied only to
this build process, not saved as a machine-wide setting.

For this build, online metadata returned 2.8.0. Full direct downloads timed out or ended
in a truncated TLS transfer, so the owner's newly supplied distribution was used. Its
original ZIP hash was unavailable; provenance records that explicitly and records the
input DLL hash rather than pretending a repacked directory has an official archive hash.

## Verification

- Candidate validation fixtures include 20 C# interface/dependency/IL cases.
- Real supplied 2.8.0 passed the compiled preparation helper and was packaged with
  BepInEx Runtime 5.4.23.5 and MMC 0.4.0.
- `node tools/verify-bundled-se.js` passed: no shared-runtime overlay, no package
  conflict, copied DLL matches the prepared package, original fixture unchanged,
  player config preserved, no game copy on manager startup, and no recopy on reopen
  or normal Mod synchronization. Report: `release/bundled-se-verification.json`.
- Startup, updater, package-build and deployment regression tests passed. The full
  suite passed 150 of 151 tests and retains the unrelated pre-existing Fog test mismatch (expects radius 100;
  current Fog configuration uses 85). That Mod and assertion were not changed.
- No fresh Unity gameplay or two-PC multiplayer session was performed. No GitHub
  upload, comment, release publication or issue closure was performed.
- The final portable EXE was opened with an isolated fixture/profile: visible 0.2.10
  window, cleared busy state, three built-in cards, and bundled SE 2.8.0+scdemm.1.
  The screenshot shows `Game Copy: Not Ready` before the first launch as intended.
  Embedded changed sources and SE bundle were byte-compared against build inputs.
  UI evidence: `release/portable-0.2.10-check-S0MXxS/verification.txt` and
  `release/manager-0.2.10-portable-startup.png`.

Artifact: `dist/manager-0.2.10/SCDE-Mod-Manager-Portable.exe`.
SHA-256: `5266a7496e1229a420c677b7a26d64bf3573f004f3466dac102f16535258e55e`.

Backup of the initial build/main files: `backups/before-latest-se-build-0.2.10`.
Earlier source baseline: `backups/issue-1-20260922`.
