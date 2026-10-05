# UIFramework

纯 C#、渲染无关的游戏 UI 管理框架。使用层级节点组织界面，通过专用容器管理独立窗口、页面历史、内嵌分类和模态结果；控件、布局与绘制由引擎负责。

A renderer-independent C# UI management framework for games. Nodes and focused containers manage panels, page history, embedded tabs and dialogs while your engine owns widgets and rendering.

## Documentation / 文档

| 入口 | 中文 | English |
| --- | --- | --- |
| 文档首页 | [阅读文档](docs/site/index.md) | [Documentation](docs/site/en/index.md) |
| Getting Started | [Godot](docs/site/getting-started/godot.md) / [Unity](docs/site/getting-started/unity.md) | [Godot](docs/site/en/getting-started/godot.md) / [Unity](docs/site/en/getting-started/unity.md) |
| Manual | [概念与使用场景](docs/site/manual/concepts.md) | [Concepts and workflows](docs/site/en/manual/concepts.md) |
| API Reference | [API 索引](docs/site/api/index.md) | [API index](docs/site/en/api/index.md) |
| Samples | [游戏客户端](docs/site/samples/index.md) | [Game clients](docs/site/en/samples/index.md) |

文档同时支持 GitHub Markdown 与双语 VitePress 站点。侧栏、搜索、代码复制与本地预览见 [文档维护与发布](docs/site/README.md)。当前未发布站点，不提供虚构在线地址。

Documents work as GitHub Markdown and a bilingual VitePress site. See [building and publishing](docs/site/README.md) for local preview, search and manual Pages deployment. No hosted site URL is claimed yet.

## 能力 / Features

- `PanelContainerNode`：单实例／多实例，隐藏保留、恢复、关闭销毁。
- `StackContainerNode`：页面历史、返回、返回指定已有实例。
- `TabContainerNode`：绑定资源内既有页面，按名称切换与收起。
- `ModalContainerNode` / `DialogNode<TResult>`：嵌套弹窗、遮罩与等待结果。
- 生命周期、资源预加载与共享持有、取消及原始异常传播。
- 原生时间轴、自定义转场、来源／阶段条件分支；可选注册与调用 Source Generator。

Panel retains independent windows; Stack retains page history; Tab selects embedded pages; Modal/Dialog supports nested prompts and awaited answers. Lifecycle, preloading, shared resource ownership and transition selection remain engine-independent.

默认仅提供结构 Root，项目自定义挂载与返回政策。框架不提供控件／布局系统、HUD／Overlay 预设、自动导航队列、绑定系统或 Analyzer。

Only structural Root is supplied. Mounts and return policy belong to the project. There is no widget/layout system, business-layer preset, automatic navigation queue, binding framework or Analyzer.

## 快速运行 / Quick start

Godot 4.7.2 .NET、.NET 10 SDK、PowerShell。从仓库根目录运行：

```powershell
./scripts/Get-Godot.ps1
./scripts/Run-Demo.ps1
./scripts/Test.ps1
```

独立的小型教程无需游戏客户端数据：`dotnet build docs/examples/godot/Tutorial.csproj`，随后在 Godot 打开该目录的 project.godot。Unity 在添加本地 UPM 包前执行 `./scripts/Run-UnityDemo.ps1 -PrepareOnly` 准备核心 DLL，按教程手动创建资源或打开 Demo/Unity。

For the standalone tutorial, build `docs/examples/godot/Tutorial.csproj` and open its project.godot. For Unity, prepare the core DLL using `./scripts/Run-UnityDemo.ps1 -PrepareOnly`, then follow its tutorial or open Demo/Unity manually.

## 当前支持边界 / Current status

Core 目标 netstandard2.1。Godot 4.7.2 .NET Demo「星庭远征」为持续维护的纯 UI 客户端。Unity 适配以 6000.3.24f1 / uGUI 2.0.0 编译，最新适配修复与教程仅离线编译，Demo 时间轴资源原生验证仍有待完成项。其他引擎需实现适配器；未验证移动端、IL2CPP 或任意引擎版本。当前使用源码接入，不承诺公开 NuGet 包、下载发布或未经确认的许可证。

Core targets netstandard2.1. The Godot 4.7.2 .NET Star Court client is maintained alongside the framework. Unity source compiles with 6000.3.24f1/uGUI 2.0.0; recent fixes and the tutorial have offline compilation only, with native Demo timeline verification pending. Other engines need adapters. Mobile/IL2CPP and arbitrary versions are unverified. Source integration is available; no public NuGet release or unconfirmed license is promised.

## 维护者 / Maintainers

[唯一设计与验收记录](docs/design-decisions.md) · [V1](docs/v1-development.md) · [V2](docs/v2-development.md)。用户文档描述当前实现；内部决策和待定范围集中于设计文档。素材记录见 [Godot](Demo/Godot/Assets/SOURCES.md) 与 [Unity](Demo/Unity/Assets/SOURCES.md)。
