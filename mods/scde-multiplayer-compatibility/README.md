# Multiplayer Mod Compatibility

MMC checks the **loaded runtime**, not how players installed it. The same MMC version can run inside SCDE Mod Manager or as a standalone BepInEx plugin. It is not an anti-cheat system and does not compare configuration or authenticate authors.

## Network contract (v4)

- Compare the loaded BepInEx loader assembly version, every instantiated BepInEx plugin's GUID/version, and SE's registered networked resource-only asset GUID/version. GUID case and numeric version spelling are normalized; display names and load order are not identities.
- Manager `.scdemod` IDs/versions remain local deployment provenance; a wrapper is not counted again as a required network Mod. Matching runtime installations therefore have the same fingerprint with or without a Manager receipt.
- A loaded plugin owns its identity. Conflicting SE metadata for that GUID fails verification instead of replacing the plugin's version. Only resource-only assets marked client-side may differ; loaded plugins remain strict.
- Both MMC peers must have matching v4 runtime profiles. Malformed, oversized, duplicated, withdrawn or digest-invalid profiles do not count as verified. Old/present/incomplete MMC metadata never falls back to SE-only interoperability. The host rejects those incompatible members after the handshake grace period and cannot start while their checks are pending.
- SE's own `_SE_*` filters and join checks remain active. For two MMC peers, matching MMC profiles is necessary, not proof of game/configuration compatibility.

### Explicitly unverified SE-only peers

By the owner's choice, a peer with no MMC metadata may join an SE-marked lobby without MMC verification. The local runtime must load SE and the host must advertise `_SE_ = true`; non-SE/no-MMC rooms stay blocked. Joining, the host lobby and starting display a bilingual warning that **extra plugins are not verified**. This is a separate allowed-but-unverified state, never a matching v4 token; SE's own filters/checks still decide their part.

SE 2.6.0 does not publish per-member SE-presence metadata. An MMC host can recognize its SE-marked room but cannot independently establish whether a missing-MMC member really loads SE or the same extra plugins. The warning and logs disclose this limit. Existing MMC peers keep a protocol-capability marker when their profile fails, so they cannot silently become SE-only peers by withdrawing their token. The client also remembers observed host MMC capability across pending join and lobby entry; disappearing metadata cannot downgrade that same lobby/owner. This bounded memory resets for a changed owner or unrelated lobby context, not a temporary missing owner response.

MMC's SE-facing `info.json` declares `NetworkMode: 0` so SE itself can allow a peer without this client-side checker. MMC's own v4 profile still requires its loaded plugin GUID/version between MMC peers. No SE source or SE check is bypassed.

Host profiles bind to the current Steam lobby-owner ID. Publishing first withdraws the old token, writes the owner and profile, and commits the token last; readers recheck owner/token coherence. Inherited metadata is not valid verification after host migration. If an SE-only player inherits a room containing stale MMC metadata, create a new room rather than silently downgrading that old room.

## Standalone and managed mode

Without `_scde_manager/active-mods.lobby` and without a `SCDEModManagerLaunchId` marker, MMC builds a runtime-only profile. A present malformed receipt, or a managed launch with a missing receipt, fails closed rather than silently falling back to standalone.

Only a valid schema-2 Manager receipt **and** launch marker enable MMC's managed SE updater prefix. A valid receipt without that marker is provenance only. Standalone MMC does not suppress SE's automatic deployment/restart. In managed mode the existing policy remains: skip SE's automatic Workshop install/update/unsubscribe/restart routine, but load already installed SE Mods. This prefix is separate from the Manager's early on-disk SE adapter guard.

## Patch validation and safeguards

Before applying Harmony patches MMC checks each declaring type, unique method name, exact signature and relevant IL call anchors. The sparse key-map transpiler additionally requires exactly the three R/T/Y dictionary lookups and their expected branch shape. Unsupported structures are rejected before modification; a patch-installation failure removes MMC's partial patches. The game assembly SHA-256 is logged as provenance, not used as the sole compatibility gate.

Initialization failures suppress readiness and verified profiles, emit a launch-ID-specific failure marker, and keep a warning telling the player to exit/update. The process is not forcibly closed. An unsupported game without installed MMC hooks is not protected multiplayer; a remote peer cannot prove the cause of its absent handshake and may only classify it as unverified under the SE-only policy.

### Startup metadata optimization (0.3.1)

In manager mode with SE 2.6.0, Workshop plugin maps are identified by reading only their bounded root `info.json` with SE's existing ZIP library. A runtime prefix then preserves SE's existing rule to exclude BepInEx packages from the playable map list, without decompressing/recompressing the complete payload. Ordinary/resource maps and unreadable metadata keep SE's original handling. No SE or subscribed Mod file is rewritten by this optimization, no security setting is disabled, and no gameplay or lobby matching rules change.

The metadata reader reuses the JSON assembly already loaded by SE; it does not require the desktop-only System.Runtime.Serialization assembly. The existing low-frequency runtime tick also reports readiness once the game's sprite resources and main menu (or first-run controls screen) are ready. It only reads the existing view-model instance and writes a launch-ID-tagged signal inside the game copy. No game state is changed, and no Serps field compatibility patch is included.

### Script Extender integration (0.3.0)

The v4 contract above supersedes v3's combined Manager-wrapper/runtime fingerprint. Required SE registry failures still block verification. The runtime prefix does not rewrite upstream files; it is distinct from the Manager's documented on-disk adapter.

In manager mode, a Harmony prefix skips SE 2.6.0's automatic Workshop installation/update/unsubscription/restart routine. Already installed SE plugins and assets still load. The upstream DLL and source are not rewritten. If the required runtime or registry is unavailable, lobby joining/starting is blocked. This is compatibility detection, not anti-cheat or a security sandbox.

Build tests cover profile comparison, serialization and a real Harmony prefix applied to an isolated method. They do not replace a two-PC Steam lobby test or a joint Unity game launch.

Run `build.ps1 -GameDir <your Steam game directory> -SkipPackage` to compile against the supplied local game and run offline tests without packaging or launching it. Omit `-SkipPackage` only when preparing a release. Tests cover equivalent Manager/standalone runtime profiles, GUID/version mismatches, receipt classification, malformed network profiles and patch-signature/IL rejection.

Version 0.2.5 retains the native skirmish isolation and strict Lobby checks from 0.2.4. Its `settings.cfg` guard now rejects a structurally complete file whose player name is blank, so a reset default cannot overwrite or bypass the last valid per-account snapshot.

It also fixes three unsafe dictionary lookups in the native managed key-map reader. Unbinding the R/T/Y stance keys could throw `KeyNotFoundException`, causing the outer settings loader to skip its success flag and reopen first-run setup. A regression test executes the original reader against a sparse key map, reproduces the exception, and verifies the corrected reader on an isolated assembly copy. This is a loader test, not a full in-game launch test.
