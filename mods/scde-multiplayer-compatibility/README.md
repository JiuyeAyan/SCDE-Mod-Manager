# Multiplayer Mod Compatibility

SCDE Mod Manager 的必需联机组件。进入 Steam Lobby 后，双方会交换由“管理器已启用 Mod + 实际加载的运行时 Mod”的 ID 和版本生成的兼容性指纹；本机加载顺序不参与联机判定。

- 房主会拒绝指纹不一致的玩家。
- 未安装或未运行此组件的玩家在短暂握手等待后也会被拒绝。
- 房间内仍有未通过校验的玩家时，阻止开始多人游戏。
- 房间资料或检测运行状态无法确认时默认拒绝加入或开局，不静默放行。
- 0.3.0 同时读取 BepInEx 实际加载的插件和 Script Extender 已注册的资源 Mod；SE 明确标注为仅客户端的资源允许不同，基础组件始终要求一致。管理器自身的启用清单仍严格比较。
- 不比较文件内容或配置，不认证作者；它不是反作弊系统。

### Startup metadata optimization (0.3.1)

In manager mode with SE 2.6.0, Workshop plugin maps are identified by reading only their bounded root `info.json` with SE's existing ZIP library. A runtime prefix then preserves SE's existing rule to exclude BepInEx packages from the playable map list, without decompressing/recompressing the complete payload. Ordinary/resource maps and unreadable metadata keep SE's original handling. No SE or subscribed Mod file is rewritten by this optimization, no security setting is disabled, and no gameplay or lobby matching rules change.

The metadata reader reuses the JSON assembly already loaded by SE; it does not require the desktop-only System.Runtime.Serialization assembly. The existing low-frequency runtime tick also reports readiness once the game's sprite resources and main menu (or first-run controls screen) are ready. It only reads the existing view-model instance and writes a launch-ID-tagged signal inside the game copy. No game state is changed, and no Serps field compatibility patch is included.

### Script Extender integration (0.3.0)

The v3 lobby profile combines the manager's enabled-package receipt with loaded BepInEx plugins and the Script Extender public asset registry. Networked SE Mods must match by GUID and version; client-only SE assets are omitted from this additional runtime comparison. The manager package comparison remains strict. Both peers need the updated checker. SE's own lobby filters and join checks remain active.

In manager mode, a Harmony prefix skips SE 2.6.0's automatic Workshop installation/update/unsubscription/restart routine. Already installed SE plugins and assets still load. The upstream DLL and source are not rewritten. If the required runtime or registry is unavailable, lobby joining/starting is blocked. This is compatibility detection, not anti-cheat or a security sandbox.

Build tests cover profile comparison, serialization and a real Harmony prefix applied to an isolated method. They do not replace a two-PC Steam lobby test or a joint Unity game launch.

The host rejects players whose enabled Mod IDs or corresponding versions differ. Load order is ignored. Players without this component are rejected after the handshake grace period, and the multiplayer game cannot start while compatibility is unverified.

Version 0.2.5 retains the native skirmish isolation and strict Lobby checks from 0.2.4. Its `settings.cfg` guard now rejects a structurally complete file whose player name is blank, so a reset default cannot overwrite or bypass the last valid per-account snapshot.

It also fixes three unsafe dictionary lookups in the native managed key-map reader. Unbinding the R/T/Y stance keys could throw `KeyNotFoundException`, causing the outer settings loader to skip its success flag and reopen first-run setup. A regression test executes the original reader against a sparse key map, reproduces the exception, and verifies the corrected reader on an isolated assembly copy. This is a loader test, not a full in-game launch test.
