# GitHub issue #1 — Manager 0.2.9 / MMC 0.4.0

Issue: <https://github.com/JiuyeAyan/SCDE-Mod-Manager/issues/1>

Local implementation, 2026-09-22. This document does not mean the changes have been uploaded, the issue closed, or a two-PC match verified. Original Steam files, subscriptions, upstream SE sources and Serps code were not edited. Pre-change files are in `backups/issue-1-20260922/`.

## Changes

| Issue | Implementation |
| --- | --- |
| Dirty original installation | Initial copy excludes the original `BepInEx`, manager receipts and system-owned root files. Removing a deployed component never restores those files from the original. Existing copies are rebuilt once under isolation policy 1. Subsequent valid launches keep the fast receipt path. |
| Mutable data | Versioned `persistentPaths` declarations preserve explicitly owned plugin data. Configs remain player-owned. Legacy migration covers SE-related DLL directories' `LobbyModSettings/*.msgpack` and `*.bin`, plus the `fixes` GUID's `data/hopsFarmWhitelist.json`. No blanket plugin/data-directory migration or automatic `aobcache.json` preservation. |
| Recovery | Data is backed up outside the copy, restored after deployment, and retained with its path in the error on failure. Packaged non-config content colliding with preserved data fails before stage mutation. A failed partial deployment invalidates its receipt, even if ID/version/order stayed unchanged. |
| System ownership | Built-in inventories and structural roots reserve runtime, SE, MMC and manager-owned paths. Public imports cannot replace configured built-in IDs. Conflicts reject before installation swap and before deployment writes; ordinary Mod-to-Mod last-wins ordering remains. Windows alias paths and linked deployment/data paths are rejected. |
| Multiplayer identity | Protocol v4 compares the actual BepInEx loader, plugin GUID/version pairs, and networked SE resource-only entries. Package wrappers, order and archive hashes remain local provenance, not duplicate network identities. Standalone MMC needs no Manager receipt; a malformed present receipt never silently becomes standalone. |
| Mixed SE rooms | Per the owner's choice, SE rooms can admit peers without MMC, with an explicit unverified warning. SE has a room marker but no per-member presence marker: a host cannot prove an uninstrumented member's SE installation. No-MMC hosts without the SE marker, malformed/old MMC advertisements, and mismatching v4 peers are not silently accepted. MMC is client-side in SE metadata so SE's own filter does not reject its absence; MMC still checks itself strictly between v4 peers. |
| SE candidates and game patches | Before installation, verify the SE APIs consumed by MMC, direct dependencies and shared-runtime member references, and the updater entry/IL anchor. Harmony targets also require matching type/signature/IL shape. Unsupported initialization produces a permanent warning and launch-ID-scoped failure notification, never a ready signal. This is structural validation, not a promise of future compatibility. |
| Package revisions and schema | Preserve imported archive SHA-256. Background checks hash only installed, matching-version `.scdemod` candidates with a known baseline; changed bytes are offered for manual confirmation and checked again before the installation swap. Schema 1 documents stable IDs, payload ownership, exact dependencies and inclusive minimum/maximum bounds. See `SCDEMOD_SCHEMA.md`. |

## Behavior kept unchanged

- SE's own filters, asset checks and anti-tamper are not replaced. The previously agreed Manager-mode automatic deployment/restart guard is not redesigned.
- No Serps performance or API-shim patch is added.
- `.scdemod` updates still need player confirmation. Hashes prove a change, not author identity or absence of malware.
- User configurations are not part of the multiplayer fingerprint. Unverified mixed rooms are not described as fully checked or cheat-proof.
- A component whose game patches cannot initialize cannot protect multiplayer. The player must exit/update; the manager does not forcibly terminate the game.

## Verification and release boundary

Automated tests cover synthetic dirty installations, file ownership/aliases, forced rebuilds, declared/legacy settings, failed recovery, stale receipts, archive revision consent, ranges, standalone/managed receipts, runtime identities, SE interfaces/dependencies and Harmony IL shapes. Real game assemblies are read-only build/test references, not a substitute for a running Unity session.

Before public release, test these on two PCs:

1. Same v4 runtime components via Manager vs standalone MMC; compare GUID and version mismatches.
2. Manager joining an SE-only host, and an SE-only member joining a Manager host; verify the explicit unverified warnings on joining/starting and SE's own checks.
3. Old/malformed MMC advertisements must not downgrade to SE-only acceptance.
4. Real Serps lobby settings and Fixes whitelist survive a forced rebuild, while derived caches are regenerated.
5. A newly downloaded official SE candidate passes preparation and actual startup; then check both hosts and guests.

The local full Node suite has one unrelated pre-existing Fog-of-War assertion expecting base radius 100 while its current config is 85. This task does not change that Mod or its gameplay setting.

### Local results

- Full Node suite: **147 passed / 148 total**; only the unrelated Fog radius assertion fails. The issue-focused checks pass, including 17 deployment-safety cases, the import consent/IPC tests and SE candidate fixtures (20 internal Cecil cases).
- MMC built against this computer's real read-only game assemblies: **83 runtime-profile/policy assertions passed**, plus `SPARSE_NATIVE_KEYMAP_OK` (original native key-map failure reproduced, patched copy succeeds).
- SE 2.6.0 raw input: full preparation succeeds with unchanged five-instruction guard output. Existing adapted SE 2.8.0: read-only interface/dependency/anchor checks pass. A fresh official 2.8.0 download did not finish within the bounded attempt; its incomplete artifact was removed. No fresh raw 2.8.0 full preparation, game launch or two-PC match is claimed.
- Isolated portable startup: visible versioned 0.2.9 window, loading spinner cleared, three built-in components; 23 extracted application files matched the current source. No real game was started. The restricted-environment attempt could not load the renderer; the same isolated test passed under normal desktop permissions, without disabling Electron sandboxing or changing security settings.
- Packaging checks additionally compare the new safety modules, helper source/binary/notices and all three bundled packages against their current inputs. No user `manager-data`, backups, game assemblies, test fixtures or build caches belong in the distributable.

### Main source anchors

| File | Entry point |
| --- | --- |
| `src/core/deployer.js` | `deploymentPlan` (105), `prepareStage` (116), `applyMods` (166) |
| `src/core/system-path-policy.js` | `systemPathPolicy` (5) |
| `src/core/persistent-data.js` | `persistenceRules` (18), `assertNoPersistentCollisions` (72) |
| `src/core/manager.js` | `#installPackages` (388), `redeploy` (598) |
| `src/core/compatibility-profile.js` | `createCompatibilityProfile` (5): local ordered/archive provenance |
| `src/core/manifest-contract.js` | `dependencyVersionMatches` (28) |
| `mods/shcde-script-extender-adapter/EarlyManagedGuard.cs` | `ValidateRegistry` (95), `CandidateResolver.ValidateDependencies` (154) |
| `mods/scde-multiplayer-compatibility/src/PatchTargetGuard.cs` | `ValidateGameTargets` (49) |

Line numbers identify this local 0.2.9 snapshot; method names are the durable lookup keys. The full SE/MMC rationale is in `SCRIPT_EXTENDER_INTEGRATION_0.2.0.md`, section 14.

### Final local artifacts

- Portable: `dist/manager-0.2.9/SCDE-Mod-Manager-Portable.exe`, EXE file/product version **0.2.9**.
- Portable SHA-256: `84da3b0ec7d66704dea0b3773efd9a5c6e8a4c6957c3ae17143821a5c68da47f`.
- MMC package: `release/scde-multiplayer-compatibility-0.4.0.scdemod`, SHA-256 `e5db081602ef50012c72f2eacbcefa2c2bd7e1693cd362557cceea171ff78700`.
- Final post-rebuild startup test: `release/portable-0.2.9-check-cFMzC5/verification.txt`; screenshot: `release/manager-0.2.9-portable-startup.png`.
- Final unpacked resources match all three package inputs and the seven new/changed safety modules; the startup test additionally matches 23 application files. Runtime and SE bootstrap packages retain their existing versions.

The build used the already installed Electron 42.4.1 distribution and a workspace-local build-tool cache after the default user-cache location was inaccessible in the restricted environment. No runtime dependency versions or security settings were changed to bypass the build problem. Build caches, verification directories and pre-change backups are not release/upload content.

GitHub has not been modified by this task: no push, Release, issue comment or issue closure. The public repository's baseline comparison showed only a README edit between the old curated checkout and issue baseline `918aa87`; that author's edit must be preserved when publishing these fixes.
