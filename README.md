# SCDE Mod Manager
Most codes were finished by CHATGPT 5.6 Sol

**SE / scdemm:** Script Extender (SE), by Rawra, enables SE Mods. `+scdemm.1` identifies our adapted SE build, **not a second Mod**. Before installing a downloaded SE update, the Manager reapplies and checks a small managed-mode guard on that copy. It prevents SE's own automatic Workshop Mod deployment/restart from bypassing the Manager. Multiplayer integration lives in Multiplayer Mod Compatibility. Steam originals are not patched; future SE compatibility is not guaranteed.

[简体中文](README.zh-CN.md) · [SE integration details](docs/SE_INTEGRATION.md) · [Language packs](docs/MANAGER_LANGUAGE_PACKS.md)

A portable Windows mod manager for **Stronghold Crusader: Definitive Edition** (Steam App ID `3024040`). Install and organize Mods in a separate game copy, without replacing your original Steam game files.

## Quick start

1. Download **SCDE-Mod-Manager-Portable.exe** from [Releases](../../releases). No installer is required; put the EXE in a writable folder.
2. Start Steam, open the Manager, and select or detect your original SCDE installation.
3. Choose **Import Mods** to select subscribed Workshop packages or browse for `.scdemod` files.
4. Enable the Mods you want, arrange deployment order, and choose **Launch with Mods**.

The adjacent `Stronghold Crusader Definitive Edition - SCDE Modded` folder is the managed game copy. Steam, a legitimately owned game, and space for this copy are required. Mod data/settings persist separately from the portable EXE.

## Features

- Batch import of `.scdemod` packages and supported SE Workshop Mods.
- Enable/disable Mods, reorder file deployment, and inspect file conflicts. BepInEx/SE still control plugin initialization; row order is not a universal plugin execution order.
- Manual confirmation for imported Mod updates; automatic preparation of SE core updates while keeping the shared BepInEx runtime.
- Protocol v4 multiplayer checks compare actual loaded runtime/plugin identities and networked SE Mods, not Manager package wrappers. SE-only peers can join with an explicit unverified warning; extra plugins cannot be verified without MMC. These checks are not anti-cheat or a guarantee against all gameplay desynchronization.
- The game copy is created/prepared when **Launch with Mods** is clicked, not when the Manager opens. Current copies are reused; older copies may need a one-time isolation migration at launch.
- Declared persistent Mod data is preserved during deployment, and ordinary packages cannot replace built-in runtime files.
- Launch progress, Steam checks, and controls locked while the managed game is running.
- Editable English/Chinese JSON language packs; community translations are supported.
- An SE toggle; disabling SE also disables declared dependent Mods with a notice.

## Included by default in 0.2.10

| Component | Bundled version | Author |
|---|---|---|
| BepInEx Runtime | 5.4.23.5 | BepInEx contributors |
| Script Extender | 2.8.0+scdemm.1 | Rawra; Manager integration by JiuyeAyan |
| Multiplayer Mod Compatibility | 0.4.0 | JiuyeAyan |

SE is initially enabled but can be disabled. The installed SE version can be newer than the bundled baseline after an update. No Fog, Advanced Control, Serps, or other optional gameplay Mod is bundled here.

New application builds obtain and validate the latest official SE release instead of silently reusing 2.6.0. This source update does not replace existing GitHub Release EXEs; check each Release's version separately.

## Notes

- This is an unofficial community project, not an official Firefly/Steam release.
- Use only Mods you trust: plugins execute code. Import checks do not make untrusted Mods safe.
- The Windows EXE is **not digitally signed**.
- Manager self-update currently checks its own Steam Workshop subscription, not GitHub Releases. Uploading a GitHub release does not change that behavior.
- See [0.2.10 source update notes](docs/RELEASE_0.2.10.md), [package schema](docs/SCDEMOD_SCHEMA.md), [build instructions](BUILDING.md), and [component notices](THIRD_PARTY_NOTICES.md). Third-party components keep their respective licenses; this repository's MIT license does not relicense SE or its bundled assets.
