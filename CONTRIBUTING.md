# Contributing / 贡献

Start with [development](docs/DEVELOPMENT.md) and the [AI guide](docs/ai/AGENT_GUIDE.md). Keep changes limited to the reported feature. Record the game executable hash, renderer and evidence level for runtime claims.

Build the Chinese and English variants. Run applicable offline probes. A build or memory readback is not gameplay verification or save persistence. Runtime tests require a saved game and the player's explicit permission. Add a reproducible issue with version, action, expected result, actual result and a redacted log excerpt; exclude full saves, game binaries, credentials and personal paths.

Chinese source is canonical. Update `localization/en.json` for new strings and any corresponding data translation in `scripts/localize.py`. Keep native IDs and addresses intact across localization. Document new migration evidence in an append-only research note, then update `docs/ai/PROJECT_STATE.md`.

从开发教程和AI指南开始；改动限定在问题范围。中英文都构建，运行相关离线检查。实机测试须经玩家授权并保存进度。问题报告包含版本、步骤、预期、实际与脱敏日志。新增文案同步翻译表，翻译不能改动游戏ID和地址。历史研究记录追加保存，当前状态引用历史证据。
