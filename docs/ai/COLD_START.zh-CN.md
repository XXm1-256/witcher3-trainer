# Agent 冷启动与维护入口

[English](COLD_START.md)

适用于只有本仓库、没有原作者聊天记录或私人研究目录的维护者。当前运行时代码基线为 v0.1.7；本指南更新于 2026-10-05。目标是先建立可复现的源码基线，再独立调查一个具体功能。新游戏版本的地址和效果仍需要目标二进制及实机证据。

## 1. 最短阅读顺序

1. 本页：环境、可执行检查、下一步输出。
2. [PROJECT_STATE](PROJECT_STATE.zh-CN.md)：支持指纹与尚未完成的验证。
3. [TECHNICAL_MAP](TECHNICAL_MAP.zh-CN.md)：功能、源码、地址与资源入口。
4. [FAILURE_LEDGER](FAILURE_LEDGER.zh-CN.md)：实际失败、修复边界和再次尝试所需证据。
5. 改运行时前读 [VERSION_MIGRATION](VERSION_MIGRATION.zh-CN.md)；日常工作规则见 [AGENT_GUIDE](AGENT_GUIDE.zh-CN.md)。

先读 `git status --short` 和 `git log -5 --oneline`，确认当前改动与源码提交。不要把 Releases 下载的 exe、main 最新源码和本机旧程序当成同一版本。

## 2. 准备环境和建立离线基线

需要 Windows x64、Git、.NET 8 SDK；英文生成与文档检查需要 Python 3.10+。源码构建所需的三个 JSON 和图像都在仓库内。无需游戏即可构建；没有目标游戏就无法证明新版本运行时适配。

从仓库根目录逐条运行，检查每条命令的退出码；PowerShell 不会自动因为原生程序非零退出而停止下一条。

```powershell
git clone https://github.com/XXm1-256/witcher3-trainer.git
Set-Location witcher3-trainer
git status --short
git rev-parse HEAD
dotnet --version
python --version
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Language zh-CN
powershell -NoProfile -ExecutionPolicy Bypass -File scripts/build.ps1 -Language en
python scripts/verify_localization.py
python scripts/check_handoff.py
dotnet run --project tools/HookProbe/HookProbe.csproj -c Release -- version-selftest
dotnet run --project tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --quiet
```

输出程序分别在 `dist/zh-CN/Witcher3Modifier.exe` 和 `dist/en/Witcher3Modifier.exe`。不要运行正式程序来代替离线检查：正式程序可能自动恢复保存的开关并连接游戏。UiProbe 使用自身输出目录里的隔离 draft；`--quiet` 不进行游戏操作或试听。

需要验证关闭缺陷时，先关闭游戏，再运行：

```powershell
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --close-without-game
dotnet run --project .build/en/tools/UiProbe/UiProbe.csproj -c Release -- --close-without-game
```

这两条会短暂显示测试窗口。`--item-batch` 使用假执行器，验证部分完成、停止和避免重复提交，也会绘制测试界面。其他探针不能仅凭名称判断安全：`gear-numeric-probe` 会生成装备，`autoloot-query` 即使不取物也可能安装调度入口和执行游戏查询，`swim-neutral-check` 可能写入动画来源。执行前读 [HookProbe 分支](../../tools/HookProbe/Program.cs)。

## 3. 根据当前任务选路

| 情况 | 最先检查 | 完成标准 |
|---|---|---|
| 构建或翻译失败 | SDK、嵌入输入、中文源码与 en.json | 两种语言构建、原生数据不变检查通过 |
| 游戏更新后功能不可用 | exe 指纹、GameVersion、对应函数/类型/布局 | 每个受影响入口有独立迁移证据，再按授权实测 |
| 读档后失效、重复物品、等待不结束 | 日志中的进程身份、请求状态、开关意图与返回数组所有权 | 可区分未开始/运行中/已完成，不重发未知结果 |
| 游戏崩溃 | 最后操作、安装入口、WER/转储、真实故障指令 | 原因与证据关联；原因未明就保留未明状态 |
| 装备数值不合预期 | 实例身份、能力组合、固定基础、最终统一回读 | 精确或可达值说明；需持久化时保存/读档确认 |
| UI/关闭/快捷键问题 | 窗口事件、状态保存、前台注册、相关 UiProbe | 可复现的交互检查通过，保持玩家设置 |

## 4. 调查新游戏版本所需资产

由维护者取得合法安装的游戏 exe、渲染器/平台/版本信息、独立存档副本；对 exe 记录 SHA256、大小和 PE 架构。可使用能分析 Windows x64 PE、注册交叉引用及 RTTI 的反汇编器。仓库不绑定某款反汇编器，也没有能自动适配未知版本的一键脚本。

```powershell
$gameExe = 'D:\Games\The Witcher 3\bin\x64_dx12\witcher3.exe'
Get-FileHash -LiteralPath $gameExe -Algorithm SHA256
(Get-Item -LiteralPath $gameExe).Length
```

路径仅为示例，换成实际安装路径。地址迁移先从 [地址清单](address-inventory.json) 找到受影响源码，再读原字节、注册和类型校验。它是现有代码索引，不是新版本扫描结果，也不是未知版本可直接调用的地址表。

游戏 `.bundle` 中的 XML 定义可以辅助调查。仓库的 [BundleExtract](../../tools/BundleExtract/Program.cs) 只支持它实际检查的 `POTATO70`、304 字节条目、压缩方法 0/1，并校验长度与 CRC。对本地只读输入和新的空输出目录运行：

```powershell
dotnet run --project tools/BundleExtract/BundleExtract.csproj -c Release -- 'D:\Research\input.bundle' 'D:\Research\extracted-new'
```

输出目录不得复用已有成果，工具会写提取文件。不支持的布局就停止调查解析器；不能改成忽略 CRC 或沿用旧工具的 320 字节假设。仓库没有原作者完整脚本树、转储和所有研究脚本；新版本调查要重新从自己的安装资源取得定义。资源提取不是源码构建的前置条件。

## 5. 留下可接续的结果

每次调查新建 `docs/ai/history/YYYY-MM-DD-topic.md` 和对应 `.zh-CN.md`，在 PROJECT_STATE 链接；目录不必预先存在。保存以下信息，即使暂时没有修复也要留下断点：

```text
目标、前驱提交、当前提交：
游戏 SHA256、平台/渲染器、版本：
症状与复现步骤：
受影响源码/方法、旧与新 RVA、布局及注册锚点：
校验/匹配数/指令/ABI、所有权与取消边界：
已排除路径、依据、适用范围、新证据要求：
实际执行命令、退出码、验证层级：
尚未验证的效果、持久化或稳定性：
下一步的一条具体操作、所需玩家协助：
成品/输入 SHA256、恢复步骤与相关提交：
```

提交源码和脱敏结论。私人配置、日志原件、转储、游戏 exe 与原始脚本树保留在维护者自己的诊断目录；公开记录引用脱敏摘要和指纹。修复后先核对最新玩家配置，再替换程序；不要回放物品添加、删除、传送等单次操作。双语更新日志追加保留历史。

## 本指南能证明什么

文档与离线检查可证明仓库可读取、构建以及明确指出既有失败边界。它们不能替代维护者的能力、游戏所有权、反汇编与调试工具、当前实机授权或新版本验证。任何尚未证明的步骤应作为开放问题接续。
