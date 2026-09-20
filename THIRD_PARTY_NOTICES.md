# Component attribution and publication notes

The Manager's package metadata declares MIT. That does **not** apply to every bundled file. Preserve each component's license, copyright and source notices.

## Script Extender

- Author: Rawra / Viktor Legodzinski.
- Upstream: https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender
- Bundled upstream baseline: 2.6.0; adapted package: 2.6.0+scdemm.1.
- Upstream source license: GNU LGPL version 3; see `mods/shcde-script-extender-v2.6.0/LICENSE.txt` and `mods/shcde-script-extender-adapter/GPL-3.0.txt`.
- Adaptation: five instructions guard `MapModManager.TryUpdateModsFromRemote` when `SCDEModManagerLaunchId` is set. The source patch and patcher are included under `mods/shcde-script-extender-adapter` and in the adapted package's `SCDEMM-notices` directory.
- This is not an official upstream build. Existing Mods still load; native automatic Workshop deployment/restart is intentionally disabled in managed launches.
- Upstream README states that its source license does not necessarily cover artwork/branding. Before making this repository public, review redistribution terms for upstream assets and dependencies, including fonts, branding/media and native libraries. No blanket permission or completed legal audit is claimed here.

## BepInEx and related runtime dependencies

- BepInEx 5.4.23.5: https://github.com/BepInEx/BepInEx
- Harmony / HarmonyX: https://github.com/pardeike/Harmony / https://github.com/BepInEx/HarmonyX
- Mono.Cecil: https://github.com/jbevain/cecil ; the included helper license is `mods/shcde-script-extender-adapter/Mono.Cecil.LICENSE.txt`.
- MonoMod: https://github.com/MonoMod/MonoMod
- Unity Doorstop: https://github.com/NeighTools/UnityDoorstop

The original runtime package includes `BepInEx/SCDE_RUNTIME_NOTICES.txt`. **Correction:** that older notice incorrectly calls BepInEx itself LGPL-2.1; the official BepInEx **v5.4.23.5 LICENSE is MIT**. See `licenses/BepInEx-5.4.23.5-LICENSE.txt`, retrieved from the upstream version tag. Additional upstream HarmonyX, MonoMod and Unity Doorstop license texts are supplied under `licenses/`; the latter three were retrieved from upstream's default branch and still require a bundled-version audit. This publication does not silently rebuild the existing EXE to alter its embedded historical notice. Review the complete runtime/dependency notice and source distribution requirements before public distribution.

## Multiplayer Mod Compatibility

Author: JiuyeAyan. Source, component notice and package are included. The component uses the shared BepInEx/Harmony runtime and game APIs; it does not redistribute the game assemblies it references. No new license for separately attributed Mods is imposed by this publication snapshot.

## Manager application dependencies

Electron, Chromium, Node.js, adm-zip, yauzl and transitive dependencies retain their own licenses. Exact npm versions are locked in `package-lock.json`; installed packages provide their corresponding license files. Electron distribution license files are supplied separately in `licenses/`.

This private upload is for owner review. It does not change the visibility of upstream projects, grant new third-party permissions, or certify that all future SE releases are compatible.
