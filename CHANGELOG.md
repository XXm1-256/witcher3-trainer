# Release notes

[简体中文](CHANGELOG.zh-CN.md)

## 0.1.6 — 2026-10-04

- Auto-loot now keeps its switch enabled while Geralt is temporarily unavailable during loading or a scene transition, then waits for gameplay to resume.
- Adds regression coverage for this readiness message. Code, type and data validation failures still stop the feature and write a log.

Both language builds and offline readiness checks passed. Recovery across actual save loads still needs player feedback.

## 0.1.5 — 2026-10-04

- Queue items with individual quantities, submit a whole list, inspect per-item results and stop after the current entry. Completed and unconfirmed entries are not resubmitted.
- Filter inventory by item type together with names and levels. Multi-select deletion affects only visible selected entries.
- Add hairstyle and fixed beard options, clarify both stubble stages, allow internal appearance cleanup and verify mounted head entries.
- Keep auto-loot enabled while initial player/inventory objects are temporarily unreadable during loading.

Target: The Witcher 3 5.00c DX12 on Windows x64. Both language builds and offline queue/type checks passed. Face rendering, appearance save/reload, the latest auto-loot load recovery and actual item batches still need player feedback. See [compatibility notes](docs/ai/PROJECT_STATE.md).
