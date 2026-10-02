# Contributing

[简体中文](CONTRIBUTING.zh-CN.md)

Start with [development](docs/DEVELOPMENT.md) and the [AI guide](docs/ai/AGENT_GUIDE.md). Keep changes limited to the reported feature. Record the game executable hash, renderer and evidence level for runtime claims.

Build the Chinese and English variants. Run applicable offline probes. A build or memory readback is not gameplay verification or save persistence. Runtime tests require a saved game and the player's explicit permission. Add a reproducible issue with version, action, expected result, actual result and a redacted log excerpt; exclude full saves, game binaries, credentials and personal paths.

Chinese source is canonical. Update `localization/en.json` for new strings and any corresponding data translation in `scripts/localize.py`. Keep native IDs and addresses intact across localization. Document new migration evidence in an append-only research note, then update `docs/ai/PROJECT_STATE.md`.
