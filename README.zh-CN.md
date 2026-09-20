# SCDE Mod Manager

**SE / scdemm：**Rawra 的 Script Extender（SE）负责支持 SE 模组。`+scdemm.1` 是适配版 SE 的标记，**不是第二个 Mod**。管理器安装下载到的 SE 新版之前，会在该副本中重新应用并检查一小段兼容保护，防止 SE 自行部署创意工坊 Mod、重启游戏而绕过管理器。联机衔接代码属于 Multiplayer Mod Compatibility。不会修改 Steam 本体，也不保证未来所有 SE 版本都兼容。

[English](README.md) · [SE 适配记录（英文）](docs/SE_INTEGRATION.md) · [语言包说明（英文）](docs/MANAGER_LANGUAGE_PACKS.md)

适用于《要塞：十字军东征决定版》（Steam App ID：`3024040`）的 Windows 免安装 Mod 管理器。通过独立游戏副本管理和运行 Mod，不替换 Steam 游戏本体文件。

## 使用

1. 从 [Releases](../../releases) 下载 **SCDE-Mod-Manager-Portable.exe**，放在可写入的文件夹中直接打开，无需安装。
2. 启动 Steam，在管理器中识别或选择原版游戏目录。
3. 点击「导入 Mod」，多选创意工坊中的支持包，或手动选择 `.scdemod` 文件。
4. 启用需要的 Mod，调整部署顺序，点击「带 Mod 启动」。

游戏副本位于原版旁的 `Stronghold Crusader Definitive Edition - SCDE Modded` 文件夹。需要正版游戏、Steam，以及容纳游戏副本的磁盘空间。管理器数据和设置独立于便携 EXE 保存。

## 功能

- 批量导入 `.scdemod` 和支持的 SE 创意工坊 Mod；启用、停用、排序和文件冲突提示。
- 排序影响文件部署覆盖优先级；BepInEx / SE 仍控制插件初始化，不代表能任意指定所有插件执行顺序。
- 已导入 Mod 的更新需玩家确认；SE 核心更新自动准备，使用统一的 BepInEx 运行库。
- 联机检查涵盖管理器清单、已加载插件和 SE 联机 Mod；不是反作弊，也不能保证所有游戏不同步问题都能被阻止。
- 启动进度提示、Steam 检查、游戏运行期间操作锁。
- 支持玩家编辑 JSON 语言包，内置英文和简体中文。
- SE 可以关闭；关闭时会同时停用明确声明依赖 SE 的 Mod，并提示玩家。

## 0.2.8 内置组件

| 组件 | 随软件附带版本 | 作者 |
|---|---|---|
| BepInEx Runtime | 5.4.23.5 | BepInEx contributors |
| Script Extender | 2.6.0+scdemm.1 | Rawra；管理器适配：JiuyeAyan |
| Multiplayer Mod Compatibility | 0.3.1 | JiuyeAyan |

三个组件初始启用，SE 可单独关闭。SE 自动更新后，实际安装版本可能高于附带版本。本项目不附带战争迷雾、Advanced Control、Serps 等可选玩法 Mod。

## 注意

- 本项目不是 Firefly / Steam 官方产品。Mod 可以执行代码，请只使用可信来源。
- 便携 EXE **未进行数字签名**。
- 管理器自身更新目前检查自己的 Steam 创意工坊订阅目录，尚未改为检查 GitHub Releases。
- 参见[发布说明](docs/RELEASE_0.2.8.md)、[构建说明](BUILDING.md)及[第三方声明](THIRD_PARTY_NOTICES.md)。各第三方组件保留原许可，不因放入本仓库而改为 MIT。
