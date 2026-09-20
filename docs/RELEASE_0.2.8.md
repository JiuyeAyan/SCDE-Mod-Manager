# SCDE Mod Manager 0.2.8 — private review build

SE supports SE Mods; `+scdemm.1` identifies the Manager's adapted SE build, not a second Mod. On a core update, the Manager reapplies/checks the managed-mode guard on the downloaded copy before installation. Native SE Workshop Mod auto-deployment/restart remains blocked in managed launches; future compatibility is not guaranteed.

Download **SCDE-Mod-Manager-Portable.exe**. No installation is required. The file contains BepInEx Runtime 5.4.23.5, Script Extender 2.6.0+scdemm.1, Multiplayer Mod Compatibility 0.3.1, and the SE update helper. Installed SE may later update beyond this bundled baseline.

- Editable language JSON under the game copy's `BepInEx/config/modmanager/lang`.
- Open config Folder shortcut and inline SE toggle with dependent-Mod handling.
- Separate game copy, Workshop import/update discovery, launch locks and progress reporting.
- Manager source, component source/patches, prepared system packages and notices accompany this release in the repository.

## Verification boundaries

This upload preserves the existing 0.2.8 EXE. It is unsigned. The publication snapshot's 108 Manager/component Node tests passed; three tests belonging to the excluded Fog/Advanced Control projects were removed from this snapshot only. Prior packaging/startup checks are not proof of every gameplay/SE/Mod combination. No new two-player test is claimed by this upload. Third-party asset/license review is still required before public distribution; see THIRD_PARTY_NOTICES.md, including the correction to the old BepInEx license label.

SHA-256 (`SCDE-Mod-Manager-Portable.exe`):

```text
89a803f606185dd8dd88419261fe431890383ec913d64ef8e76427ab37e4ecdb
```

Keep this repository private until the owner's next review. No Steam Workshop upload or software behavior change is part of this publication.
