# Script Extender integration: consolidated author handoff

Updated: 2026-09-22. Workspace: `E:\z-GameDev\1-StrongHOld\SCModing`.

This document retains its original filename for existing references. It includes released Manager changes through 0.2.8 and the local 0.2.9 / MMC 0.4.0 candidate in section 14, not just the original 0.2.0 integration. Separate earlier change reports remain historical records; their policy descriptions must be read in their release context.

## 1. Current status and correction

- The unreleased GitHub issue #1 work is documented in section 14. It supersedes v3's duplicate package/runtime network comparison and strengthens candidate/patch validation. The versioned release descriptions below remain historical records, not a claim that the new source has already been published or tested in a live match.

- Manager **0.2.8**; shared BepInEx Runtime **5.4.23.5**; Multiplayer Mod Compatibility (MMC) **0.3.1**; bundled Script Extender (SE) **2.6.0+scdemm.1**. The existing updater can prepare official 2.8.0. The player installation and game copy inspected for 0.2.7 contained **2.8.0+scdemm.1**; see sections 11 and 12 for the verification boundaries. Section 13 documents the optional-SE policy added in 0.2.8.
- `2.6.0+scdemm.1` is one adapted build, not two Mods. The upstream base is 2.6.0; the suffix identifies our integration build. The runtime bridge belongs to MMC, not an empty separate "SE Compatibility" package.
- The supplied upstream source `mods/shcde-script-extender-v2.6.0` and distribution `mods/SHCDESE` are unmodified. Steam's original game and subscribed packages are not deployment targets.
- **The deployed SE DLL is modified.** Since Manager 0.2.2, five IL instructions are inserted at the entry of `MapModManager.TryUpdateModsFromRemote`. The original 0.2.0 statement that distributed binaries were unmodified no longer describes the current build.
- MMC applies separate runtime Harmony prefixes. These are not the five-instruction disk patch.
- On 2026-09-18 the owner explicitly requested **no change to SE update behavior while discussing it with the SE author**. Version 0.2.5 retains the existing SE policies. The proposed automatic deployment/restart handoff is **not implemented**.
- Ordinary `.scdemod` updates and Manager self-updates remain user-confirmed, not silently accepted.

## 2. Distinguish the update mechanisms

| Mechanism | Current implementation and policy |
|---|---|
| Upstream SE core release notification | `ReleaseUpdateChecker` reads release metadata; this checker alone does not install a new core DLL |
| Manager's SE core installer (0.2.4+) | Automatically prepares an allowed official runtime release with the integration guard; recovery backups remain; 0.2.8 removes rollback/reapply UI; game-running updates wait |
| Upstream SE Workshop Mod install/update/unsubscribe | `MapModManager` stages plugins/assets and launches an external updater; still blocked in managed mode |
| Imported package updates | Downloaded Workshop versions of installed `.scdemod` and imported SE Mods are compared; the user selects and confirms updates |
| Manager EXE update (0.2.5) | Only its own Workshop item is inspected; the user confirms replacement while the game is closed |

Approval to install untested official **SE core** versions is separate from a request to restore **SE Mod** automatic updates. The latter is now paused by the owner. Installed SE Mods still load; no native-SE update block was removed in 0.2.5.

## 3. Exact SE-related changes

### 3.1 Early managed-mode guard (0.2.2)

Equivalent upstream source change at the beginning of `MapModManager.TryUpdateModsFromRemote()`:

```csharp
// The manager owns deployment. Check before any plugin can initialize Steam.
if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("SCDEModManagerLaunchId")))
    return;

```

The handoff patch adds **4 source lines including the comment and blank line**. The supplied upstream tree itself has **0 edited source lines**. The generated DLL receives **5 additional IL instructions**, not "5 bytes changed." Without the launch marker, its original method body executes.

A real Serps startup initialized Steam before MMC installed its runtime prefix. The early guard prevents that ordering race; a late prefix alone was insufficient.

| Integration file | Current line / count | Purpose |
|---|---|---|
| `mods/shcde-script-extender-adapter/EarlyManagedGuard.cs` | 17; 76 total lines | Input validation and patch tool |
| Same file | 55-59 | Five IL insertions; original opcode sequence verified |
| `mods/shcde-script-extender-adapter/early-managed-guard.patch` | 3; +4 / -0 equivalent source lines | Author-facing patch, not applied to the supplied upstream tree |
| `mods/shcde-script-extender-adapter/build.js` | Adapter build entry | Packages the independently patched DLL and permitted upstream files |
| `mods/shcde-script-extender-adapter/THIRD_PARTY_NOTICES.txt` | Modification notice | Binary modification, attribution, license and policy disclosure |

Original DLL SHA-256:

`F671634B7A3C0480EFF8CD757D871431DAF3E5B445A67BEC5B502A01C6C79EB1`

Adapted 2.6.0 DLL SHA-256:

`2697274941ED9184913CDD2479CD4292A999042DAD0EFBDAE71B0DC04DF9DBD2`

The 0.2.4 update mode validates assembly identity/version and supported target-method structure before patching a future official release. Signed assemblies require separate review. Structural acceptance is **not proof of gameplay or multiplayer compatibility**.

### 3.2 Runtime bridge in MMC (0.2.0 onward)

`mods/scde-multiplayer-compatibility/src/ScriptExtenderBridge.cs` currently has **179 lines**:

- Line 23: `InstallManagedPolicy` applies a Harmony prefix returning false from `TryUpdateModsFromRemote`. This suppresses automatic Workshop installation, updating, unsubscribe cleanup and updater-triggered restart, not loading of existing scripts/assets.
- Line 63: `Read` reflects `GameAssetModManager.Instance.GetRegisteredAssetDirectories()` and merges registered metadata with actually loaded `Chainloader.PluginInfos`. Registered assets take precedence over bare plugin entries.
- Line 135: `RuntimeProfile.Merge` publishes required runtime identities under `se:` in MMC's v3 profile. Infrastructure GUIDs cannot be declared client-only.
- SE's `_SE_*` lobby fields, filters, client-side join checks, native network paths and AntiTamper logic remain intact. MMC uses separate `scdemm_*_v3` fields, host-side member enforcement and a start gate. Neither mechanism overrides the other's rejection.
- Manager-declared packages remain strict ID/version requirements. Client-only SE assets may be optional in the runtime layer but remain strict if also explicitly declared as manager packages.
- Missing required SE registration/policy, duplicate identities, invalid/oversized profiles and incorrect digests fail closed. Failed in-room refresh withdraws previous verification tokens rather than trusting stale success.
- Network profile v3 is distinct from the existing schema-2 disk deployment receipt.

Both peers need compatible MMC/runtime profiles. Vanilla, SE-only and older-MMC clients are not automatically compatible. Matching IDs/versions is not anti-cheat or proof of identical configuration.

### 3.3 Read-only Workshop metadata optimization (0.2.3 / MMC 0.3.1)

- `ScriptExtenderBridge.cs:38` installs a fast path only for SE assembly **2.6.0.0**; line 56 skips the original map-processing hook only for confirmed BepInEx plugin maps.
- `SeWorkshopMetadata.cs`: **83 lines**; initialization at 22, `IsPluginMap` at 47. It reads ZIP directory metadata and root `info.json` with a 256 KiB bound, using SE's ZIP/JSON dependencies, and closes handles.
- The avoided upstream path constructs an editable `MapArchive` merely to classify a plugin map, unpacking/recompressing the archive. Ordinary/resource maps and malformed/unknown formats fall back to upstream handling.
- This is a runtime prefix, not another SE DLL disk modification. Other assembly versions do not receive the version-specific optimization.

### 3.4 Startup-ready reporting (0.2.3)

`StartupReadyReporter.cs`: **34 lines**; `Tick` at 15. MMC's existing 0.75-second tick reads the existing main-menu/sprite readiness and emits one launch-ID-specific signal. It does not create game UI or change game state.

`src/core/launch-progress.js`: **115 lines**; `watch` at 55, `poll` at 72. Every 500 ms it reads at most 64 KiB of new log data, retains at most 512 entries plus an 8 KiB incomplete line, and stops on readiness, exit, manual close or timeout. The bilingual non-modal launch window shows observed plugin names and elapsed times, not a fabricated percentage. Closing it does not terminate the game or unlock Mod editing.

No Serps field-name rewrite or speculative SE downgrade was shipped. The earlier `GameUnit.N000000F4` incompatibility remains the third-party author's responsibility under the owner's explicit instruction.

## 4. Why unrestricted upstream updates may break compatibility

Replacing the adapted DLL with an untouched upstream DLL removes its early guard. Loading order can again let SE update before MMC. A future SE release may also change private methods, public registry APIs, dependencies or introduce another deployment path. These are concrete risks, not a claim that every future update will break.

The inspected SE 2.6.0 updater differs from the manager's ownership model:

1. SE updates staged files while the manager repository still contains an older imported package/version. A later manager deployment can overwrite the newer files with the older copy.
2. SE kills the game before a separate updater finishes writing. Without coordination, the manager can release its running-game lock too early.
3. Upstream `deps/data/mod-updater.ps1:85` restarts through `steam://run/3024040`, not a guaranteed launch of the staged executable. Passing a `GameExe` argument does not alter that final command.
4. Automatic installation and unsubscribe cleanup need agreement about file ownership, version records and enabled/disabled choices.

A proposed handoff would let SE decide/stage updates, synchronize the manager's saved packages/profile, retain the lock through deployment, and restart the same staged game. It would **not** add manual confirmation for each SE Mod update. "Safe deployment" means correct targets and non-conflicting operations, not malware screening. **This handoff is not implemented; the owner will first discuss it with the SE author.**

### Unmodified upstream reference points

Paths below are relative to `mods/shcde-script-extender-v2.6.0/src/SHCDESE.BepInEx/` unless stated otherwise. Line numbers refer to the supplied 2.6.0 snapshot.

| Upstream file | Line | Use |
|---|---:|---|
| `API/Components/Archive/MapModManager.cs` | 124 | Guard/prefix target |
| Same file | 229, 340 | Native unsubscribe detection and update staging |
| Same file | 494, 533 | Updater handoff and Windows exit |
| `API/Components/ModManager/GameAssetModManager.cs` | 80 | Public asset registry |
| `API/GameNetworkAPI.cs` | 246, 270, 379-398 | Join check, runtime list, client-only rules; preserved |
| `IO/DirectoryHelpers.cs` | 78, 130 | Derives paths from the running game |
| `Bootstrap/Plugin.cs` | 284 | First-start author prompt; preserved |
| Upstream `deps/data/mod-updater.ps1` | 30-46, 76, 85 | Deletion list, staged copy and Steam URI restart |

Required registry/update methods were also inspected in the supplied binary, not only source. Upstream project: <https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender>.

## 5. Later manager-side changes previously missing from this document

### Workshop import and notifications (0.2.2)

- `src/core/se-package.js`: **162 lines**; `readSEPackageMetadata` at 122, `stageSEPackage` at 132. Supports appended archives in SE `.map` and `.semod`; scans metadata without decompressing the full map. Ordinary playable maps are not listed as code Mods.
- `src/core/mod-package.js`, `src/core/workshop.js`, `src/import-window.js`: temporary installation/rollback and multi-select import. New IDs default enabled; re-import/update preserves enabled/disabled state.
- Plugin directory layout is retained. Resource/Lua packages stay packed; embedded DLLs are not promoted to loader plugins. Core/loader replacement paths are rejected.
- `src/core/deployer.js` keeps existing player configuration; defaults are copied only when absent. `src/core/manager.js` enforces declared minimum/maximum SE versions.
- `src/core/release-updates.js`: **115 lines**. Uses explicit HTTPS GitHub/GitLab `VersionCheckUrl`; does not guess from an unrelated website. The inspected Serps 1.0.12 did not specify this field, so its local Workshop package is the available version-comparison source.
- Checks start about 1.5 seconds after the initialized UI paints. Network work does not hold the launch lock. Transient errors retry up to 10 times with timeout; invalid metadata/404 are not retried ten times. Bilingual notifications are non-modal and do not focus/raise the manager over a game.

### SE core automatic installation (0.2.4, retained)

- `src/core/se-core-package.js`: **118 lines**; download at 18, extraction at 56, preparation at 84. Only the official GitLab project **74440776** `SHCDESE.zip` runtime asset is accepted; not SDK/debug assets, different projects/domains or redirects.
- Limits: 128 MiB download; 10,000 extracted entries; 512 MiB unpacked. Unsafe/duplicate paths and symlinks are rejected. Only `BepInEx/plugins` and supplied `msvcp140.dll` are selected; not upstream core/loader/configuration/launch scripts.
- `EarlyManagedGuard.cs:17-47` adds update-mode validation; 55-59 still inserts the same five-instruction guard into an independent copy.
- `src/core/se-core-updater.js`: **164 lines**. Uses prepared versions, backups and a swap journal under `manager-data/se-updates/`. Download does not lock game launch; installation waits for game exit and idle state. Interrupted swaps are recovered on reopen.
- Accepted installed SE versions survive reopen instead of being downgraded to the bundled version. The SE card can roll back; the rejected upstream version is skipped on later checks.
- `tools/build-se-update-helper.js` bundles the .NET Framework helper/Cecil. Players need no development SDK or .NET 8 runtime.
- Manager game-session persistence records the game PID and checks executable identity on reopen; uncertain live identity retains the lock conservatively.
- `src/import-window.js`, `src/preload.js`, `src/renderer/app.js`: non-modal download/wait/update/failure/rollback status and SE-card rollback.
- Background work does not rewrite an actively running game copy. The accepted version is deployed at the next normal launch.

### Fixed Manager filename and opt-in self-update (0.2.5)

- Release output is **`SCDE-Mod-Manager-Portable.exe`**. Window title and embedded version retain **0.2.5**. No separate version TXT file is needed.
- `src/core/manager-updates.js` checks only `<SteamLibrary>/steamapps/workshop/content/3024040/3796682182/SCDE-Mod-Manager-Portable.exe`. Steam library discovery is reused; other item directories are not enumerated for Manager EXEs.
- After the main UI paints, checks read version resources without running the candidate or hashing the entire EXE. Missing/incomplete downloads cannot block game launch.
- The notice offers **Not Now** and **Update and Restart Manager**. Replacement requires the game to be closed and other Mod operations idle; it does not force-close a game. Development/non-portable launches cannot self-replace.
- Only after confirmation are files copied and hashed. A candidate changed since notification is refused. The target is the outer `PORTABLE_EXECUTABLE_FILE`, not Electron's extracted temporary EXE.
- `src/core/manager-update.ps1` waits for manager exit, requires a commit marker, checks the target is unchanged, replaces with a same-volume backup, then launches the same path. Its hidden process uses standard Windows PowerShell modules; no administrator rights are requested.
- Sibling `.scdemm-update-*` directories retain `previous.exe` and result/error logs. This backs up the EXE, not all user data. Mod/configuration repositories are not deleted or migrated.
- The mutation lock remains held through shutdown; pending SE work does not start in that gap. SE update-policy code and the SE DLL are unchanged in 0.2.5.
- Version/product text and SHA-256 prove expected metadata and copy consistency, **not publisher authentication**. The EXE is unsigned. There is no Manager network-download service or automatic Workshop upload.
- The real subscribed item inspected on 2026-09-18 still held `SCDE-Mod-Manager-0.1.16-Portable.exe`; it was not modified. Future uploads must place the fixed name at the same item's root. Old Managers without self-update need an initial manual replacement or new published file.

## 6. Shared runtime, data ownership and performance

SE 2.6.0 references BepInEx 5.4.21.0. All 69 inspected BepInEx/MonoMod member references resolved against the shared 5.4.23.5 runtime, followed by successful real Unity startup. Merely having a newer version was not treated as proof.

Only the Runtime package owns `BepInEx/core`, Doorstop and `winhttp.dll`. The adapter contains SE plugins, the supplied native dependency, license and attribution, not another loader. Upstream vanilla-launch scripts targeting Steam's original game are excluded.

The three required components appear as compact cards outside user Mod sorting. Configuration is not redeployable default payload. Staged games still use the game's normal user settings/saves: file isolation is **not an OS sandbox**. Third-party code runs with the user's permissions.

MMC reads runtime identities at lobby boundaries on the existing 0.75-second cadence, not by per-frame DLL hashing. The launch observer stops after readiness/close. This does not establish zero overhead, leak freedom, configuration equality or anti-cheat.

## 7. Backups and recovery

| Before change | Snapshot |
|---|---|
| Initial integration | `backups/before-script-extender-20260916-224831/`: 55 files, Manager 0.1.19 and MMC 0.2.6 sources |
| SE import / early guard | `backups/before-se-import-20260917/` |
| Startup performance / progress | `backups/before-se-performance-20260917/`, `backups/before-launch-progress-20260917-094502/` |
| SE core auto-update | `backups/before-se-auto-update-20260917-213849/` |
| Self-update / English handoff | `backups/before-manager-self-update-20260918/`: source, tests, build configuration, previous handoff and portable checker |

Close the manager/game before source rollback. Archive current work, restore the selected snapshot by relative path, and archive integration files absent from an older snapshot. Do not delete player Mods, saves or settings. These are source backups, not player-data snapshots. Preserve configuration before recreating a staged game for an older manager.

SE update recovery uses `manager-data/se-updates/state.json`, `backup-<UUID>` and `ready-<UUID>`. Manager EXE backups live beside the portable EXE. Their disk usage is intentional, not a memory leak.

## 8. Verification evidence and outstanding tests

Historical evidence is labeled by release. None substitutes for unperformed two-player tests.

| Check | Recorded result | Boundary |
|---|---|---|
| 0.2.0 API audit | 69 / 69 references resolved | Static evidence |
| 0.2.0 runtime profile | 18 assertions passed, including reversible Harmony prefix | Protocol/classification tests |
| 0.2.0 isolated deployment | 263 payload paths, 0 conflicts, configuration kept | That release still used an unmodified SE DLL |
| 0.2.0 Unity startup | SE/MMC/UU-ImGUI/runtime loaded; bridge probe passed | Startup, not multiplayer gameplay |
| 0.2.2 real Serps import/startup | Scanned/imported original package; plugins loaded; Chainloader completed | Not every Serps feature |
| 0.2.3 metadata fast path | Unity probe about 15.7 ms and natural skip marker observed | Not a controlled whole-startup A/B comparison |
| 0.2.3 real startup | Main menu ready at about 60.8 seconds | No universal acceleration of arbitrary plugin initialization |
| 0.2.4 official package preparation | Downloaded official 2.6.0; adapted DLL hash matched bundled build | 2.7.0 fixtures were hypothetical, not an official release |
| 0.2.4 Node suite | 78 passed / 1 existing Fog failure | Expected radius 100 versus configured 85; not changed here |
| 0.2.4 Electron / portable | Bilingual SE status, rollback and visible final window passed | No real updated-gameplay or two-player claim |
| 0.2.5 updater core | Fixed-item scope, changed-file refusal, opt-in preparation, shutdown lock | Fixture/core checks |
| 0.2.5 Windows helper | Backup/replace/relaunch; no-commit and changed-target refusal; Unicode/space/quote paths | Isolated compiled test EXEs |
| 0.2.5 Electron update UI | Delayed check, dismiss, explicit accept, language and running-game/mutation locks | Isolated UI |

Evidence locations:

- `release/se-startup-smoke-GWljsn/core-startup.log`, `bridge-startup.log` (initial bridge markers at 618, 621-623), `first-start-prompt.log`.
- `release/se-startup-smoke-EvaI1F/serps-startup.log`; `release/se-startup-smoke-cvb1a6/before-early-guard.log`.
- `release/se-performance-audit-20260917/startup-0.2.3-*`; `release/manager-launch-profile-teAITu/result.json` records preparation, not complete game startup.
- `release/se-real-update-check-h6n75H/`, `release/se-real-update-check.json`, `release/se-update-ui-a5LWJ6/`, `release/portable-0.2.4-check-bYbVFD/verification.txt`.
- `release/manager-self-update-kBHLm3/`; final 0.2.5 evidence is recorded in `docs/MANAGER_0.2.5_SELF_UPDATE.md`.

Real-game test processes were deliberately terminated, not game crashes or proof of a normal menu exit. Player settings hashes were unchanged. Optional UU-ImGUI export and MMC logo warnings were not described as warning-free. A final eight-line 0.2.0 token-withdrawal refinement had build/protocol checks but no actual two-player trigger. An additional old loading-window screenshot attempt in 0.2.4 returned `UnknownVizError`; it was not counted as a pass, although final main-window and SE-update captures succeeded.

Before claiming complete compatibility, test matching peers; missing/extra/wrong-version Networked SE Mods; optional client-only assets; SE-only/older/vanilla peers; timeout; host migration; return to single player; start enforcement; representative gameplay and long-run memory/frame times. Unknown future SE versions and all third-party combinations remain unverified.

## 9. Historical 0.2.0 source-diff accounting

These counts compare the original integration to its pre-change backup. They include comments/blank lines, not only logic statements. They are **not cumulative 0.2.5 counts**. Current SE-related file sizes and entry lines are listed above.

| File | Added / removed | Original purpose |
|---|---:|---|
| `src/main.js` | +7 / -2 | Required packages |
| `src/core/manager.js` | +2 / -2 | Required-component protection |
| `src/renderer/index.html` | +1 / -0 | Card container |
| `src/renderer/app.js` | +14 / -2 | Cards separate from user sorting |
| `src/renderer/styles.css` | +6 / -0 | Compact wrapping layout |
| `package.json` | +7 / -3 | Version/resources |
| `package-lock.json` | +2 / -2 | Root version |
| `mods/scde-multiplayer-compatibility/src/SCDEMultiplayerCompatibilityPlugin.cs` | +83 / -31 | v3 profile/refresh/enforcement |
| `mods/scde-multiplayer-compatibility/src/ScriptExtenderBridge.cs` | +152 / -0 | Original bridge |
| `mods/scde-multiplayer-compatibility/info.json` | +10 / -0 | SE metadata |
| `mods/scde-multiplayer-compatibility/manifest.json` | +2 / -2 | Version |
| `mods/scde-multiplayer-compatibility/build.ps1` | +9 / -1 | Build/tests |
| `mods/scde-multiplayer-compatibility/README.md` | +11 / -2 | Policy |
| `mods/scde-multiplayer-compatibility/test/RuntimeProfileTests.cs` | +93 / -0 | 18 assertions |
| `mods/scde-multiplayer-compatibility/test/ScriptExtenderStartupProbe.cs` | +31 / -0 | Test-only probe, not shipped |
| `mods/shcde-script-extender-adapter/build.js` | +26 / -0 | Original packaging |
| `mods/shcde-script-extender-adapter/manifest.json` | +8 / -0 | System package |
| `mods/shcde-script-extender-adapter/THIRD_PARTY_NOTICES.txt` | +17 / -0 | Notices |
| `mods/shcde-script-extender-adapter/GPL-3.0.txt` | +674 / -0 | License, not logic |
| `tools/verify-script-extender-integration.js` | +58 / -0 | Deployment checks |
| `tools/smoke-script-extender-startup.js` | +45 / -0 | Isolated game startup |
| `tools/verify-manager-launch-ui.js` | +9 / -4 | UI regression |
| `tools/verify-multiplayer-compatibility.js` | +3 / -3 | Package regression |
| `test/core.test.js` | +4 / -2 | Version/handshake contracts |

Generated artifacts, screenshots, logs, extracted copies and the original new handoff document were excluded from these source counts.

## 10. Historical artifact identities

| Artifact | SHA-256 |
|---|---|
| Manager 0.2.0 Portable, 145,796,090 bytes | `737BAC17761F59FB5213C3272F6D25F12F8CD11A159FB279D82EFF7415CBD4DD` |
| Runtime 5.4.23.5 | `76A8F848776E758924C884429D11D94C9334464B7068345F7153550BC72A341F` |
| Original SE 2.6.0 adapter package | `447E72FDCF4F252D8789A335FBD9FB309DDCB4FFA87C87BD948D705CE3758BE4` |
| MMC 0.3.0 | `74A55A0F460F7748283F08B3C65CBAEC081C2C66A6214F3D351516929BCF8747` |
| Adapted SE 2.6.0+scdemm.1 | `BDF30AE5B06D231B6C526F97B91518415FEEE83C184FD8882B417ACB44DA3ED5` |
| Manager 0.2.2 Portable | `A0917F5E5624E55F401DC77B7C03D58E5646B566D566CB9E929F16C333AE1D37` |
| Manager 0.2.3 Portable | `1EEE5598EDF2E3FB484337B545E8C754428121BA0ABF4324961982E795C44BD9` |
| Manager 0.2.4 Portable, 145,953,718 bytes | `4BD25F27F08227BC66B95B93BFDEC9E9532F72BB7E853ECEDE066CA73AAAA382` |

Portable releases were not Authenticode-signed. License/attribution inclusion is not a full audit of all bundled dependency licenses. Test probes are not release payloads. No Workshop publication, player-install replacement or Steam-original modification is implied by a successful local build.

## 11. Manager 0.2.6: reapply and incompatible-component launch protection

### Recovery and policy

Rollback previously retained the rejected SE directory and a skipped-version marker but exposed no way to reuse it. The same compact card button now offers **Reapply v...** after rollback. Reapply uses the retained local copy, clears the skipped version only after successful commit, retains the current version as the next rollback backup, and shares the existing transaction and running-game locks. Automatic checks still respect a rollback until the owner explicitly reapplies or a different official version becomes available. No backup is silently installed merely by showing the button.

The owner's final policy is **warn and block game launch**, superseding the earlier warning-with-confirmation choice. The warning lists the containing pack, incompatible component names and supported SE ranges. A launch callback cannot override this block. A cancelled/blocked launch closes its progress window and releases its operation lock. A manual rollback involving nested component requirements can still be explicitly accepted, but the resulting incompatible combination cannot launch; the user must reapply a suitable SE version or disable the pack in the Manager. Direct imported-Mod SE requirements continue to be enforced by dependency resolution before an SE directory swap.

Startup tolerates a recorded SE-version mismatch without silently disabling Mods or deploying that incompatible profile, so the recovery controls remain accessible. Actual launch still checks compatibility. Unrelated initialization or dependency errors are not suppressed.

### Metadata compatibility

- SE 2.7 metadata can express the core requirement through `Dependencies` entries with GUID `000shcdese` and `MinimumVersion` / `MaximumVersion`. These bounds are combined with older top-level `MinimumScriptExtenderVersion` / `MaximumScriptExtenderVersion` using their intersection. Invalid/inverted bounds are rejected.
- Older installed imports are refreshed in memory from their installed `info.json` or resource-package metadata. Import identity/version must agree. Existing package files and player manifests are not rewritten.
- The explicit launch/version-switch check reads installed, enabled SE pack metadata only, including nested `info.json` files. It does not hash or load plugin DLLs and does not scan the Workshop or perform network access. Files are bounded to 256 KiB, traversal to 10,000 entries per pack, and links/junctions are skipped.
- Internal third-party enable switches are not interpreted. All declared incompatible components inside an enabled pack block launch, even if a pack-specific UI may have disabled one. Disabling the entire pack in the Manager excludes it from this check.
- This is declared SE-version validation, not arbitrary DLL/API compatibility verification or a complete dependency solver for all SE Mods. Nested component metadata hidden inside opaque resource archives is not recursively unpacked by this check.

### Changed Manager entry points (0.2.6 source line references)

| File | Entry line | Change |
|---|---:|---|
| `src/core/se-core-updater.js` | 61, 110, 118, 165 | Reapply status, transaction commit, manual-switch check, explicit local restore |
| `src/core/se-package.js` | 24, 151 | Both dependency formats and read-only legacy import refresh |
| `src/core/mod-package.js` | 71, 99 | Metadata format marker and installed metadata refresh |
| `src/core/se-compatibility.js` | 8 | Enabled-pack and nested-component SE range checks |
| `src/core/manager.js` | 562, 633, 641 | Recoverable initialization, hard launch gate before deployment/spawn |
| `src/main.js` | 159, 194 | Bilingual component warning; recovery-aware startup |
| `src/import-window.js` | 34 | Main-window-only reapply IPC |
| `src/preload.js` | 11 | Reapply bridge |
| `src/renderer/app.js` | 355 | Same-location rollback/reapply control |
| `src/launch-window.js` | 55 | Close progress monitoring on blocked launch |

No SE author source, EarlyManagedGuard policy, MMC, Fog, Advanced Control or Serps gameplay implementation was changed for 0.2.6. The pre-change source and 0.2.5 executable are retained in `backups/before-se-restore-20260919/`.

### Official SE 2.7.2 evidence

Official tag `v2.7.2`, commit `4575b49695a43d859cc0491549d35b2fd677211c`, was inspected using the GitLab compare/file APIs and the official runtime package. Evidence is under `release/se-2.7.2-audit/`. The full source archive download was unavailable; this is a targeted source/assembly inspection, not an exhaustive source review.

- The reflection targets used by MMC remain present: `MapModManager.TryUpdateModsFromRemote`, `GameAssetModManager.Instance`, `GetRegisteredAssetDirectories`, and ModInfo GUID/Name/Version/NetworkMode properties.
- Existing early-guard preparation passed on the actual official 2.7.2 assembly. The original target method body was preserved after five prepended IL instructions. Full normal update preparation passed separately in `release/se-real-update-check-q08jM3/`; it did not replace BepInEx core or configs and left the supplied upstream distribution unchanged.
- The 2.7.2 upstream MapArchive implementation no longer unpacks/recompresses every ZIP entry during metadata opening. MMC's old fast path is explicitly limited to SE 2.6.0.0; it remains off for 2.7.2, allowing upstream handling.
- New metadata dependency fields required the Manager parsing changes above. Other native/API changes can still affect third-party Mods. These structural/package checks are **not a new live-game or two-player compatibility test**.

| Audit artifact | SHA-256 |
|---|---|
| Official 2.7.2 runtime ZIP | `CD4DCD743BC5EFA982D186FF099F19A195F25DD74FCA3C9C6D9BDC242B2062F1` |
| Untouched 2.7.2 SHCDESE DLL | `D7F13D1A6688DBE481A491AF3E29E7F5E6E152C9BC8AAAF8AB6E0F84BB4DB412` |
| Guarded 2.7.2 SHCDESE DLL | `D49C5391A09A45C53B47BEE80F10E610D169C6A77500F6436AD33B9C4CA396E9` |

Detailed tests, remaining multiplayer uncertainty and final artifact identity are recorded in `docs/MANAGER_0.2.6_SE_RECOVERY.md`.

## 12. Manager 0.2.7: localization and official SE 2.8.0 audit

### Update state and restart clarification

The owner clarified that an earlier failed launch followed an update click while the SE card still displayed 2.6.0. The displayed error was the incompatible-component launch gate, not an observed game crash. That report establishes that the displayed installed version did not meet enabled Mod requirements; it does not establish whether the earlier update was still downloading, deferred, failed, or affected by a stale UI. No retained error record proves that historical cause.

No Manager auto-restart was added. Successful SE updates change the Manager's installed repository; the next launch deploys that version into the game copy. The Manager does not keep the SE assembly loaded as part of its own JavaScript process. The UI now explicitly distinguishes download/preparation, staged-but-not-applied, and completed states. Completion states that the next game launch uses the new version without restarting the Manager. Existing hard launch blocks, game-running locks, rollback/reapply and native-SE automatic-Mod-update policy are unchanged.

`test/se-core-updater.test.js:115` exercises an update to 2.8.0 and a subsequent launch through the **same ModManager instance**. Its game process and DLL bytes are fixtures; it proves Manager state/deployment behavior, not third-party runtime compatibility. Independently, existing player history records two real 2.8.0 launches from Manager PID 9088 at `2026-09-20T01:28:25.463Z` and `2026-09-20T01:34:41.435Z`, both ending with exit code 0. Those are pre-existing player sessions, not newly conducted gameplay tests for this release.

### The integration patch is still present

Official release `v2.8.0`, commit `5b4d48e732e9b6e2e93c135f0b28ce5b9d8bcd33`, was retrieved from the upstream GitLab API. The release notes describe DDS texture support for atlas overrides. Targeted comparison against v2.7.2 found changes mainly in asset/atlas/sprite interfaces, texture extensions, tooling and build files; the inspected `MapModManager`, `GameAssetModManager`, `ModInfo` and `ModDependency` source files were unchanged in that comparison.

The existing update preparation ran against the official 2.8.0 runtime package in an isolated directory. It applied and verified the same five-instruction entry guard in `MapModManager.TryUpdateModsFromRemote`. The newly prepared guarded DLL hash matched both the current installed Manager repository DLL and the game's staged DLL. An independent IL dump of the installed DLL also showed the environment-variable check, branch to the original method, and early return. Updating SE did **not** remove the guard. No new upstream source edit or special 2.8.0 patch was necessary.

The retained guard prevents native Workshop install/update/unsubscribe/restart in managed mode. MMC additionally installs its existing runtime policy prefix and reads the SE registry. Its separate Workshop metadata performance workaround explicitly runs only with assembly version 2.6.0.0 and remains disabled for 2.8.0, allowing the newer upstream implementation to handle that path. The proposed SE-to-Manager update/restart handoff remains unimplemented pending the owner's discussion with the SE author.

| Artifact | SHA-256 |
|---|---|
| Official 2.8.0 runtime ZIP | `89989D439A6B295386A5DE7EFE5E3ED2DC5EDE01E60B84408422746303B57BBF` |
| Untouched 2.8.0 SHCDESE DLL | `F671634B7A3C0480EFF8CD757D871431DAF3E5B445A67BEC5B502A01C6C79EB1` |
| Guarded 2.8.0 DLL: isolated preparation, installed repository and game copy | `B6969D931FD29DB75ACDF4B5DEF7B063E1C534D47FCE6D82A558803C780127D1` |

Evidence: `release/se-2.8.0-audit/` contains official release/compare responses, the four targeted source files, package preparation results, installed IL output, installed update notices and a copy of the existing player session log. `tools/audit-se-release.js` is a read-only upstream audit collector; it does not install or execute downloaded source.

### Evidence limits and graded risks

| Level | Finding / risk | Boundary |
|---|---|---|
| Low for the inspected paths | Official 2.8.0 accepts the existing guard; inspected bridge entry points remain available. | Actual package preparation and matching deployed bytes, not a universal API guarantee. |
| Medium, observed diagnostic issue | SE reports absent `info.json` for some ordinary BepInEx plugins, including Fog and Advanced Control. | They subsequently load through BepInEx in the same log; the warning alone does not prove a loading failure. MMC also reads Chainloader plugin metadata. |
| Medium, observed feature failure | Serps Bugfixes and QoL reports an exception for `Lord control groups` while initializing `CrusaderDE.MainViewModel` (`DisableLordControlGroupNativePatch`, line 1470; `ApplySetting`, line 959 in its reported source). | That feature remains inactive according to the log. The log does not establish SE 2.8.0 or the Manager as the cause. No Serps fix was made. |
| High, future integration changes | SE can rename/change updater or registry APIs, network-mode meaning, dependencies, or introduce an independent update/restart path. | The current guard checks method structure, not semantic equivalence of future code. Structural failures reject preparation; semantic changes may still need a new integration review. |
| High, game/third-party compatibility | Native offsets, gameplay hooks and Mod API requirements may change independently. | Declared-version checks do not prove every enabled feature works. No new two-player handshake, Fog vision, control ownership or mixed-version match was tested. |

The copied session log records SE 2.8.0, MMC 0.3.1, Fog 0.2.51 and Advanced Control **0.2.20**, followed by the startup-ready marker and managed game initialization. It is not evidence for the older reported 0.2.19 multiplayer incident. Normal close/exit lines must not be presented as a crash. The current tested release can reuse the integration preparation; this does **not** promise compatibility with every future release.

### Language packs and changed source entry points

Persistent packs live under `manager-data/config/modmanager`. The Manager seeds `en.json` and `zh-CN.json` without overwriting existing edits. On first selection it matches the system locale, then progressively less specific tags; unsupported Chinese locales fall back to Simplified Chinese, other unmatched locales to English. A saved valid selection is preserved. Missing/invalid community translations use bundled text without preventing launch. Strings are cached per Manager process, not polled during gameplay; placeholders are validated and output is text rather than HTML/code.

| Source | Entry | Change |
|---|---:|---|
| `src/core/localization.js` | 8, 28, 55, 65 | Language-tag validation, file initialization, selection and bounded loading/fallback |
| `src/core/manager.js` | 220, 232, 256 | Persist arbitrary valid language tags and return one merged bundle |
| `src/i18n.js` | 1 | Shared literal placeholder substitution |
| `src/locales/en.json`, `src/locales/zh-CN.json` | 1 | External templates, including clearer SE update states |
| `src/renderer/app.js`, `src/renderer/import.js`, `src/renderer/launch.js` | 1 | Consume bundles instead of embedded bilingual dictionaries |
| `src/main.js`, `src/import-window.js`, `src/launch-window.js` | Localization call sites | Translate native warnings/file pickers and pass bundles to child windows |
| `test/localization.test.js` | 19 | Selection, persistence, fallback, placeholder and template parity checks |
| `test/se-core-updater.test.js` | 115 | Same-instance SE update-to-launch regression |

All changes in this section are Manager/UI/test/documentation changes. No upstream SE code, adapter behavior, MMC, Serps, Fog or Advanced Control implementation was changed. The source backup is `backups/before-manager-localization-20260919/`. See `docs/MANAGER_LANGUAGE_PACKS.md` for translators and `docs/MANAGER_0.2.7_LOCALIZATION_SE_AUDIT.md` for release verification.

## 13. Manager 0.2.8: optional SE and game-copy language files

The owner requested removal of the SE rollback/reapply buttons, continued automatic official-core updates, and a compact on/off switch beside the SE name. This request does not establish universal future-SE compatibility: existing structural preparation checks, transaction recovery, game-running locks and component version gates remain. The native-SE Mod automatic-deployment/restart guard is unchanged.

### Optional runtime policy

- SE remains an installed, pinned system component that cannot be removed or reordered, but its activation is now optional and stored as `seEnabled: false` after explicit disabling. The default for existing/new installations remains enabled.
- Disabling SE also disables enabled Mods whose declared dependencies reach SE, including indirect dependencies and imported SE packages. The Manager lists their names. Ordinary independent Mods, BepInEx and MMC remain enabled. The next deployment/profile excludes disabled components.
- Explicitly enabling a dependent Mod while SE is disabled asks for confirmation to enable both. Cancel leaves configuration unchanged. An SE-dependent new import cannot silently override the disabled choice; it is imported but activation fails with an enable-SE message until the user explicitly enables it.
- Reopening the Manager, enforcing system packages and installing an SE core update do not reactivate an explicitly disabled SE. Updating its installed bytes is distinct from enabling/deploying it.
- Version rollback/reapply IPC and preload/UI controls were removed. Internal transaction/recovery helpers and backups remain; legacy skipped-version markers no longer suppress newer automatic updates when no rollback button is available. Failed download/preparation still keeps the current installation, and a running game still delays replacement.
- MMC already declares SE as a BepInEx soft dependency, returns from policy installation when SE is absent, and requires its registry only if SE is present in the Manager's active profile. No MMC DLL/source or SE DLL/source change was needed for this release. This source/package logic is not a fresh SE-disabled gameplay or multiplayer test.

### Localization and configuration ownership

The Manager now reads editable text from the game copy's `BepInEx/config/modmanager/lang/*.json`. It seeds English/Chinese and migrates missing old Manager-data translations without overwriting player edits. No original Steam files are written. Until a valid game directory is selected, the old Manager-data folder remains a startup fallback. The main button now opens **config**, not the game-copy root.

The reserved `modmanager` folder leaves space for other settings. Other authors may use `BepInEx/config/<installed-package-id>/lang/*.json`; the Manager exposes a bounded, read-only per-installed-Mod dictionary interface and does not inject translation code into plugins. Existing first-run/saved-language behavior is retained. See `MANAGER_LANGUAGE_PACKS.md` for the precise contract and the difference between the Manager-side interface and author-owned game-side C# readers.

Manager-controlled stage rebuilding backs up the existing config tree outside the stage before rebuilding and restores it; failures retain the backup and report its path. Mod defaults under `BepInEx/config/<id>/lang` are seeded only if missing and are not treated as ordinary removable deployment files. Existing non-language payload/config policies otherwise remain unchanged.

### Source anchors and verification boundary

| File / entry | Responsibility |
|---|---|
| `src/core/manager.js`: `dependsOnSE`, `setEnabled`, `ensureSystemMods`, `launch` | Dependency cascade, confirmation, persistent disabled state and active-only version checks |
| `src/core/se-core-updater.js`: `update`, `swap` | Automatic update after legacy rollback; disabled-state-aware dependency resolution |
| `src/main.js`: `confirmEnableSE`, `folder:open`, `localization:mod` | Localized confirmation, config-folder target and fixed Mod language IPC |
| `src/core/localization.js`: `Localizations`, `createModLocalizations` | Game-copy/legacy loading, bounded templates, author fallback and safe paths |
| `src/core/deployer.js`: `prepareStage`, `applyMods` | Config backup during rebuild and player-owned language defaults |
| `src/renderer/app.js`, `styles.css`, `index.html` | Same-line SE toggle, no version-switch buttons and config-folder label |
| `test/se-toggle.test.js`, `test/localization.test.js`, `test/startup.test.js` | Cascade/update persistence, migration/rebuild, per-Mod interface and native-dialog/folder regressions |

Source backup: `backups/before-manager-0.2.8/`. No automatic update handoff/restart implementation, Serps compatibility patch, existing Mod localization implementation or Workshop upload is part of this release. Actual release artifacts and test results are recorded in `MANAGER_0.2.8_CONFIG_LANG_SE_TOGGLE.md`.

## 14. GitHub issue #1: runtime identity and bounded compatibility checks

Status: local Manager **0.2.9** / MMC **0.4.0** candidate under validation on 2026-09-22; not published by this work and no live-game result is implied. Pre-change files are retained under `backups/issue-1-20260922/`. SE 2.8.0 candidate evidence is a read-only check of a cached prepared copy, not a fresh official download or new Unity launch; the bounded fresh-download attempt was unavailable.

### One runtime identity across installation methods

MMC's new **v4** fingerprint compares the actual BepInEx loader assembly version, instantiated plugin GUID/version pairs, and networked SE resource-only asset GUID/version pairs. Numeric versions and GUID casing are normalized. Manager package wrappers are retained locally as deployment provenance and no longer create extra required network identities. Matching loaded components can therefore match between Manager and standalone MMC installations.

Loaded plugins own their identities: conflicting SE metadata fails verification rather than replacing a plugin's version or changing it to client-only. Only resource-only client-side assets are omitted. Duplicate GUIDs, missing required registration, malformed profiles and hash mismatches fail closed. Both MMC peers must use v4. SE's own lobby fields, filters, checks and AntiTamper are preserved. This is not configuration equality or anti-cheat.

The owner explicitly chose **allowed-but-unverified SE-only interoperability** for peers without MMC metadata. It requires locally loaded SE and a host lobby marker `_SE_ = true`; joining, hosting and starting warn that extra plugins are not verified. This state is not a matching v4 profile. Present/old/malformed/incomplete MMC metadata never downgrades to this fallback, and MMC retains `scdemm_protocol = 4` when it withdraws a failed runtime token. Clients remember observed host capability across pending join and lobby entry, keyed by lobby/owner with bounded current/pending context. Disappearing metadata or temporarily missing owner data cannot downgrade that host; a changed owner or unrelated lobby context resets the evidence. Rooms without an SE marker remain blocked.

SE 2.6.0 has no per-member SE-presence marker, so the host cannot independently confirm a missing-MMC member actually loads SE or the same additional plugins. The warning explicitly discloses this limit; SE's own native checks remain in control of their checks. MMC's SE-facing `info.json` uses `NetworkMode: 0` so SE can permit its absence. MMC itself still treats its plugin GUID/version as required in both-v4 comparison. No SE check is disabled for this policy.

V4 host profiles also bind to the current Steam owner through `scdemm_host_owner_v4`. Publishing withdraws the previous token before writing the profile/owner and commits the token last. Readers require the same nonzero owner and coherent metadata before and after the read, so inherited old-host data is not treated as verification for a new host. An SE-only new owner with stale MMC metadata must create a new room; the old room stays blocked instead of silently downgrading.

Standalone MMC needs neither a Manager receipt nor a Manager launch marker. A present but unreadable/malformed receipt, or a managed launch with a missing receipt, never silently becomes standalone. Receipt reads are bounded to 48,000 characters. Only a valid schema-2 receipt **and** `SCDEModManagerLaunchId` activate MMC's runtime updater-blocking prefix. This does not redesign the earlier **on-disk** SE entry guard: that five-instruction guard still checks the launch marker and retains the previously agreed Manager-mode automatic-deployment/restart policy.

### Candidate SE validation before installation

The adapter now checks the exact public API consumed by MMC: `GameAssetModManager.Instance`, a public parameterless instance `GetRegisteredAssetDirectories()` returning `IEnumerable<KeyValuePair<ModInfo,string>>`, string `GUID`/`Version`/`Name` getters, and an enum with `Clientside = 0`. The updater target must be unique, private, non-static, non-generic `void TryUpdateModsFromRemote()` and contain exactly one call to `Platform_Workshop.GetListOfSubscribedItemsPaths`.

Candidate dependencies are resolved using the selected game's Managed directory and the authoritative shared BepInEx core. Candidate-local copies cannot shadow shared assemblies. Missing dependency identities/public-key tokens and unavailable referenced shared-runtime types/members reject preparation. The helper still adds the same five entry instructions to an independent SE copy and verifies preservation of the original opcode sequence. These checks do not establish semantic equivalence or guarantee compatibility with every future release.

### Game patch failures are explicit

MMC validates declaring types, unique target names, complete signatures and relevant IL call anchors before applying Harmony patches. The key-map transpiler additionally requires exactly three R/T/Y dictionary lookups and their expected branch shape. Whole-game `Assembly-CSharp.dll` SHA-256 is diagnostic provenance, not the sole patch gate.

Unsupported targets remove MMC's partial patches, suppress startup readiness and verified lobby tokens, log `SCDEMM_STARTUP_FAILED`, and keep a visible warning telling the player to exit/update. Managed launches also write `_scde_manager/startup-failed.txt` containing the launch ID and error. The game is not forcibly terminated. An unsupported game whose hooks could not install is **not protected multiplayer**; it must not be described as safe to continue merely because its window opened. A remote peer cannot prove why its handshake is absent: the SE-only policy may allow it as explicitly unverified, never as MMC-verified.

### Source entry points and evidence boundary

| File | Entry points / changed responsibility |
|---|---|
| `mods/shcde-script-extender-adapter/EarlyManagedGuard.cs` | `Patch`, `ValidateRegistry`, `CandidateResolver.ValidateDependencies`: candidate API/dependency/IL preflight before the unchanged five-instruction adapter |
| `mods/scde-multiplayer-compatibility/src/ScriptExtenderBridge.cs` | `Read`, `AddAsset`, `RuntimeProfile.Merge`: actual runtime authority, duplicate/version checks, v4 serialization |
| `mods/scde-multiplayer-compatibility/src/SCDEMultiplayerCompatibilityPlugin.cs` | `ReadManagerReceipt`, `ClassifyManagerProfile`, `RefreshRuntimeProfile`, `RecordInitializationFailure`: standalone/managed separation and explicit failure |
| `mods/scde-multiplayer-compatibility/src/PatchTargetGuard.cs` | `Require`, `RequireCall`, `ValidateGameTargets`, `ValidateSparseKeyMap`: signature and IL validation |
| `mods/scde-multiplayer-compatibility/src/StartupReadyReporter.cs` | `ReportFailure`: launch-ID-scoped failure notification and suppression of ready |

Current local offline MMC checks pass 83 assertions plus the sparse-key-map regression against read-only game references from `E:\Steam\steamapps\common\Stronghold Crusader Definitive Edition`. Fixtures cover same runtime/different wrapper equivalence, GUID/version mismatch, plugin/SE metadata collisions, absent/unreadable/oversized receipts, rejected patch signatures/IL, the actual stance transpiler, reversible prefix, verified/rejected/explicitly-unverified peer classification, sticky host-capability transitions and host-owner binding. These are not a fresh Unity launch, two-PC match, or proof of arbitrary third-party compatibility. No upstream SE source, Serps source or Steam subscription was edited for these changes.

## 15. Manager 0.2.10: current SE build input and startup correction

The build now selects the latest official SE instead of pinning 2.6.0. Both build
targets run `tools/prepare-bundled-se.js`; failed download/preflight stops packaging.
An explicitly supplied, freshly downloaded runtime distribution may be used after
checking its version against official release metadata. This is not a silent fallback.
`src/core/se-core-package.js:prepareExtractedSE` shares the existing validation/guard
preparation between downloaded ZIPs and that developer-supplied runtime. It introduces
no additional SE patch or change to SE's gameplay, lobby or updater policy.

The owner supplied `mods/SHCDESE/SHCDESE` (assembly 2.8.0.0, metadata 2.8.0), which passed
the current compiled helper and isolated deployment verification. The old-named
`mods/shcde-script-extender-v2.6.0` also contains 2.8.0 source, not a compiled release.
Input code and distribution remain unchanged. The generated 2.8.0 copy includes the
same existing five-instruction manager-mode updater guard. Its identity/version and
provenance are generated with the package, not hardcoded in the app.

`src/main.js:getSystemPackages` reads the bundled SE manifest. Startup now calls
`ensureSystemMods(false, true)`: it does not prepare/rebuild a game copy while opening
the manager. Normal current copies remain reusable on subsequent game launches.
See `MANAGER_0.2.10_BUNDLED_SE_STARTUP.md` for hashes, reproduction commands, test
boundaries and the old-copy isolation migration caveat. These checks are not live
multiplayer validation or a guarantee of compatibility with future SE releases.
