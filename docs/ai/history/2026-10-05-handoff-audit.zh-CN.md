# 2026-10-05：公开维护接档审查

[English](2026-10-05-handoff-audit.md)

## 目标与基线

让后续维护者只依赖公开仓库接手，不需要原聊天或私有研究目录。运行时代码基线为v0.1.7，源码提交`850ecf32535fe6abf9ea07cb20619b5997500d3c`，游戏配置指纹为`9406ECCC12B68E08920931442EF6A57340E910D3E01F2082E88232487433FE51`。本轮更新文档与离线索引/检查器，运行时代码和已发布exe不变。

## 发现与改动

原指南有迁移原则，但缺可直接执行的冷启动路线、集中的源码/地址导航，以及足够具体的失败记录。新增[COLD_START](../COLD_START.zh-CN.md)、[TECHNICAL_MAP](../TECHNICAL_MAP.zh-CN.md)、[FAILURE_LEDGER](../FAILURE_LEDGER.zh-CN.md)和[address-inventory.json](../address-inventory.json)。根AGENTS、README、既有Agent/迁移教程均指向这些入口，双语维护范围一致。

源码索引包括66项映射、86处字面量和2处直接module候选，并记录标准化文本指纹。它不能找出新地址，也不能穷尽解析机器码、间接调用和字段偏移。

## 实际完成的检查

使用工作区干净的独立Git副本，不依赖私有研究资产。已测试的文档/工具提交为`1fbc879`，运行时文件与v0.1.7基线一致。环境为Windows x64、.NET SDK 8.0.424、Python 3.13.9。这验证了副本隔离，不是全新操作系统或另一个独立AI的接手演练。

- `scripts/build.ps1 -Language zh-CN`和`-Language en`均成功生成自包含成品。
- `scripts/verify_localization.py`通过原生hex/机器码、物品身份、等级数据和预设不变量检查。
- 双语HookProbe的`version-selftest`与`autoloot-recovery-offline`通过，没有游戏访问。
- 中文UiProbe `--quiet`的隔离设置、背包、布局、快捷键和声音数据检查通过。
- 生成的EnglishProbe检查578个初始控件/列表/提示并离线绘制页面；不覆盖所有动态消息和DPI。
- `scripts/check_handoff.py`的公开链接、双语指南/失败编号和索引时效检查通过。刻意过期源码与失效链接样例均被拒绝，恢复文件后工作区干净。

## 审查中的失败与处理

1. 源码原始字节哈希受Git换行转换影响，误报过期。现按无BOM的UTF-8、LF换行文本计算；新副本通过。
2. 生成英文UiProbe `--quiet`在提示文字断言失败。仅调整该断言后又在翻译预览断言失败，局部修改已撤回。英文使用EnglishProbe与独立翻译的HookProbe模式验收；完整quiet仍依赖中文文案，不能把这种测试限制当作运行时故障。

## 验证边界与接续

本轮没有游戏写入、游玩操作、地址迁移、保存读档或新的稳定性实测。历史记录为脱敏摘要与现有代码链接，不包含原始转储/脚本。新安装二进制仍要分析，所有配置/地址/类型/ABI契约在调用前重新成立。

下一位维护者：按COLD_START建立基线，记录目标exe指纹，从TECHNICAL_MAP选一个受影响功能，查其失败编号，填写迁移证据模板。双语历史追加新结论与验证，PROJECT_STATE链接当前记录。
