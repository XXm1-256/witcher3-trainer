# 巫师3修改器

[English](README.md) · [玩家指南](docs/USAGE.zh-CN.md) · [开发教程](docs/DEVELOPMENT.zh-CN.md) · [AI Agent接档指南](docs/ai/AGENT_GUIDE.zh-CN.md)

Windows x64单机外部修改器，提供中文、英文可执行版本。自动连接正在运行的游戏，通过校验过的内存结构和游戏现有流程执行功能，支持小键盘快捷键、保存设置及操作反馈音。

## 下载和使用

从[发布页](https://github.com/XXm1-256/witcher3-trainer/releases)下载zh-CN或en压缩包，解压至可写文件夹。游戏载入存档后，打开文件夹中的`Witcher3Modifier.exe`。玩家无需安装.NET SDK。不同语言分别解压，修改装备或传送前先保存进度。

## 功能

- 金币修改、金币收入与经验倍率。
- 生命、活力、呼吸、毒性、马匹活力/恐惧、肾上腺素与战斗辅助。
- 物品与弹药不减、装备修复、制作与穿戴等级辅助。
- 搜索及添加物品、指定数量、DLC物品目录、自定义与修改装备、词条目标数值、多选删除。
- 地图标记传送、无落地伤害、猫眼夜视、移动/跳跃/游泳倍率。
- 天气、时间、发型/胡须与视觉实验，昆特牌获胜和洗点。
- 五米内容器拾取与草药采集，暂停/读档等待后继续。
- 开关与倍率保存、快捷键总开关、功能提示与音效。

## 适配和验证范围

当前开发适配基于**5.00c、Windows DX12 x64**。源码中的旧版参考地址不代表所有版本均适配；具体操作会校验代码入口与对象。维护新版本先读[地址迁移教程](docs/ai/VERSION_MIGRATION.zh-CN.md)。

已进行构建、离线回归与英文界面检查；部分实机功能有玩家反馈，尚没有覆盖全部功能的自动化游戏测试。最新暂停恢复逻辑经过离线检查，游泳推进速度、高倍率运动、偶发武器自动替换和全部复杂地形传送仍待更多实机验收。[当前工程状态](docs/ai/PROJECT_STATE.md)明确区分验证层级。

英文界面及提示已翻译，物品名采用内部英文标识，部分名称是编号或技术名称，保留中文搜索别名。

## 从源码制作

Windows安装.NET 8 SDK；英文构建另需Python 3.10以上。

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Language zh-CN
powershell -ExecutionPolicy Bypass -File scripts/build.ps1 -Language en
```

成品位于`dist/zh-CN`和`dist/en`。[开发与学习教程](docs/DEVELOPMENT.zh-CN.md)提供代码阅读顺序、构建、离线测试和功能维护流程。

原创程序代码采用[MIT](LICENSE)，游戏元数据、商标和狼学派图像的权利说明见[NOTICE](NOTICE.md)。本项目为非官方同人项目。
