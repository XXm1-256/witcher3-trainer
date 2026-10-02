# 开发与学习教程

## 建立可重复构建的起点

Windows安装.NET 8 SDK，英文生成另需Python 3.10以上。克隆后运行`scripts/build.ps1 -Language zh-CN`及`-Language en`。源码已包含物品/等级JSON、装备预设与狼头资源，不要求先解包游戏才能编译。玩家成品自包含，源码开发需要SDK。

中文源码为权威，英文源在`.build/en`生成：使用审阅过的翻译表，不改变原生ID、地址与机器码；装备预设名称/说明在生成器中提供，物品采用内部英文标识。维护时改原源码与翻译表，不直接改生成文件。

## 阅读顺序与练习

1. Program和MainForm各部分：控件、事件、忙状态、恢复保护、Draft保存、提示、快捷键；离线沿金币按钮追踪至后台。
2. GameMoney和GameVersion：进程连接、哈希、模块基址、RVA、对象虚表、读写与适配表。练习计算`VA=模块基址+RVA`，区分堆对象指针。
3. GameInventory、GameItem及目录JSON：区分游戏物品ID与背包实际实例，编写离线搜索练习。
4. GameItemScheduler各部分：串行请求、函数元数据、游戏线程执行、返回值、取消和资源所有权。沿自动拾取超时追踪等待与延迟清理。
5. GameInventoryHooks、GameExperience和装备数值部分：入口校验、指令与属性下限；解释界面数值为何未必等于内存存储值。
6. TrainerTheme、MainForm.Theme、ToggleSounds：布局、DPI、操作反馈和实际效果反馈。

## 离线验证命令

```powershell
dotnet build Witcher3Modifier/Witcher3Modifier.csproj -c Release
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --help-copy
dotnet run --project tools/UiProbe/UiProbe.csproj -c Release -- --quiet
dotnet run --project tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
python scripts/localize.py
python scripts/verify_localization.py
dotnet run --project .build/en/tools/EnglishProbe/EnglishProbe.csproj -c Release
dotnet run --project .build/en/tools/HookProbe/HookProbe.csproj -c Release -- autoloot-recovery-offline
```

这些指定模式不访问游戏；HookProbe其他模式可能写入游戏，执行前读分支。EnglishProbe检查初始控件/列表/提示文字并渲染五页，不能替代所有动态消息或DPI状态检查。UiProbe的测试配置位于工具输出文件夹，与玩家根配置分离。

## 维护功能

先记录预期与复现，优先现成注册函数和元数据，以最小改动修复，并做相关离线回归。中英文构建后检查改动界面。实机需玩家明确授权并保存，一次验证隔离操作；内存回读、玩家实际效果、存档持久化分别记录。更新地址前读迁移教程。

暂停超时不代表请求失败，要区分排队、执行和完成。已经完成的数组析构不能重复，未确认清理的所有权须保留；跨进程禁止复用堆地址。不能绕过入口和类型检查来维持开关。

## 发布与接档

每轮新增日期研究记录，保留失败、依据和适用范围，再更新当前状态的引用。成品不带个人配置/日志、转储、游戏本体和解包脚本。标签对应审阅源码，中英文独立压缩包顶层放exe、说明、LICENSE、NOTICE和校验值。从干净克隆构建后才宣称可重复构建。
