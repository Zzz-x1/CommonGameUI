# UIFramework

纯 C#、渲染无关的游戏 UI 管理框架。节点管理界面关系，容器管理打开、切换和返回；控件与布局使用引擎原生资源。

[中文文档](docs/site/index.md) · [English documentation](docs/site/en/index.md)

## 文档

双击根目录的 [Open-Docs.cmd](Open-Docs.cmd)，自动启动本地文档并打开浏览器。也可运行 `./scripts/Open-Docs.ps1`；需要 Node.js 22+，首次运行自动安装文档依赖。

| 章节 | 中文 | English |
| --- | --- | --- |
| Getting Started | [Godot](docs/site/getting-started/godot.md) / [Unity](docs/site/getting-started/unity.md) | [Godot](docs/site/en/getting-started/godot.md) / [Unity](docs/site/en/getting-started/unity.md) |
| Manual | [使用指南](docs/site/manual/concepts.md) | [User guide](docs/site/en/manual/concepts.md) |
| API Reference | [API 索引](docs/site/api/index.md) | [API index](docs/site/en/api/index.md) |
| Samples | [游戏客户端](docs/site/samples/index.md) | [Game clients](docs/site/en/samples/index.md) |

## 能力

- Panel：单实例／多实例窗口，隐藏保留、恢复和销毁。
- Stack：页面历史，返回上页或指定实例。
- Tab：绑定资源内页面，按名称切换。
- Modal／Dialog：嵌套弹窗、遮罩与等待答案。
- 生命周期、资源预加载、条件转场与原生时间轴；可选 Source Generator。
- 可选 [MVVM](docs/site/manual/mvvm.md)：Cecil 通知、三种 VM 来源、双向绑定、命令、列表及两端 GUI。

默认只提供结构 Root，项目定义业务挂载和返回规则。

## 运行 Godot Demo

需要 Godot 4.7.2 .NET、.NET 10 SDK 和 PowerShell。从仓库根目录执行：

```powershell
./scripts/Get-Godot.ps1
./scripts/Run-Demo.ps1
./scripts/Test.ps1
```

## 接入状态

Core 目标 netstandard2.1。Godot 持续维护完整 UI 客户端；Unity 客户端、生命周期和时间轴已通过原生 Play Mode 回归。独立 Unity 入门教程仍仅离线编译。详见 [Unity 适配](docs/site/integrations/unity.md)。

## 维护

[文档构建与发布](docs/site/README.md) · [设计与验收记录](docs/design-decisions.md) · [素材来源](Demo/Godot/Assets/SOURCES.md)
