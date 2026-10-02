# Developer tutorial

## Build a reproducible baseline

Use Windows x64, .NET 8 SDK, and Python 3.10+ for English. Clone the repository; run `scripts/build.ps1 -Language zh-CN`, then `-Language en`. All required embedded inputs are included: item/level JSON, equipment presets and the wolf image. Game extraction is not a source-build prerequisite. Players receive self-contained executables; source developers need the SDK.

English source is generated in `.build/en`, from Chinese canonical source plus a reviewed literal map. Game identifiers, code bytes and addresses remain unchanged. Catalog display names use internal English identifiers. Preset effect names/descriptions are supplied explicitly in the generator. Edit canonical source/mapping rather than generated files.

## Reading order and learning exercises

1. `Program.cs`, `MainForm.cs`, `MainForm.*.cs`: learn controls, event handlers, UI busy/refresh guards, saved `Draft`, tooltips and shortcut handling. Follow the gold button from event to backend without running the game.
2. `GameMoney.cs`, `GameVersion.cs`: learn executable identification, module-relative addressing, process handles, reads/writes, object-vtable checks and the selected profile. Compute `VA = module base + RVA` on paper; do not confuse it with a heap object address.
3. `GameInventory.cs`, `GameItem.cs`, item/level JSON: learn catalog identifiers versus live inventory instances; create a small offline catalog search test.
4. `GameItemScheduler.cs` and its partial files: learn serialized requests, validated function metadata, queued execution, return values, cancellation and ownership. Trace a pause timeout through auto-loot and deferred array cleanup.
5. `GameInventoryHooks.cs`, `GameExperience.cs`, equipment-number files: learn instruction validation and native attribute limits. Explain why a shown item quantity or displayed damage is not always the stored value.
6. `TrainerTheme.cs`, `MainForm.Theme.cs`, `ToggleSounds.cs`: learn rendering, DPI scaling and action versus actual-effect feedback.

## Offline checks

```powershell
dotnet build Witcher3Modifier/Witcher3Modifier.csproj -c Release
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --help-copy
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --quiet
dotnet run --project tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
python scripts/localize.py
python scripts/verify_localization.py
dotnet run --project .build/en/tools/EnglishProbe/EnglishProbe.csproj -c Release
dotnet run --project .build/en/tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
```

These specific modes are offline. **Other HookProbe modes can connect to and modify a running game**: read the branch before execution. Do not invoke a probe by guessing its name. EnglishProbe inspects initial control/list/tooltip text and renders five pages without gameplay actions; it does not prove all dynamic messages or every DPI/layout state. UiProbe has its own isolated draft under its output folder.

## Change one feature safely

Record expected behavior and a reproducer. Prefer the game's registered native/script flow and validated metadata over repeated value scans. Add the smallest relevant offline regression. Rebuild both variants and inspect changed UI at normal and maximized sizes. For runtime verification obtain player permission and a saved game, test one isolated operation, then check effect and persistence separately. See the migration guide before changing addresses.

Never reuse a heap pointer or a returned array in a different process. Timeout does not prove failure: queued, executing and completed requests must be distinguished. A completed array destructor cannot be retried. Retain pending ownership until confirmed cleanup, or let the originating process lifetime end; keep validation intact.

## Documentation and release

Record changes, evidence, unsuccessful approaches and applicable reopening conditions in a new dated note. Update the current state to point to it, preserving historical notes. Do not bundle user drafts/logs, crash dumps, game binaries or extracted scripts. Tag the reviewed source; release separate language ZIPs with the executable at their top level, README, LICENSE, NOTICE and checksums. Rebuild from a clean checkout before claiming reproducibility.
