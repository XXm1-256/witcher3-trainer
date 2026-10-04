# AI Agent operating guide

[简体中文](AGENT_GUIDE.zh-CN.md)

Start by reading README, DEVELOPMENT, PROJECT_STATE and VERSION_MIGRATION. Use screenshots, logs and extracted material to investigate behavior. Confirm the current task's permission before modifying a running game.

## Before changing code

- Identify the exact requested change, current source commit, game executable/profile and validation boundary before editing.
- Keep changes small and directly related. Preserve user changes, settings and historical evidence. Read code before choosing an approach.
- Prefer registered game functions, reflection metadata and existing validated trainer flows. A displayed value may be transformed, derived or encoded; do not assume searching for its decimal representation locates its owner.
- Keep player instructions about effect, use and practical limitations; put engineering diagnostics in logs/docs.
- Use external runtime operations; this project's normal build does not change game files. Do not add a new execution mechanism without a concrete need.
- Runtime testing needs explicit player permission and a saved game. Read-only verification, a build, an effect observed in game and save/reload persistence are different evidence levels. Never claim one from another.
- A queued operation during pause may complete later. Do not resubmit a mutation or free a result twice. Audit pending/running/completed flags and resource ownership first.
- Log real validation failures. For known scene-readiness conditions, wait without changing the requested switch preference. Do not suppress unknown pointer/type/code errors.
- Localization must preserve native IDs, code bytes, offsets and request semantics. Check both builds; English item labels currently use internal English IDs.

## Starting from an existing checkout

1. Read current state and linked notes; inspect `git status`, recent commits and relevant files. Verify files exist and recorded hashes belong to the current artifact.
2. Reconstruct the latest accepted requirements and which operations are authorized. Do not treat an old test permission as continuing authorization.
3. Record a short plan with success criteria. Perform independent offline work while runtime evidence is unavailable.
4. Reproduce the reported failure using logs or a minimal offline test. Inspect existing game/code metadata before guessing addresses.
5. Make the smallest change, run relevant checks and read back the actual diff. Document what is saved, built, checked, observed and still pending separately.
6. Before release, check language output, resource inputs, privacy and license notices. Preserve the player's latest draft at replacement time and reopen the requested executable.

## Document an investigation

Add dated notes instead of overwriting old investigations. Each note contains: parent source/version, user-visible symptom, evidence paths/hashes, change, actual checks, failed alternatives and the specific reason, applicability/reopening evidence, artifact hash and next step. Keep the current state concise and linked to history.

Public evidence must be sanitized. Do not commit live heap addresses as reusable configuration, personal logs/settings, full crash dumps, credentials, game executables or raw game script trees. A local complete archive may retain diagnostic evidence separately; a public source repository is a curated export, not a replacement for the owner's full private archive.

## Learning tasks

Trace gold editing from MainForm to GameMoney. Trace a native scheduler request through completion and timeout. Trace inventory instance identity into equipment validation. Explain auto-loot's private query flags, general entity storage, eligibility filters and deferred destructor. Then propose a focused offline regression before attempting new runtime work.

## Release changelog

For each update, append a dated version entry to CHANGELOG.md and CHANGELOG.zh-CN.md. Keep both descriptions aligned, preserve previous entries, and summarize the same changes in the GitHub release body. State pending gameplay verification accurately.
