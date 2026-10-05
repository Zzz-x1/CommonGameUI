# UIFramework 文档

纯 C#、渲染无关的游戏 UI 管理框架。用节点与具备明确职责的容器组织窗口、页面历史、内嵌 Tab 和弹窗；布局、控件与绘制继续交给游戏引擎。

## 从这里开始

| 入口 | 你将学到什么 |
| --- | --- |
| [Getting Started](getting-started/index.md) | 在 Godot 或 Unity 中搭建第一个菜单与设置流程 |
| [Manual](manual/concepts.md) | 按实际游戏场景理解实例、生命周期、资源和动画 |
| [Integrations](integrations/custom-engine.md) | 引擎适配与自定义资源后端 |
| [Samples](samples/index.md) | 运行「星庭远征」客户端与常见业务组合 |
| [API Reference](api/index.md) | 核对签名、默认值和操作语义 |
| [Troubleshooting](troubleshooting.md) | 查找常见接入错误与当前限制 |

## 选择正确的容器

背包窗口开关用 Panel；大厅→背包→详情→返回用 Stack；设置资源内的音量／画面分类用 Tab；设置→画质或购买确认用 Modal／Dialog。每种容器直接提供对应 API，没有中间 Navigation 或 Selection 对象。

只默认创建结构 Root。业务类可命名为 InventoryView，项目自行决定挂载、层级和返回规则。

当前 Godot Demo 使用 4.7.2 .NET，核心目标 netstandard2.1。Unity 适配当前以 6000.3.24f1 编译，最新改动原生验证与 Demo 时间轴资源验证尚未完成。其他引擎需实现适配器，不能直接套用引擎专属资源。

[English documentation](en/index.md)
