# 功能、地址与数据路线图

[English](TECHNICAL_MAP.md) · [冷启动](COLD_START.zh-CN.md) · [迁移教程](VERSION_MIGRATION.zh-CN.md)

代码基线 v0.1.7；清单从当前公开源码生成，不需要原作者的 E 盘路径。功能入口和验证职责如下，UI 开关不等于游戏效果。

## 源码导航

所有下列名称位于 [Witcher3Modifier](../../Witcher3Modifier)。

| 功能/层 | 文件与关键入口 | 更新时核对什么 |
|---|---|---|
| 窗口、设置、声音、快捷键 | MainForm.cs、MainForm.*、Draft.cs、ToggleSounds.cs | 恢复意图、忙碌状态、前台注册、关闭与图形生命周期 |
| 金币与共同对象链 | GameMoney.cs / WithInventory、Set | 模块指纹、游戏/玩家/库存虚表、引用、实例条目、金币名称 |
| 金币倍率、数量保护、内部清理 | GameInventoryHooks.cs / Set、Read、Rebind | add/remove原始指令、trampoline、配置页、默认弩箭/外观名称ID |
| 经验倍率 | GameExperience.cs | AddPoints元数据、参数解析后入口、寄存器/栈、正向增量及结算 |
| 调度、添加、删除、耐久/槽位 | GameItemScheduler.cs / Execute、Give、ExecuteEquipmentNative | IsFocusModeActive触发、CFunction、页标记、串行槽、返回数组析构、各原生参数 |
| 库存与多选删除 | GameInventory.cs、MainForm.Inventory.cs、MainForm.ItemBatch.cs | 实例ID与名称/库存身份、隐藏选择、部分完成与未知结果 |
| 装备能力与数值/等级 | GameEquipment.cs、GameEquipmentNumbers.cs、GameEquipmentNumbers.Reduction.cs、GameEquipmentLevels.cs | 类别、能力定义、基数/增量、可移除组合、最终统一回读；等级是派生结果 |
| 穿戴等级检查 | GameItemScheduler.EquipmentLevel.cs | HasRequiredLevelToEquipItem字节码、当前角色绑定，不改独立等级 |
| 活力、马匹、弹药、落地保护 | GameDebugFacts.cs、GameItemScheduler.cs / ValidateDebugFacts | 事实键与add/remove注册入口、调用前缀、原生参数 |
| 生命、呼吸、毒性、击杀、耐久 | GameItemScheduler.Auxiliary.cs | 对应脚本/原生元数据、角色引用、请求及返回契约 |
| 制作无需材料 | GameItemScheduler.Crafting.cs | 函数分派、原流程返回、材料消费与恢复，不等于仅阻止扣数量 |
| 肾上腺素 | GameItemScheduler.Adrenaline.cs | 属性/入口、补满与消费两个路径 |
| 无视负重 | GameWeight.cs | Accessibility/WeightlessItems配置层、SetConfigValue；现实现含远程线程，不能与调度器线程契约混用 |
| 地图标记传送 | GameTeleport.cs、GameTeleport.Code.cs、GameTeleport.StageCode.cs | 世界、地图区域/标记、导航、表面碰撞、请求与坐标确认 |
| 拾取/采集 | GameItemScheduler.AutoLoot.cs、GameItemScheduler.ActorScript.cs | 通用实体查询、资格、私有字节码、返回数组寿命、等待与fatal分类 |
| 移动、跳跃、游泳、敌人速度 | GameItemScheduler.PlayerMotion.cs、GameItemScheduler.SwimSpeed.cs、GameItemScheduler.EnemySpeed.cs | 当前角色/来源ID、动画参数、清理/恢复；动画倍率不保证位移线性 |
| 昆特牌、洗点、外观、天气、猫眼 | GameItemScheduler.Gwent.cs、GameItemScheduler.Respec.cs、GameItemScheduler.Fun.cs | GUI/脚本类型、完整VM调用、区域资源、头部装配与返回值语义 |

## 地址清单及局限

[address-inventory.json](address-inventory.json) 包含：

- `profile_selector_sha256`：代码里的5.00c指纹；不是程序成品哈希。
- `profile_mappings`：GameVersion内全部参考RVA→5.00c RVA；数值相同也必须在新版本验证。
- `literal_sites`：GameVersion.Rva、ExecuteEquipmentNative和FunCheck传入字面量的位置及上下文。
- `direct_module_literals`：直接与module/baseAddress相加的十六进制候选，可能绕过映射；必须人工判定用途。
- `source_sha256`：清单来源文件指纹，按无BOM的UTF-8、LF换行文本计算，避免Git换行转换误报，用来检查索引是否过期。

```powershell
python scripts/check_handoff.py
python scripts/check_handoff.py --update-address-inventory
```

第一条只读检查相对链接、双语入口与清单一致性；第二条离线更新清单后再检查，不连接游戏。源码修改导致清单变化时，与迁移证据一起审核后提交。清单是词法索引，不能解析所有变量传参、模板中的机器码立即数、RIP相对目标或虚表间接调用；它不验证目标exe。

## 先定位共同根，再迁移叶子

1. GameMoney.WithInventory：先从GamePointer解析游戏；玩家经handle+8，库存同样经handle。每层需当前类型与身份。`0xFD60`、`0x1B0`、`0x140/0x148`等是字段偏移，不是RVA，仍可能随版本变化。
2. ResolveName：现实现以FNV类名称hash、池桶和节点遍历解析CName。池布局、hash契约与名称字符串共同核对；NameId不得跨进程缓存。金币当前常量51313也是版本假设，不能仅迁移函数。
3. 注册/反射：FindTick、FindAmmoFunction、FindAuxiliaryFunction、LootGlobalFunction按名称/所属类/标志找函数；RespecProperty/RespecFindField/RespecField按元数据取属性位置。元数据自身的虚表与字段偏移也需要迁移。
4. 入口与ABI：GameVersion只给RVA选择，调用前的prefix/type checks在各feature实现中。长hex模板还包含指令、旧返回目标、可替换标记、类型参数，不能逐个“看起来像地址”替换。
5. 请求与所有权：RequestLock串行化，桥接有pending/running/completion与临时数组；先检查FindPage和标记的实际代码一致性，再复用页。原生数组使用游戏分配器/析构契约，不能把VirtualAllocEx缓冲交给游戏析构。

未知hash现走参考分支，这是代码事实，不是已支持任意版本。适配新版本要更新选择逻辑、每个受影响映射、原字节及结构契约；不能只加一个hash或一个全局delta。调度入口属于具体指令位置，并非所有RVA都是可直接调用的函数入口。

## 数据文件与重新生成

| 资产 | 当前作用 | 新版本维护 |
|---|---|---|
| ItemCatalog.json | 搜索标签、内部物品名、类型 | 核对本体/DLC定义与当前名称池；同名不等于已载入 |
| EquipmentLevels.json | 搜索参考等级 | 实例等级可能随生成/属性变化；不能把参考等级当实际实例 |
| Research/equipment_presets.json | 已整理的能力/附魔及数值候选 | 核对类别、单位、真实能力效果和固定下限 |
| localization/en.json / scripts/localize.py | 中文源码生成英文 | 保持原生名称、hex、机器码和资源身份；运行verify_localization |
| DesignPreview/assets/wolf-school-head-repaired.png | 嵌入图像 | 见NOTICE权利说明，不能把图像权利等同源码MIT |

原始全量资源扫描/候选生成脚本未全部公开。现有JSON足够构建；更新目录或能力候选需要重新分析自有游戏资源，记录本体与DLC覆盖、解析失败与恢复策略。曾有XML整文件因局部重复属性解析失败而漏掉能力；逐片段恢复仍需统计成功/失败，不能把空结果当不存在。

## 对未来功能的检查顺序

查本页的共同依赖→查失败编号→查目标脚本/注册及真实消费路径→建立离线契约→按当前授权只读→独立写入→效果→必要的保存读档。中间某层失败就记录断点，不能跳过失败层扩大写入范围。
