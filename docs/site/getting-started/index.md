# Getting Started

先用手写注册完成菜单与设置，再按项目需求加入动画、资源预加载和生成器。

| 组件 | 当前要求／方式 |
| --- | --- |
| Core | netstandard2.1；源码 ProjectReference 或构建 DLL |
| Godot | 4.7.2 .NET、.NET 10 SDK；引用 Core 与 Godot 项目 |
| Unity | 当前编译验证为 6000.3.24f1、uGUI 2.0.0；准备 DLL 后添加本地 UPM 包 |
| Generator | 可选 Roslyn 生成器，通过 Analyzer 项目引用接入；Unity 工作流尚未集成 |

当前从源码接入。Manual 的短示例假设容器已初始化、业务 View 已定义；完整代码见入门教程。

[Godot 入门](godot.md) · [Unity 入门](unity.md) · [下一步](next-steps.md)
