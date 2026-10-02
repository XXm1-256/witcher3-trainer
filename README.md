# The Witcher 3 Trainer

Adjust gold and XP, add items, customize equipment, and turn on combat or exploration helpers as needed.

**[Download English](https://github.com/XXm1-256/witcher3-trainer/releases/download/v0.1.4/Witcher3Trainer-v0.1.2-en-win-x64.zip)** · **[下载中文版](https://github.com/XXm1-256/witcher3-trainer/releases/download/v0.1.4/Witcher3Trainer-v0.1.2-zh-CN-win-x64.zip)** · [All releases](https://github.com/XXm1-256/witcher3-trainer/releases)

[简体中文介绍](README.zh-CN.md)

## Download and use

1. Download and extract a package. `Witcher3Modifier.exe` is at the top level; no separate .NET installation is needed.
2. Start the game and load a save, then open the trainer.
3. Click a function or use its displayed shortcut.

All shortcuts use the **numeric keypad** with Num Lock on. The **Hotkeys** switch at the top disables them together.

Save your progress before changing equipment, deleting items or teleporting. See the [player guide](docs/USAGE.md) for detailed instructions.

## Features

- **Choose your progression pace:** set gold directly or adjust gold income and XP multipliers.
- **Spend less during combat:** infinite health, stamina, breath and Adrenaline; no toxicity; item and ammunition preservation; one-hit kills.
- **Manage your inventory:** search for items, choose how many to add, find equipment by level, and delete several selected items at once.
- **Customize your gear:** choose a base weapon or armor, add modifiers and enchantments, adjust attributes, and repair durability.
- **Explore more easily:** teleport to map markers, prevent fall damage, use Cat night vision, and adjust movement, jump and swimming multipliers.
- **Try something different:** change weather, time, hair and beard; win the current Gwent match or reset your skill build.
- **Loot and gather:** collect eligible container items and harvest herbs within 5 meters.

Common functions are on the first page. Hover the round **i** for instructions. Enable settings saving at the top to restore switches and multipliers next time.

## Before using

- For Windows x64 and single-player use. The current target is **5.00c with DX12**.
- DLC items require their corresponding content installed.
- Functions operate while the game runs and leave its files unchanged.
- Teleporting to complex terrain may place you in the air or below a bridge. Use **No fall damage**.
- Some equipment attributes have a minimum set by the game. Required level can change with the item and its attributes; estimates help when choosing gear.

## Common questions

**Why hasn't an added item appeared yet?**

Return to a scene where Geralt can move, let the addition finish, then add the next item. Some actions wait during menus, story scenes or loading.

**What happens to auto-loot when I pause?**

It waits and resumes during gameplay. Locked, quest, special-interaction and theft-sensitive containers are skipped.

**What if teleporting leaves me airborne or my character is invisible?**

Place another marker and teleport again. A reload is usually unnecessary.

**How do I report a problem?**

Open an [issue](https://github.com/XXm1-256/witcher3-trainer/issues) with the game version, steps and message shown. Errors are recorded in `modifier.log.jsonl` beside the trainer; include the relevant excerpt.

English item labels use the game's internal English names. Some may look different from the names shown in your inventory.

## Source and tutorials

To build the trainer, change a feature or learn how it works, start with the [developer tutorial](docs/DEVELOPMENT.md). For game updates, see [address migration](docs/ai/VERSION_MIGRATION.md). AI development tools have a separate [agent guide](docs/ai/AGENT_GUIDE.md).

[Compatibility and verification](docs/ai/PROJECT_STATE.md) · [Contributing](CONTRIBUTING.md) · [MIT license](LICENSE) · [Asset attribution](NOTICE.md)
