# The Witcher 3 Trainer

[简体中文](README.zh-CN.md) · [Player guide](docs/USAGE.md) · [Developer guide](docs/DEVELOPMENT.md) · [AI Agent guide](docs/ai/AGENT_GUIDE.md)

A Windows x64 external trainer for single-player The Witcher 3, with Chinese and English executables. It connects to the running game, uses validated memory structures and the game's existing gameplay flows, and provides a desktop interface with numpad shortcuts.

## Download and use

Download the **zh-CN** or **en** ZIP from [Releases](https://github.com/XXm1-256/witcher3-trainer/releases), extract it into a writable folder, load a game save, and launch `Witcher3Modifier.exe`. The release is self-contained; players do not need the .NET SDK. Keep each language package in its own folder. Save progress before applying equipment changes or teleporting.

## Features

- Gold editing and gold/experience multipliers.
- Health, stamina, breath, toxicity, horse stamina/fear, Adrenaline and combat helpers.
- Item and ammunition preservation, equipment repair, crafting and equipment-level helpers.
- Item search and quantity selection, DLC metadata, equipment creation/editing, modifier targets and multi-item deletion.
- Map-marker teleport, fall protection, Cat night vision, movement/jump/swimming controls.
- Weather, time, hair/beard and visual experiments; Gwent victory and skill reset.
- Nearby container looting and herb gathering within 5 meters, with pause/loading recovery.
- Saved switches/multipliers, shortcut master switch, tooltips and activation feedback.

## Compatibility and validation

The current executable profile was developed against **5.00c, Windows DX12 x64**. The repository includes a legacy reference address profile; this does not establish compatibility with every edition or unknown executable. Operations perform individual code/object checks. See [version migration](docs/ai/VERSION_MIGRATION.md) before adapting a new build.

Builds, offline regression checks and English UI checks have been run. Many gameplay functions have prior user observations, but this is not a complete automated gameplay suite. Pause-recovery handling was checked offline; swimming displacement, very high movement multipliers, occasional weapon auto-swaps and all complex teleport destinations still need broader gameplay acceptance. See [project state](docs/ai/PROJECT_STATE.md).

English UI labels and messages are translated. The English item catalog displays internal English game identifiers; some are abbreviated/technical rather than the official localized inventory name. Chinese search aliases are retained.

## Build

On Windows, install **.NET 8 SDK**. English generation also requires **Python 3.10+**.

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Language zh-CN
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Language en
```

Outputs: `dist/zh-CN/Witcher3Modifier.exe` and `dist/en/Witcher3Modifier.exe`.

[Architecture and learning path](docs/DEVELOPMENT.md) · [Contribution rules](CONTRIBUTING.md) · [AI version migration](docs/ai/VERSION_MIGRATION.md)

## License and attribution

Original trainer code is released under [MIT](LICENSE). Game metadata, trademarks and the wolf medallion artwork have separate attribution and rights described in [NOTICE](NOTICE.md). This is an unofficial fan project.
