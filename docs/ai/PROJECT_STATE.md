# Current project state / 当前工程状态

## Authoritative inputs

Chinese source under `Witcher3Modifier`, generated English source via `scripts/localize.py`, and three embedded metadata files plus the wolf image. Game profile: 5.00c Windows DX12 x64, SHA256 `9406ECCC12B68E08920931442EF6A57340E910D3E01F2082E88232487433FE51`. Other executable hashes need independent verification.

## Latest change: auto-loot recovery

Observed timeout during returned-array cleanup and player-unavailable errors during scene changes previously switched auto-loot off. Known readiness conditions now preserve the requested switch and retry during gameplay. Pending array ownership is retained until cleanup succeeds; a new query waits for cleanup. A process ID/start-time change discards old-process ownership. Timeout-boundary completion checks prevent resubmitting a finished/running job. Real code/type/read failures still stop and log.

Offline regression separates known waits from genuine validation failures. Prior gameplay logs show container stock reductions and one herb transfer; the newest recovery flow still needs player-driven pause/loading acceptance. Theft-sensitive, locked, quest and special-interaction containers are excluded by explicit eligibility checks, not by a blanket claim that all stealing paths were tested.

## Evidence levels and open limits

| Area | Evidence | Remaining scope |
|---|---|---|
| Chinese / English source | Release builds, offline probes | All runtime functions are not exhaustively automated |
| English UI | 560 initial controls/list/tooltip checks, five-page offline rendering | Dynamic errors and every DPI/size combination are not fully covered |
| Equipment persistence | Prior user save/reload observations for tested equipment | Native attribute floors and derived levels remain; arbitrary independent levels unsupported |
| Loot query | General-entity query bytecode invariants; observed containers/herb | New recovery, all scripted container types and complex scenes |
| Teleport | Prior successful navigation landings | All distant/multi-level terrain destinations; air arrival possible |
| Swimming / high movement / weapon swapping | Implemented, partial checks | Actual swim displacement, extreme multipliers, occasional weapon substitution root cause |

## Durable known pitfalls

- `null` attitude with neutral/hostile query flags still selected actor storage and omitted normal containers/herbs. The private wrapper zeros the two flags; do not infer that `null` alone queries all entities.
- A nearby FactsAdd candidate was inside a function and crashed. Resolve the registered entry and decode instructions before calling.
- Treating all auto-loot errors as fatal persisted an unwanted OFF state. Keep known readiness handling narrow and never bypass true validation.
- English item identifiers are practical names, not a claim of a complete official English localization extraction.

中文版摘要：当前5.00c DX12配置；最新拾取等待恢复经离线验证，实际暂停/读档恢复待玩家反馈。保留数组所有权与进程身份，清理先于新查询。部分容器与草药已有取物日志。装备保存有历史玩家验证，但原生下限和派生等级仍存在。复杂传送、高倍率运动、游泳推进和偶发武器替换尚缺完整验收。历史失败应按具体依据保留，不扩大为所有功能已修复。
