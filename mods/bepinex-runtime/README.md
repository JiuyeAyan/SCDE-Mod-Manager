# BepInEx 运行库

这是 SCDE Mod Manager 的共用代码 Mod 运行库，包含 BepInEx 5.4.23.5 Mono x64 和 Harmony。

功能 Mod 应只打包自己的插件文件，并在 `manifest.json` 中声明：

```json
"dependencies": [
  {
    "id": "bepinex-runtime",
    "version": "5.4.23.5"
  }
]
```

管理器会在启用功能 Mod 时自动启用本运行库。运行库本身不改变游戏玩法。

## 构建

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\mods\bepinex-runtime\build.ps1
```

输出：`release/bepinex-runtime-5.4.23.5.scdemod`
