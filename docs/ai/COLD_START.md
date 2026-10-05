# Agent cold start and maintenance entry

[简体中文](COLD_START.zh-CN.md)

For maintainers who have this repository but none of the original conversations or private research directories. The runtime baseline is v0.1.7; this guide was updated on 2026-10-05. Establish a reproducible source baseline before investigating one feature. New game versions still require the target binary and runtime evidence.

## 1. Shortest reading route

1. This page: environment, executable checks and the next handoff.
2. [PROJECT_STATE](PROJECT_STATE.md): supported fingerprint and open verification.
3. [TECHNICAL_MAP](TECHNICAL_MAP.md): features, code, addresses and resources.
4. [FAILURE_LEDGER](FAILURE_LEDGER.md): observed failures, correction boundaries and evidence required to revisit them.
5. Read [VERSION_MIGRATION](VERSION_MIGRATION.md) before runtime changes; routine rules are in [AGENT_GUIDE](AGENT_GUIDE.md).

Read `git status --short` and `git log -5 --oneline` first. Distinguish a release executable, the latest main source and an older local executable.

## 2. Environment and offline baseline

Use Windows x64, Git and .NET 8 SDK. English generation and documentation checks require Python 3.10+. The three JSON inputs and image needed to build are included. Building requires no game; proving compatibility with a new version requires that game.

Run these individually from the repository root and check every exit code. PowerShell does not automatically stop subsequent commands when a native executable returns nonzero.

```powershell
git clone https://github.com/XXm1-256/witcher3-trainer.git
Set-Location witcher3-trainer
git status --short
git rev-parse HEAD
dotnet --version
python --version
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Language zh-CN
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Language en
python scripts/verify_localization.py
python scripts/check_handoff.py
dotnet run --project tools/HookProbe/HookProbe.csproj -c Release -- version-selftest
dotnet run --project tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --quiet
```

Outputs are `dist/zh-CN/Witcher3Modifier.exe` and `dist/en/Witcher3Modifier.exe`. Do not launch the production program as an offline check: it may restore saved switches and connect to the game. UiProbe uses an isolated draft beside its own output; `--quiet` performs no game actions or sound playback.

For shutdown verification, close the game first, then run:

```powershell
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --close-without-game
dotnet run --project .build/en/tools/UiProbe/UiProbe.csproj -c Release -- --close-without-game
```

These briefly show a test window. `--item-batch` uses a fake executor to verify partial completion, stopping and avoiding duplicate submissions; it also draws a test UI. Do not infer other probes' safety from their names: `gear-numeric-probe` creates equipment, `autoloot-query` can install scheduling code and execute queries even without transferring items, and `swim-neutral-check` can write an animation source. Read the [HookProbe branch](../../tools/HookProbe/Program.cs) before execution.

English UI checks use `EnglishProbe`, which inspects generated text and renders the pages offline:

```powershell
dotnet run --project .build/en/tools/EnglishProbe/EnglishProbe.csproj -c Release
dotnet run --project .build/en/tools/HookProbe/HookProbe.csproj -c Release -- version-selftest
dotnet run --project .build/en/tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
```

Run UI probes when a brief test window or rendering will not interrupt the player. The full `UiProbe --quiet` assertions currently target Chinese copy; running that mode on generated English source fails on translated hint/preview expectations and is not the English acceptance route. This is a test-language limitation, not evidence that gameplay is broken.

## 3. Choose a route for the task

| Situation | First evidence | Completion condition |
|---|---|---|
| Build or localization failure | SDK, embedded inputs, canonical source and en.json | Both builds and native-data invariants pass |
| Feature fails after an update | Executable fingerprint, GameVersion, function/type/layout | Independent migration evidence for each affected entry, followed by authorized runtime testing |
| Load recovery, duplicated items or indefinite waiting | Logged process identity, request state, requested switches and array ownership | Separate pending/running/completed states; never replay uncertain results |
| Game crash | Last operation, installed entries, WER/dump, actual fault instruction | Tie the cause to evidence; retain unknown causes as unknown |
| Unexpected equipment values | Instance identity, ability combinations, fixed base, final combined readback | Exact or attainable values explained; save/reload checked when persistence is required |
| UI, shutdown or hotkey issue | Window events, saved state, foreground registration, relevant UiProbe | Reproducible interaction check passes while preserving settings |

## 4. Assets needed for a new game version

Obtain a legally installed executable, renderer/platform/version details and a separate save backup. Record SHA256, size and PE architecture. Use a disassembler capable of Windows x64 PE, registration cross-references and RTTI analysis. This repository does not require one particular disassembler and provides no automatic updater for unknown versions.

```powershell
$gameExe = 'D:\Games\The Witcher 3\bin\x64_dx12\witcher3.exe'
Get-FileHash -LiteralPath $gameExe -Algorithm SHA256
(Get-Item -LiteralPath $gameExe).Length
```

Replace the example with the actual installation path. Start migration with the [address inventory](address-inventory.json), then inspect original bytes, registration and type guards. The inventory indexes existing code; it is neither a scan of a new binary nor a call-ready table for unknown versions.

XML definitions in game `.bundle` files can help. The included [BundleExtract](../../tools/BundleExtract/Program.cs) only supports its checked `POTATO70` signature, 304-byte entries and compression methods 0/1; it verifies length and CRC. Use a local read-only input and a new empty output directory:

```powershell
dotnet run --project tools/BundleExtract/BundleExtract.csproj -c Release -- 'D:\Research\input.bundle' 'D:\Research\extracted-new'
```

Do not reuse an output directory containing existing work: the tool writes extracted files. Stop on unsupported layouts; investigate the parser rather than disabling CRC or applying a legacy 320-byte assumption. The repository excludes the original author's full script tree, dumps and some research scripts. Obtain definitions from the maintainer's own installation when investigating a new version. Extraction is not a build prerequisite.

## 5. Leave a resumable result

Create `docs/ai/history/YYYY-MM-DD-topic.md` and its `.zh-CN.md` counterpart for each investigation, linked from PROJECT_STATE. Create the directory when needed. Even unsuccessful investigations must leave a usable breakpoint:

```text
Objective, parent commit, current commit:
Game SHA256, platform/renderer, version:
Symptom and reproduction steps:
Affected code/method, old and new RVA, layout and registration anchors:
Validation/match count/instructions/ABI, ownership and cancellation boundaries:
Ruled-out paths, evidence, applicability, evidence needed to revisit:
Commands actually run, exit codes and evidence levels:
Unverified effect, persistence or stability:
One specific next action and required player assistance:
Artifact/input SHA256, recovery steps and related commits:
```

Commit source and sanitized conclusions. Keep personal settings, original logs, dumps, game executables and extracted script trees in the maintainer's diagnostic directory; public notes refer to sanitized summaries and fingerprints. Preserve the player's latest settings before replacing the executable. Never replay one-shot additions, deletions or teleports. Append bilingual changelogs while retaining previous entries.

## What this guide establishes

Documentation and offline checks establish that the repository can be read and built and that known failure boundaries are accessible. They cannot replace a maintainer's capabilities, game ownership, analysis tools, current runtime authorization or new-version validation. Leave unproven steps as open work.
