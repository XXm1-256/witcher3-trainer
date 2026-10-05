# 2026-10-05: Public maintenance handoff audit

[简体中文](2026-10-05-handoff-audit.zh-CN.md)

## Objective and baseline

Make future maintenance possible from the public repository without the original conversation or private research directory. Runtime baseline: v0.1.7, source commit `850ecf32535fe6abf9ea07cb20619b5997500d3c`, game profile SHA256 `9406ECCC12B68E08920931442EF6A57340E910D3E01F2082E88232487433FE51`. This audit changes documentation and an offline index/checker; runtime source and released executables remain unchanged.

## Findings and changes

The previous guide explained migration principles but lacked a direct cold-start route, consolidated source/address navigation and sufficiently detailed failure records. Add [COLD_START](../COLD_START.md), [TECHNICAL_MAP](../TECHNICAL_MAP.md), [FAILURE_LEDGER](../FAILURE_LEDGER.md) and [address-inventory.json](../address-inventory.json). Root AGENTS, README and existing agent/migration guides link these entries. Both languages carry the same maintenance scope.

The source inventory contains 66 mappings, 86 lexical literal sites and two direct-module candidates. It records normalized source text fingerprints. It cannot locate new addresses or exhaustively parse encoded instructions, indirect calls and field offsets.

## Checks actually performed

An independent Git clone with a clean worktree was used, without private research dependencies. Its tested documentation/tool revision was `1fbc879`; runtime files matched the v0.1.7 baseline. Environment: Windows x64, .NET SDK 8.0.424, Python 3.13.9. This is a checkout-isolation check on the existing machine, not a fresh OS or independently acting AI agent.

- Both `scripts/build.ps1 -Language zh-CN` and `-Language en` completed successfully and produced self-contained outputs.
- `scripts/verify_localization.py` passed native hex/code, item identity, level data and preset invariants.
- `HookProbe version-selftest` and `autoloot-recovery-offline` passed in both languages, without game access.
- Canonical Chinese `UiProbe --quiet` passed its isolated settings, inventory, layout, hotkey and sound-data checks.
- Generated `EnglishProbe` checked 578 initial controls/list/tooltip entries and rendered the pages offline. This does not cover every dynamic message or DPI state.
- `scripts/check_handoff.py` passed public links, paired guide/failure IDs and source-index freshness. Deliberately stale source and broken-link fixtures were rejected; files were restored and the worktree was clean.

## Audit failures retained

1. Hashing raw source bytes falsely reported stale inventory after Git newline conversion. The checker now hashes UTF-8 text without BOM and with LF newlines; a fresh checkout passed.
2. Running generated English `UiProbe --quiet` failed on translated hint assertions. A narrow hint-only adjustment then failed at a translated preview assertion; it was reverted. The supported English acceptance route is EnglishProbe, with separate translated HookProbe modes. The full quiet probe is still Chinese-copy dependent; do not mistake this test limitation for a runtime defect.

## Evidence boundaries and continuation

No game writes, active gameplay actions, runtime address migration, save/reload tests or new stability tests were performed. Historical records are sanitized summaries with current code links, not the original dumps/scripts. Binary analysis of a new installation remains necessary; all profile/address/type/ABI guards must be re-established before calls.

Next maintainer: follow COLD_START, record the exact target binary fingerprint, choose one affected feature in TECHNICAL_MAP, consult its failure IDs, then produce the migration evidence template. Append new findings and verification to bilingual historical notes and PROJECT_STATE.
