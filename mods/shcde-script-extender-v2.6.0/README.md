<div align="center">

<img src="docs/images/logo.gif" width="300" height="200"/>

  # SHCDE-Script-Extender
  
  **`SHCDE-SE` is a Script Extender for [Stronghold Crusader: Definitive Edition](https://store.steampowered.com/app/3024040/Stronghold_Crusader_Definitive_Edition/)**

  [🇬🇧 English](README.md) | [🇩🇪 Deutsch](README_DE.md) | [🇷🇺 Русский](README_RU.md) | [🇵🇱 Polski](README_PL.md)

  <br/>

| CI/CD | Release | Tech Stack | Platform | License |
|:---:|:---:|:---:|:---:|:---:|
| [![pipeline status](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/badges/main/pipeline.svg)](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/pipelines) | [![latest release](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/badges/release.svg)](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases) | ![x64](https://img.shields.io/badge/CPU-x64-orange) ![C#](https://img.shields.io/badge/C%23-239120?logo=c-sharp&logoColor=white) [![RedBird](https://img.shields.io/badge/RedBird-8A2BE2)](https://gitlab.com/Rawra/redbird) [![NLua](https://img.shields.io/badge/NLua-8A2F52)](https://github.com/NLua/NLua) [![R3](https://img.shields.io/badge/R3-50000A)](https://github.com/Cysharp/R3) [![Iced](https://img.shields.io/badge/Iced-000000)](https://github.com/icedland/iced) [![BepInEx](https://img.shields.io/badge/BepInEx-FF2F52)](https://github.com/BepInEx/BepInEx)| ![Windows](https://img.shields.io/badge/OS-Win64-0078D6?logo=windows) | [![Static Badge](https://img.shields.io/badge/License-LGPLv3-teal)](LICENSE.md)

</div>

This project exposes the internal capabilities of the underlying engine of Stronghold Crusader: Definitive Edition for modders and mappers alike. Its main goal is to allow map-makers to add custom logic for their maps to make map concepts such as tower defenses or otherwise heavily non-vanilla gameplay possible.

Since initial release, the project has grown into more of a general Modding SDK of sorts, with inbuilt support for overriding most of the game's Assets, patching XAMLs and a decent amount of Events to hook on-to.

It achieves this all by detouring (via [PolyHook2](https://github.com/stevemk14ebr/PolyHook_2_0) and [Iced](https://github.com/icedland/iced)) many undocumented engine functions and providing a higher level C# API for them, with the addition of a sandboxed LUA layer that sits in-between using [NLua](https://github.com/NLua/NLua), with most Events being based on [R3](https://github.com/Cysharp/R3) for various game events.

## Motivation

Once upon a time there was a Stronghold Crusader (the original) map called **"Berglöwen"** - translated literally it becomes "Mountain Lions", which was pretty much a joke map where lions would multiply upon killing another non-lion entity (zombie style, if you will it.), where the objective was to survive as long as possible. This was done with a experimental tool called SHCHooks, it was in essence, the precursor to this project.

It was the first step in creating non-standard gameplay loops for this game.

## Scope

This project is a **Script Extender** first and foremost. Formal reverse engineering is **not the primary objective**; it is only used as a means to enable the extender’s functionality. The project does **not aim to fully understand the underlying engine**, nor does it attempt to fully decompile or recompile the game binary.

The scope is strictly limited to enabling **[runtime](https://en.wikipedia.org/wiki/Execution_%28computing%29#Runtime) modifications**.

Please do note, that this is **not** something like [UCP](https://unofficialcrusaderpatch.com/) nor does it attempt to be, therefore do not expect significant bugfixes or similiar from this project.

If you're still keen on bugfixes, check out the **[shcde-fixes](https://gitlab.com/rawra-stronghold-crusader/shcde-fixes)** mod.

## Features
- [x] High-level API (C#, LUA)
  - [x] **[Extended AI Modding](docs/guides/extended-ai-modding.md)**
  - [x] **[Asset API](docs/guides/asset-api.md)**
    - [x] Noesis Support (Manipulation of existing UI and Custom UI)
    - [x] Sound Replacement
    - [x] Music Replacement
    - [x] Sprite Replacement (Noesis and Unit ones, individual or Atlas incl. Cursors)
    - [x] Custom Localization
    - [x] AIV / AIC Overrides
    - [x] Lua5 Scripting
  - [x] **[Map Archive System](docs/guides/archive-system.md)**
    - [x] Embedded zip-archive in map files.
    - [x] Map Editor: ImGUI-based toolkit.
    - [x] Raised max map file size limit (8MB to 128MB)
  - [x] **Noesis API**
    - [x] **[Mod XAML](docs/guides/xaml-api.md)**
    - [x] **[Map XAML](docs/guides/map-xaml-guide.md)**
    - [x] **[Mod Lobby Settings](docs/guides/mod-lobby-settings.md)**
  - [x] **[Runtime Config](docs/guides/runtime-config.md)**
    - [x] Managed Assembly Immediate/Displacement System
  - [x] **[Projectile API](docs/guides/projectile-api.md)**
  - [x] **[Unit API](docs/guides/unit-api.md)**
  - [x] **[Tribe API](docs/guides/tribe-api.md)**
  - [x] **[Vegetation API](docs/guides/vegetation-api.md)**
  - [x] **[Building API](docs/guides/building-api.md)**
  - [x] **[Tiles API](docs/guides/tile-api.md)**
  - [x] **[Player API](docs/guides/player-api.md)**
    - [x] State Management
    - [x] Resources Management
    - [x] Victory Condition Management
    - [x] Camera Management
    - [x] Team Management
  - [x] **[Time API](docs/guides/timers-system.md)**
    - [x] Date API
    - [x] Timer API
  - [x] **[Trigger API](docs/guides/triggers-system.md)**
  - [x] **[Translation API](docs/guides/translate-api)**
  - [x] **[Sound API](docs/guides/sound-api.md)**
  - [x] **[Save API](docs/guides/save-data-api.md)**
  - [x] **[Metadata API](docs/guides/metadata-system.md)**
  - [x] **[Persistent Map Data](docs/guides/persistent-data-api.md)**
  - [x] **[Pitch API](docs/guides/pitch-api.md)**
  - [x] **[Network API](docs/guides/network-api.md)**
    - [x] **[Chat Handling](docs/guides/network-api.md#chat-handling)**
    - [x] **[Command Handling](docs/guides/network-api.md#custom-chat-commands)**
    - [x] **[Packet Handling](docs/guides/network-api.md#receiving-packets-table--rpc)**
    - [x] **[Lockstep Chore Handling](docs/guides/network-api.md#the-chore-transport-lockstep-delivery)**
  - [x] **[Custom Map Previews](docs/guides/map-creation-guide.md#custom-map-previews)**
  - [x] Map Editor Extensions
    - [x] **[Mirror Tool](docs/guides/map-editor-extensions.md#mirror-tool)**
    - [x] **[Mass-tile Editor Tool (with Preview and Undo)](docs/guides/map-editor-extensions.md#mass-tile-editor)**
    - [x] **[Tile-Inspector and Editor](docs/guides/map-editor-extensions.md#tile-inspectoreditor)**
- [x] High-level Assembler and Detouring toolkit
  - [x] **[Iced](https://github.com/icedland/iced/tree/master)** as the primary Assembler/Disassembler engine.
  - [x] **[RedBird.NET](gitlab.com/Rawra/redbird)** as the primary native manipulation library (detouring, raw-asm, etc)
- [x]  [Dear ImGUI](https://github.com/ocornut/imgui) API via [UU-ImGUI](https://gitlab.com/rawra-rain-world-mods/rain-world-imgui-api)
- [x] Reverse Engineering Databases
  - [x]   [ReClass.NET](https://github.com/ReClassNET/ReClass.NET) Project
  - [x]   [IDA](https://hex-rays.com/ida-pro) Database
- [x] Misc Features
  - [x] Ability to play multiplayer matches with AIs only.
  - [x] Unrestricted min/max game speed.
  - [x] Lobby Isolation (only see lobbies that use the Script Extender as well)
  - [x] [BepInEx Mod Support for Steam Workshop](docs/guides/workshop-mod-creation-guide.md)
  - [x] [Allows multiple game instances for testing](docs/guides/multiple-instances.md)

## Quickstart

Check out the [quick-start](docs/guides/quick-start.md) guide to get cooking.

## Planned
- [ ] AI (C/V) Scripting
- [ ] PlayerAPI->SetSelectedChimps(...)

## Under consideration
- [ ] Archive Ext: Custom Tile Sets
- [ ] Archive Ext: Custom Sprites

## Known Issues
* If encountering crashes under Linux/Proton upon reaching any PolyHook2 constructor, you might need to drop a up-to-date `msvcp140.dll` the native dependency can work with (approx. 14.51.36231+) into the root game folder.
* Any debugger attached with active INT3 breakpoints at certain positions, while the plugin is initializing, will cause the bootstrapper to fail: This is not fixable due to how INT3 breakpoints work. Either disable all active bp's, use hw bp's or simply attach yourself post-init.
* `Player_SetProductionGoodAllowed`, `Player_SetUnitRecruitable` and similiar do not update the visuals immediately.

Found a crash? Check out how to report them [here](docs/guides/error-reporting.md).

## Compatibility

| Game Version | Required Plugin Version | Notes |
|:-|:-|:-|
| **2.8.0.2** | `v1.43.0+` or `v2.3.0+` |
| **2.8.0.1** | `v1.41.0+` |
| **2.8.0.0** | `v1.40.0+` |
| **2.7.0.1** | `v1.32.0+` |
| **2.7.0.0** | `v1.27.0+` |
| **2.6.0.2** | `v1.23.0+` |
| **2.6.0.1** | `v1.18.0+` |
| **2.6.0.0** | `v1.17.0+` | Custom Lord Asset Update
| **2.5.0.1** | `v1.14.0+` | The February Update
| **2.0.4.1** | `v1.13.0` | Initial Release version |

## Security

For a word on the general safety of this Script Extender, please see the [SECURITY.md](docs/SECURITY.md) file.

## Performance

The Script Extender and shcde-fixes are trying to do the minimum work on any of its core indirection points. There is a guaranteed overhead given due to many native-to-managed callbacks and vice versa, which are not free and not quite efficient on older .NET runtimes (or rather: the mono environment the game uses).

Although these shouldnt be affecting the average performance of your game by any significant margin or noticeable amount. If you feel like you are experiencing lag: please first consult the active mods you are using before suspecting the script extender.

The biggest hit in performance will be on the startup time, runtime performance shouldnt be affected that much, but of course, this depends on the user machine, etc, etc.

## Documentation

This project uses [DocFX](https://dotnet.github.io/docfx/) to generate its documentation automatically from source code, with the exception of a few articles incl the lua documentation. Please check out the [GitLab Pages](https://rawra-stronghold-crusader.gitlab.io/shcde-script-extender) link to see the projects core C# documentation.
Alternatively refer to the Features section and the relevant links there.

Heres a quick [Lua API reference](docs/guides/lua-reference.md) for your convenience.
For API-specific documentation check out [this](docs/guides/) directory.

## Building the Project

See: [Building](docs/guides/building-the-project.md)

## Installation

First, make sure your game is set-up with BepInEx. If not, consider getting the latest release for it from [here](https://gitlab.com/rawra-stronghold-crusader/shcde-bepinex) or the [official repository](https://github.com/bepinex/bepinex/releases)

> NOTE: At the time of writing, the script extender currently targets BepInEx version `5.4.23.4`

Then visit the [releases](https://gitlab.com/rawra-stronghold-crusader/shcde-script-extender/-/releases) page and snatch the latest one!

The file you'd look out for is called `SHCDESE_X.X.X.zip`.

After you have downloaded it, you can proceed to simply drag and drop the contents of the zip archive into your main game directory.

How do you know if it worked? Well, you either have a command line running when starting the game, or you see this at the left side of the main menu:

<img src="docs/images/scriptextendermarker-1.png" width="300" height="256"/>

> NOTE: Simply seeing this does not guarantee the script extender being in working conditions, make sure to check out the commandline log or the LogOutput.txt file from BepInEx when you encounter issues. Especially after game updates!

## Example Projects

Here you can find some example projects that have been made utilizing this library.

- **[Crusaders United](./examples/crusaders_united/README.md)**: Remake of [Castle Fight](https://www.hiveworkshop.com/threads/castle-fight-v1-13b.76788/)
- **[Lion Crusader](https://gitlab.com/rawra-stronghold-crusader/shcde-lion-crusader)**: A mod that severery buffs lions

## Community Projects

Check out some of the stuff people have made utilizing this library.
- **[Lorrdy Subjects](https://steamcommunity.com/sharedfiles/filedetails/?id=3671450357)**: Subjugate other lords by converting them to your side instead of killing them.
- **[Handicap](https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/102)**: New difficulty systems to spice up gameplay.
- **[Unit Stat Editor](https://www.nexusmods.com/strongholdcrusaderdefinitiveedition/mods/38)**: Adds many easy to change settings for users to customize their experience.

## Community

We have a [Discord](https://discord.gg/waXrWJUZzq)! There you can stay up-to-date about announcements or chat with fellow mappers that use the script extender.

## Contributing

Want to help out?

If you have a bug to report or know C#, are decent with reverse engineering, or simply enjoy working with debugging tools, your contributions are welcome!  

Check out the [CONTRIBUTING](CONTRIBUTING.md) article for details on how to get started.

## Credits
- **Zaqura**: Huge help in testing.
- **Ensrick**: Been a big help in testing as well.
- **Wilps**: Russian localization.
- **Serp**: Testing, Bugfixes, Misc additions alongside custom lord details.
- **Gynt**: Sharing some known RE'd id's such as from the game's Chore system.

## Legal

This project is **not affiliated with, endorsed by, or sponsored by [FireFly Studios Limited](https://fireflyworlds.com/)**. 

It is an independent, free, and open-source software [FOSS](https://de.wikipedia.org/wiki/Free/Libre_Open_Source_Software) project created for educational and community purposes.

The use of this software is **at your own risk**. The authors are not responsible for any damage, loss of data, or other issues that may result from using this software.

All trademarks, logos, and game assets referenced belong to their respective owners.

## License

This project is using the LGPLv3 license.

This license applies only to the source code in this repository. Other files, such as artwork or branding assets, may be subject to different licensing terms.

## Support the Project

This Script Extender is a project maintained in my (@Rawra) free time.  
If you enjoy it and want to support its continued development, you can buy me a coffee:  

[![ko-fi](https://ko-fi.com/img/githubbutton_sm.svg)](https://ko-fi.com/0xrawra)

## Other Projects

While not directly involved with the Definitive Edition, [UCP](https://github.com/UnofficialCrusaderPatch/UnofficialCrusaderPatch) may be of interest to those who'd like to see more interesting projects for Stronghold, so be sure to check them out as well.