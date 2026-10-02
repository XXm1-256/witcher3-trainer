# Player guide

[简体中文](USAGE.zh-CN.md)

1. Extract the language package into a writable folder. Load a game save, then run `Witcher3Modifier.exe`.
2. Use the Common functions page for gold, multipliers and switches. Hover the round **i** for short instructions.
3. **Save switches and multipliers** remembers selected settings. **Hotkeys** controls all shortcuts. Input boxes should continue accepting normal typing.
4. After adding an item, return to a scene where Geralt can move and wait for the item to appear before adding another. Menus, cutscenes and loading can delay some actions. No toxicity may be unavailable during story scenes.

## Shortcuts

All numbers and symbols below are from the **numeric keypad**, with Num Lock on. The top number row is not used.

| Key | Function |
|---|---|
| Numpad 1–8 | Health, stamina, breath, no toxicity, horse stamina, fearless horse, crafting, durability |
| Numpad 9 / 0 | Keep non-gold items / one-hit kill |
| Ctrl + Numpad 1–5 | Teleport, ignore equipment level, ignore weight, keep ammunition, repair equipment |
| Ctrl + Numpad 6–9 / 0 | No fall damage, Gwent victory, reset skill build, Adrenaline / enemy speed |
| Numpad /, *, -, + | Cat night vision, movement speed, jump height, swimming speed |
| Ctrl + Numpad + | Nearby looting and gathering |

## Items and equipment

Select an item, set the quantity, then click **Add selected item**. DLC items require the corresponding content installed. Add items and Custom equipment use reference levels to help find suitable gear; Inventory uses each item's current required level. Some items receive their level when they are added.

Choose a base weapon or armor, select modifiers and enter the values you want. Check the setup before applying it. Labels beside the modifiers show which equipment they fit. Some items have a minimum for an attribute; use **Use reachable value** if your chosen value cannot be applied. Required level is estimated from the equipment and its attributes, rather than set separately. Check the final level and damage range in the game. Save and reload to check that your changes remain.

Use Ctrl-click/Shift-click for multi-item deletion. The specified quantity applies to each selected item, limited by its available amount. Verify the confirmation list first.

## Teleport and looting

Place a marker in the current region and return to gameplay. Some destinations load slowly or have several vertical surfaces; a bridge marker can resolve below the bridge. Teleport may arrive in the air: enable **No fall damage**. If the player model is missing while airborne, mark another place and teleport again; a reload is usually unnecessary. A failure is reported in status/log output so another attempt can be made.

Turn on nearby looting and gathering to collect container items and herbs within 5 meters. Locked, quest, special-interaction and theft-sensitive containers are skipped. It waits during pauses/loading and resumes during gameplay. If it stops with an error, check the message and the log; some story containers cannot be collected this way.

If something goes wrong, include the message and the relevant lines from `modifier.log.jsonl` beside the executable when reporting it. Keep a separate save before deleting items or changing equipment.
