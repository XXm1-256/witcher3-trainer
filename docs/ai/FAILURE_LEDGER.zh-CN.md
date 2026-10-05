# 历史失败与维护边界

[English](FAILURE_LEDGER.md) · [冷启动](COLD_START.zh-CN.md)

更新于 2026-10-05，代码基线 v0.1.7。以下是私人工程记录中已确认现象的脱敏技术摘要；原始转储、完整日志和游戏脚本不在公开仓库。源码链接指向当前防护或实现，不能代替历史原件。无法完整还原的原因和待验收结果保留为开放项；这里不是永久禁止研究的清单。

## F01：错误的绝对跳转位移导致安装崩溃（2026-09-29）

- 现象/依据：首版库存入口 hook 安装后游戏访问冲突，安装校验失败。`FF 25` 的四字节 RIP 位移误填 `90 90 90 90`。
- 修正：`FF 25 00 00 00 00` 后接八字节目标；核对完整覆盖指令和返回跳板，在恢复线程之前校验，失败回滚。
- 当前入口：[GameInventoryHooks](../../Witcher3Modifier/GameInventoryHooks.cs)。重新改写时需要本地执行桩、寄存器/栈契约以及目标原字节核对；只通过构建不足以放行。

## F02：复制附近地址作为 FactsAdd 入口（2026-10-02）

- 现象/依据：开启马匹耐力后崩溃；5.00c 候选 `0x1F58600` 位于函数内部，包含错误返回路径。注册与函数边界定位实际入口为 `0x1F58700`。WER 有故障现场，没有完整转储栈，不能声称完整因果链已还原。
- 修正：[GameVersion](../../Witcher3Modifier/GameVersion.cs) 的该项映射，以及 [ValidateDebugFacts](../../Witcher3Modifier/GameItemScheduler.cs) 的 add/remove 前缀检查。
- 边界：开启/独立读取/关闭/独立读取通过，不等于骑乘消耗的视觉验收。新版本重新追注册、函数边界和参数，不复制旧候选。

## F03：把玩家组件当作游戏世界（2026-09-30）

- 现象/依据：导航查询崩溃。`player+0xF0` 引用的是组件；错误对象传给导航查询。真实 RTTI 和较早异常上下文支持此实现错误；最终图形驱动故障不能推翻这项证据。
- 修正：[GameTeleport](../../Witcher3Modifier/GameTeleport.cs) 从 `game+0xF0` 的 handle 解析世界，校验 CGameWorld、格/表边界，再查询。
- 重开条件：目标版本真实 RTTI 与对象来源、导航结构只读核对，再按许可预览及移动。假对象 ABI 测试不能证明实际对象类型。

## F04：短 native frame 被当作完整脚本 VM frame（2026-10-01）

- 现象/依据：脚本清理读取 frame 的临时参数表时崩溃。80 字节 native frame 不满足完整解释器 frame 的清理契约，参数区重叠。
- 修正：撤下该调用方式；完整脚本调用参考 [Respec](../../Witcher3Modifier/GameItemScheduler.Respec.cs) 与 [ActorScript](../../Witcher3Modifier/GameItemScheduler.ActorScript.cs)，独立参数、输出、frame 与清理所有权。
- 重开条件：目标 ABI、frame 大小、临时对象清理、外部返回指针的独立验证。不要把解释器局部参数指针当成外部返回缓冲；这项失败不否定已经核对的原生包装调用。

## F05：存在 OnTick 元数据就假定它持续触发（2026-09-30）

- 现象/依据：游玩时旧 OnTick 请求触发计数为零，IsFocusModeActive 有持续命中。
- 修正：[FindTick](../../Witcher3Modifier/GameItemScheduler.cs) 按当前实际触发函数元数据核对。调度不在任意外部线程直接调用游戏对象。
- 边界：新版本重新验证真实调用频率与游戏线程；有名称和注册并不等于此场景会执行，暂停时仍可延迟。

## F06：把超时当作“没有添加”，再发一次（2026-09-30）

- 现象/依据：银锭显示失败但已经增加，后台请求后来完成。
- 修正：[Give/Execute](../../Witcher3Modifier/GameItemScheduler.cs) 与 [ItemBatch](../../Witcher3Modifier/MainForm.ItemBatch.cs) 保留同一请求，按库存差确认；完成或未确认项目不自动重发。
- 边界：区分 pending/running/completed；取消只针对尚未开始的请求。返回数组析构也需完成确认，不得超时后重复析构。

## F07：读档失去绑定后回退成倍率 1 并覆盖意图（2026-09-30）

- 现象/依据：出售没有放大，游戏回读倍率为 1；明确设置 2 后交易生效。旧库存对象不匹配时 Read 返回中性值，混淆未绑定与真实关闭。
- 修正：[GameInventoryHooks](../../Witcher3Modifier/GameInventoryHooks.cs) Rebind 与 [MainForm](../../Witcher3Modifier/MainForm.cs) 的设置恢复分别维护当前对象与保存意图。
- 边界：PID、启动时间、玩家/库存身份重新核对；不能把缓存失效写回为玩家关闭。启动自动恢复与手动测试可能竞争，先记录请求顺序。

## F08：只阻止库存移除，药水仍扣次数（2026-09-29～30）

- 现象/依据：物品保护开启时药水仍减少，炼金次数使用 `SingletonItemRemoveAmmo` 与 `ammo_current`。
- 修正：[FindAmmoFunction](../../Witcher3Modifier/GameItemScheduler.cs) 验证并拦截对应脚本路径；药水效果经玩家确认。
- 边界：炸弹共用路径是源码依据，不代替每类炸弹实测。新版本跟踪真实次数路径，不能只检查普通数量条目。

## F09：保护默认弩箭内部清理，反而越射越多（2026-10-01）

- 现象/依据：默认箭每射一次增加一支。游戏的 `AddAndEquipInfiniteBolt` 先移除再补回默认箭，保护挡住了移除。
- 修正：[GameInventoryHooks](../../Witcher3Modifier/GameInventoryHooks.cs) 放行 Bodkin/Harpoon 默认箭内部删除，按当前名称池绑定；玩家确认不再增加。
- 边界：有限特殊箭保留数量保护；显式删除走专用原始入口，不临时关闭全局保护。外观头部/发型内部清理也需要同类语义放行。

## F10：null 阵营不等于查询所有实体（2026-10-02）

- 现象/依据：普通箱子和草药未被查询到；两个中立/敌对参数仍让游戏选择 Actor 存储。
- 修正：[BuildLootQueryCode](../../Witcher3Modifier/GameItemScheduler.AutoLoot.cs) 在私有字节码副本清零两个标志，检查预期模式，原脚本不变。
- 边界：修改偏移与模式需目标字节码证明，不通过扩大范围凑成功。守卫敏感、任务、上锁及特殊容器资格保护仍有效。

## F11：场景未就绪被保存成自动拾取关闭（2026-10-04）

- 现象/依据：初始对象遍历及 returned-array 清理中的 FunTarget 未就绪进入 fatal，开关被保存为 OFF。
- 修正：[IsAutoLootWait](../../Witcher3Modifier/GameItemScheduler.AutoLoot.cs) 只分类已知场景/忙碌/超时，保留意图和清理所有权；对应 `autoloot-recovery-offline` 回归。
- 边界：未知代码、类型或读取故障继续失败。容器与草药转移有既有记录，最新所有读档场景恢复仍未穷尽实测。

## F12：关闭窗口等待已经退出的游戏（2026-10-04）

- 现象/依据：连续 UserClosing 后移动恢复报未找到游戏，Cancel 不解除。
- 修正：[MainForm.OnFormClosing](../../Witcher3Modifier/MainForm.cs) 在事件分派前记录游戏是否存在；所有游戏忙碌保护在游戏消失后放行。运动清理完成后不再重发。
- 边界：中英文全忙碌实际测试窗口、中文正式程序关闭/重开通过；游戏仍运行时的清理未重新实测。

## F13：无视穿戴检查不等于永久修改需要等级（2026-09-30～10-02）

- 现象/依据：`HasRequiredLevelToEquipItem` 只控制穿戴检查；`level`、实例 seed、生成范围、模板覆盖等候选不能证明独立的需要等级编辑。
- 修正：[EquipmentNumbers](../../Witcher3Modifier/GameEquipmentNumbers.cs) 按原生能力组合与基础属性计算可达值；[EquipmentLevel](../../Witcher3Modifier/GameItemScheduler.EquipmentLevel.cs) 单独管理检查开关。
- 边界：固定基础下限不能通过移除不存在的增量降低；seed可能同时改变多个词条。所有变更后统一回读；关闭程序后的保存/读档证据单独取得。未来有新实例序列化/消费路径证据可以继续研究独立等级。

## F14：仅用击杀/任务全局倍率覆盖所有经验（2026-09-29）

- 现象/依据：`expGlobalMod_kills/quests` 是上游特定奖励来源，直接补写经验总值会绕过等级、技能点和 HUD 流程。
- 修正：[GameExperience](../../Witcher3Modifier/GameExperience.cs) 接入经元数据和字节校验的 AddPoints 路径。
- 边界：需求限定某来源时可以用上游设置；全来源承诺需要实际覆盖证据，回读倍率不证明每个奖励来源。

## F15：原生返回值、属性写入和自测成功被当作最终效果

- 已遇到的边界：void 外观调用不能读返回缓冲首字节判断成功；装配 NameId回读不证明脸部画面完整；SetItemModifierFloat任意键不保证属性计算会读取；独立机器码测试不证明目标元数据正确。
- 当前检查：[Fun](../../Witcher3Modifier/GameItemScheduler.Fun.cs)、[Equipment](../../Witcher3Modifier/GameEquipment.cs) 与 [PROJECT_STATE](PROJECT_STATE.zh-CN.md)。
- 实际验收：完成状态、身份/字段回读、玩家效果、保存/读档、稳定性观察逐层记录，不能互相替代。图形模块上的故障也不能单独证明修改器无关；历史根因未明的崩溃保持未明。

## F16：测试提取错模板、图形布局与全局快捷键副作用

- 精确匹配完整字段声明，防止 `Template` 误抓 `RemoveJobTemplate` 或 Legacy 模板；每个测试进程分别看退出码。
- [TrainerTheme](../../Witcher3Modifier/TrainerTheme.cs) 绘制前检查零尺寸；[布局](../../Witcher3Modifier/MainForm.Theme.cs)需控件与实际渲染核对，缩小字号不能修复错误行高。
- [Shortcuts](../../Witcher3Modifier/MainForm.Shortcuts.cs) 仅游戏前台注册，关闭总开关释放；隐藏页不能依赖 Button.PerformClick 的可选择条件。离线测试不试听音效。
- 条件：改对应模板、布局或注册流程时加针对性复现。其他软件不能正常输入数字是缺陷，不能作为全局快捷键的正常代价。

2026-10-05干净副本审查还发现：源码逐字节哈希会因Git换行转换误报索引过期，现按无BOM的UTF-8/LF文本计算。生成英文UiProbe --quiet含中文文案断言假设，应使用指南中的EnglishProbe入口，不能把这类失败当作正式功能缺陷。

## 更新此表

保留编号。新证据给旧记录追加明确纠正，当前状态链接日期调查；不要把有限验证扩成全部场景已解决。新版本如改变旧契约，可凭注册、ABI、对象寿命或实际行为证据重新评估。
