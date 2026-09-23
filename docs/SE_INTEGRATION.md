# Script Extender integration — Manager 0.2.10

SE belongs to Rawra / Viktor Legodzinski. `+scdemm.1` is this project's integration suffix, not a separate plugin or an official upstream version. This snapshot's prepared baseline is SE 2.8.0. New builds obtain the latest official release and stop on download/validation failure instead of silently shipping an old bundle. The updater can prepare later official releases if its structural checks succeed.

## What changes

1. **Offline guard:** `mods/shcde-script-extender-adapter/EarlyManagedGuard.cs` inserts five instructions at the start of `SHCDESE.API.Components.Archive.MapModManager.TryUpdateModsFromRemote`. When `SCDEModManagerLaunchId` is nonempty, the method returns before native automatic Mod deployment/restart. Otherwise its original implementation runs. See `early-managed-guard.patch` for the equivalent source edit.
2. **Update preparation:** `src/core/se-core-package.js` extracts only plugins and the required native runtime from the official archive. It does not install upstream BepInEx core, Doorstop configuration, player configs or launch scripts. It applies/verifies the guard, adds notices and records the upstream version/download hash before producing the managed package. An unsupported method layout, signed assembly or version mismatch stops preparation. This is structural validation, not proof of runtime compatibility.
3. **Safe replacement:** `src/core/se-core-updater.js` waits for an idle Manager/game state, retains a recovery backup and synchronizes the active package. It is not a background patch of a DLL already running in the game. Enabled SE receives automatic core updates; updating does not silently re-enable disabled SE.
4. **Runtime bridge:** `mods/scde-multiplayer-compatibility/src/ScriptExtenderBridge.cs` belongs to MMC. It also guards native automatic deployment and reads SE's registered asset Mods. MMC 0.4.0 / protocol v4 compares actual loaded runtime/plugin GUIDs and versions plus networked SE resources, rather than Manager package wrappers. SE-only peers can be admitted with explicit unverified warnings; additional plugins and individual uninstrumented members' SE presence cannot be proven. Old/malformed MMC advertisements are not silently downgraded. SE's own lobby filters/checks remain in place; neither system overrides the other's rejection.
5. **SE 2.6.0 startup fast path:** the MMC bridge avoids recompressing confirmed plugin Workshop maps when only metadata is needed. This optimization is explicitly limited to SE assembly 2.6.0.0; later versions retain upstream handling.

## Update behavior, deliberately separated

- **SE core:** the Manager downloads/prepares and automatically installs compatible-in-structure official releases when idle. Future interfaces may change; checks do not guarantee all Mods or multiplayer scenarios.
- **Imported `.scdemod` / supported SE Mod packages:** the Manager offers local Workshop updates for installed packages and requires user confirmation.
- **SE's in-game automatic Workshop Mod install/update/unsubscribe/restart:** currently blocked during managed launches. This policy has not been restored or redesigned by the GitHub upload.
- **Manager EXE:** its own Workshop-item update flow remains separate; no GitHub self-updater is added here.

## File boundary

The Manager deploys to the adjacent `- SCDE Modded` game copy and keeps its own managed packages. Steam's original game, the subscribed upstream archive and the upstream author's source are not patched in place. A new SE core is adapted before installation, not unconditionally overwritten immediately after every update.

## Source and attribution

The current local upstream core source, local guard source, equivalent patch and MMC bridge source are included. The directory name `mods/shcde-script-extender-v2.6.0` is historical, not a declaration that the bundle remains 2.6.0. Upstream source: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender . The curated source has not been independently rebuilt into the upstream release DLL. See the [full integration record](SCRIPT_EXTENDER_INTEGRATION_0.2.0.md) and `THIRD_PARTY_NOTICES.md` for validation and attribution boundaries. An adapted version number must not be represented as an official release or as a guarantee of compatibility with every future SE release.
