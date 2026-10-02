# Player guide

[简体中文](USAGE.zh-CN.md)

1. Extract the language package into a writable folder. Load a game save, then run `Witcher3Modifier.exe`.
2. Use the Common functions page for gold, multipliers and switches. Hover the round **i** for short instructions.
3. **Save switches and multipliers** remembers selected settings. **Hotkeys** controls all shortcuts. Input boxes should continue accepting normal typing.
4. After adding an item, return to a movable gameplay scene so the queued request finishes before adding another item. Pauses, cutscenes and loading can delay game-thread operations. No-toxicity activation may be unavailable during story scenes.

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

Select a catalog item, set quantity, then use **Give selected item**. DLC entries require the relevant content installed. Reference level filters catalog/base equipment; inventory filters use the actual item instance. Unspecified catalog levels may depend on generation rules.

Choose the base weapon or armor for equipment creation. Select modifiers and their target values, then validate before applying. Type hints indicate weapon/armor suitability. Native base attributes can impose a minimum; the reachable-value option helps when the requested target is unavailable. Estimated level is a guide, not a freely editable independent level field. The game confirms the final level and damage range. Save/reload verifies persistence.

Use Ctrl-click/Shift-click for multi-item deletion. The specified quantity applies to each selected item, limited by its available amount. Verify the confirmation list first.

## Teleport and looting

Place a marker in the current region and return to gameplay. Some destinations load slowly or have several vertical surfaces; a bridge marker can resolve below the bridge. Teleport may arrive in the air: enable **No fall damage**. If the player model is missing while airborne, mark another place and teleport again; a reload is usually unnecessary. A failure is reported in status/log output so another attempt can be made.

Auto-loot/gather affects eligible containers and herbs within 5 meters. It skips locked, quest, special-interaction and theft-sensitive containers. Pauses/loading wait and resume; validated entry/type failures stop the feature and are logged. Some scripted containers are intentionally excluded.

Logs are written beside the executable and contain diagnostic addresses/version information. Share only a redacted excerpt. Keep a separate game save before destructive inventory changes.
