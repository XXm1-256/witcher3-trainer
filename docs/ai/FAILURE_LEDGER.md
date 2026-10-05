# Failure history and maintenance boundaries

[简体中文](FAILURE_LEDGER.zh-CN.md) · [Cold start](COLD_START.md)

Updated 2026-10-05; code baseline v0.1.7. These are sanitized technical summaries of recorded observations in the private engineering archive. Original dumps, full logs and game scripts are not in the public repository. Source links identify current guards or implementations, not substitutes for historical originals. Unknown causes and unverified results remain open. These are evidence boundaries rather than permanent bans on research.

## F01: Incorrect absolute-jump displacement crashed installation (2026-09-29)

- Observation: the first inventory hook caused an access violation and failed installation verification. Its four-byte RIP displacement after `FF 25` contained `90 90 90 90`.
- Correction: `FF 25 00 00 00 00` followed by an eight-byte target; cover complete instructions, validate the return trampoline and verify before resuming threads, rolling back on failure.
- Code: [GameInventoryHooks](../../Witcher3Modifier/GameInventoryHooks.cs). Further changes require local execution stubs, register/stack contracts and target-byte checks. Compilation alone is insufficient.

## F02: A nearby address was mistaken for FactsAdd (2026-10-02)

- Observation: horse-stamina activation crashed; 5.00c candidate `0x1F58600` was inside a function with an incorrect return path. Registration and function boundaries identified `0x1F58700`. WER captured the fault site but no complete dump stack; the full causal chain was not reconstructed.
- Correction: the [GameVersion](../../Witcher3Modifier/GameVersion.cs) mapping and add/remove prefix validation in [ValidateDebugFacts](../../Witcher3Modifier/GameItemScheduler.cs).
- Boundary: enable/independent read/disable/independent read passed; this is not visual verification of stamina consumption while riding. Re-resolve registration, boundaries and parameters on new versions.

## F03: A player component was treated as the game world (2026-09-30)

- Observation: navigation queries crashed. `player+0xF0` referenced a component passed incorrectly to navigation. Actual RTTI and an earlier exception context supported this implementation error; the final graphics-driver fault did not invalidate that evidence.
- Correction: [GameTeleport](../../Witcher3Modifier/GameTeleport.cs) resolves the world through the handle at `game+0xF0`, validates CGameWorld and bounds navigation grids/tables.
- Revisit with: actual target RTTI, object provenance and read-only navigation checks, then authorized preview and movement. Fake-object ABI tests cannot prove the real object type.

## F04: A short native frame was passed as a complete script VM frame (2026-10-01)

- Observation: script cleanup crashed reading the frame's temporary-argument table. An 80-byte native frame did not satisfy the interpreter cleanup contract and overlapped parameter storage.
- Correction: remove that call approach. Complete script calls are in [Respec](../../Witcher3Modifier/GameItemScheduler.Respec.cs) and [ActorScript](../../Witcher3Modifier/GameItemScheduler.ActorScript.cs), with independent arguments, output, frame and ownership.
- Revisit with: target ABI, frame size, temporary cleanup and external return-pointer verification. Do not confuse interpreter local-argument pointers with external return buffers. This failure does not invalidate verified native wrappers.

## F05: Existing OnTick metadata was assumed to guarantee execution (2026-09-30)

- Observation: the earlier OnTick request had zero hits during gameplay; IsFocusModeActive had recurring hits.
- Correction: [FindTick](../../Witcher3Modifier/GameItemScheduler.cs) validates the actual triggering function's metadata. Scheduling avoids calling game objects directly from an arbitrary external thread.
- Boundary: recheck execution frequency and the game thread for a new version. A registered name does not guarantee execution in the current scene; pause may delay processing.

## F06: Timeout was treated as no addition and the mutation was replayed (2026-09-30)

- Observation: a silver ingot was reported as failed although inventory increased; the queued request completed later.
- Correction: [Give/Execute](../../Witcher3Modifier/GameItemScheduler.cs) and [ItemBatch](../../Witcher3Modifier/MainForm.ItemBatch.cs) retain one request and confirm inventory deltas. Completed and unconfirmed entries are not automatically resubmitted.
- Boundary: distinguish pending/running/completed. Cancel only requests that have not started. Array destruction also requires confirmed completion; never destroy twice after timeout.

## F07: Lost inventory binding reset the displayed multiplier and intent (2026-09-30)

- Observation: selling did not multiply income; readback was 1. Explicitly setting 2 restored transaction behavior. Returning a neutral value for an invalid inventory object conflated an unavailable binding with an actual OFF state.
- Correction: [GameInventoryHooks](../../Witcher3Modifier/GameInventoryHooks.cs) Rebind and [MainForm](../../Witcher3Modifier/MainForm.cs) settings restoration separate live objects from saved intent.
- Boundary: recheck PID, start time and player/inventory identity. Cache invalidation must not persist a player-requested OFF. Startup restoration and manual tests may race; record ordering first.

## F08: Blocking inventory removal did not preserve potion charges (2026-09-29–30)

- Observation: potion charges decreased with preservation enabled. Alchemy charges use `SingletonItemRemoveAmmo` and `ammo_current`.
- Correction: [FindAmmoFunction](../../Witcher3Modifier/GameItemScheduler.cs) validates and intercepts that script path; the player confirmed potion behavior.
- Boundary: shared bomb code is source evidence, not gameplay verification of every bomb type. Trace charge consumption on new versions instead of checking only stack quantities.

## F09: Preserving default-bolt cleanup created extra bolts (2026-10-01)

- Observation: each default-bolt shot added another. `AddAndEquipInfiniteBolt` removes the old bolt before replenishing it; preservation blocked that cleanup.
- Correction: [GameInventoryHooks](../../Witcher3Modifier/GameInventoryHooks.cs) permits internal Bodkin/Harpoon deletion with IDs resolved from the current name pool. The player confirmed accumulation stopped.
- Boundary: finite special bolts retain protection. Explicit deletion uses its dedicated original-entry path instead of temporarily disabling all preservation. Internal head/hair cleanup requires a similar semantic exemption.

## F10: Null attitude did not query all entities (2026-10-02)

- Observation: normal containers and herbs were missing; neutral/hostile flags still selected Actor storage.
- Correction: [BuildLootQueryCode](../../Witcher3Modifier/GameItemScheduler.AutoLoot.cs) zeros two flags in a checked private bytecode copy while preserving the original script.
- Boundary: establish offsets and patterns from target bytecode; enlarging the range is not a fix. Theft, quest, lock and special-container guards remain necessary.

## F11: Readiness failures persisted auto-loot OFF (2026-10-04)

- Observation: initial object traversal and FunTarget during returned-array cleanup entered the fatal path and saved OFF.
- Correction: [IsAutoLootWait](../../Witcher3Modifier/GameItemScheduler.AutoLoot.cs) classifies only known scene/busy/timeout conditions, retaining intent and cleanup ownership. Covered by `autoloot-recovery-offline`.
- Boundary: unknown code, type or read failures remain errors. Prior container/herb transfers are recorded; the latest recovery has not been verified across all loading situations.

## F12: Shutdown waited for a game that had exited (2026-10-04)

- Observation: repeated UserClosing events were followed by no-game movement errors; Cancel remained set.
- Correction: [MainForm.OnFormClosing](../../Witcher3Modifier/MainForm.cs) captures process presence before event dispatch; busy guards allow closure after game exit. Completed movement cleanup is not resubmitted.
- Boundary: both language test windows passed with all busy guards set; the deployed Chinese program closed/reopened successfully. Live-game cleanup was not retested.

## F13: Bypassing equip checks did not independently edit persistent required level (2026-09-30–10-02)

- Observation: `HasRequiredLevelToEquipItem` controls eligibility only. The `level` property, instance seed, generation range and template override did not establish an independent required-level edit.
- Correction: [EquipmentNumbers](../../Witcher3Modifier/GameEquipmentNumbers.cs) computes attainable values through native abilities and base attributes; [EquipmentLevel](../../Witcher3Modifier/GameItemScheduler.EquipmentLevel.cs) manages eligibility separately.
- Boundary: fixed base values cannot be reduced by removing nonexistent bonuses; seed may affect multiple modifiers. Read all fields after all changes, and obtain separate save/reload evidence without the trainer. New serialization/consumer evidence can reopen independent-level research.

## F14: Kill/quest multipliers were assumed to cover all XP (2026-09-29)

- Observation: `expGlobalMod_kills/quests` affect specific upstream reward sources. Directly increasing total XP bypasses level, skill-point and HUD settlement.
- Correction: [GameExperience](../../Witcher3Modifier/GameExperience.cs) intercepts the metadata- and byte-validated AddPoints path.
- Boundary: upstream settings can satisfy narrower requirements. All-source claims need coverage evidence; reading a multiplier does not verify every reward source.

## F15: Return values, writes and self-tests were treated as final effects

- Observed boundaries: a void appearance call cannot be judged by the return buffer's first byte; mounted NameId readback does not prove a complete face; arbitrary SetItemModifierFloat keys need not feed attribute computation; local machine-code tests do not prove target metadata.
- Checks: [Fun](../../Witcher3Modifier/GameItemScheduler.Fun.cs), [Equipment](../../Witcher3Modifier/GameEquipment.cs), [PROJECT_STATE](PROJECT_STATE.md).
- Acceptance: record execution, identity/field readback, gameplay effect, save/reload and stability separately. A graphics-module fault alone cannot exonerate the trainer. Historical crashes without established causes remain unresolved.

## F16: Wrong template extraction, drawing layout and global hotkey side effects

- Match complete field declarations: `Template` can accidentally select `RemoveJobTemplate` or a Legacy template. Check each test process's exit code.
- [TrainerTheme](../../Witcher3Modifier/TrainerTheme.cs) guards zero-size drawing. [Layout](../../Witcher3Modifier/MainForm.Theme.cs) needs control bounds and actual rendering checks; smaller fonts do not fix incorrect row heights.
- [Shortcuts](../../Witcher3Modifier/MainForm.Shortcuts.cs) register only while the game is foreground and release through the master switch. Hidden-page dispatch cannot depend on Button.PerformClick selectability. Offline checks must not play sounds.
- Revisit with a focused reproducer when changing those templates, layouts or registration. Breaking numeric input in other applications is a defect, not an acceptable hotkey cost.

## Maintaining this ledger

Keep identifiers stable. Append explicit corrections when new evidence changes an entry and link dated investigations from current state. Do not generalize limited checks to all scenarios. New registration, ABI, lifecycle or behavior evidence may justify revisiting an older contract on a new game version.
