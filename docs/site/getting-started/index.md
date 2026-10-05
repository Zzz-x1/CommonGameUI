# Getting Started

## 本框架解决什么问题

UIFramework 管理 UI 的身份、层级、打开／返回、生命周期、输入状态与资源持有。它不绘制按钮，不统一两种引擎的布局文件，也不自动生成业务层级。业务模型、网络、存档和奖励仍由游戏项目实现。

## 环境与安装方式

| 组件 | 当前要求／方式 |
| --- | --- |
| Core | netstandard2.1；源码 ProjectReference 或构建 DLL |
| Godot | 4.7.2 .NET、.NET 10 SDK；引用 Core 与 Godot 项目 |
| Unity | 当前编译验证为 6000.3.24f1、uGUI 2.0.0；准备 DLL 后添加本地 UPM 包 |
| Generator | 可选 Roslyn 生成器，通过 Analyzer 项目引用接入；Unity 工作流尚未集成 |

仓库当前没有可承诺的公开 NuGet 包或发布下载地址，直接使用源码。SDK 与引擎版本表示本工程使用和验证的环境，不代表所有版本兼容性。

## 两条入门路线

1. [Godot 教程](godot.md)：提供完整场景资源与宿主，菜单→设置→分类切换→确认退出。
2. [Unity 教程](unity.md)：提供完整 C# 宿主和 Inspector／Prefab 配置步骤，实现相同流程；原生运行尚未验证。

先使用手写 Register，看清资源与实例关系；熟悉后再读 [Source Generator](../manual/source-generator.md)。示例中短片段的 ui、pages、windows、modals 和 tabs 是已经初始化的 Context／容器变量；View 类型表示项目定义的业务节点。完整可运行代码集中在入门教程，Manual 不重复整套宿主。

[下一步](next-steps.md)
