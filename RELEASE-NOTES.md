# v0.1.0-beta.1

CommonGameUI 首个公开 Beta，采用 MIT 协议。API 尚未稳定，后续调整不保留历史兼容层。

## 本版能力

- 渲染无关 Core，Unity uGUI 与 Godot Control 适配。
- Panel、Stack、Tab、Modal／Dialog，页面历史、嵌套弹窗和关闭后实例缓存。
- 生命周期、资源管理、条件转场、Unity Timeline／Animation 与 Godot AnimationPlayer。
- 可选 MVVM：属性通知、双向绑定、命令参数、异步集合条目、转换器和编辑器配置。
- Unity 全局 SO VM：Provider 名称查找、按需初始化、Context 共享与异步释放。
- 两端客户端 Demo、中英文教程与 API 文档。

## 安装

Unity 6000.3：下载 Release 附件中的 `.tgz`，在 Package Manager 使用 **Add package from tarball**。先安装 `com.ui-framework.unity`，再按需安装 `com.ui-framework.mvvm.unity`。两包包含 Debug DLL 和匹配 PDB；调试须使用本标签源码。MVVM 包依赖 Unity Mono.Cecil 包。

Godot 4.7.2 .NET：下载本标签源码，按照 [Godot 入门](docs/site/getting-started/godot.md) 接入。源码仓库中的 Unity DLL 是生成产物，源码安装须运行 `scripts/Prepare-Mvvm.ps1 -Configuration Debug`。

## 验证与边界

此前已完成 51 项相关核心／Weaver／集合测试、Unity MVVM 原生与编辑器专项及客户端购买流程验证。本次新增 Modal 逐级返回专项通过，Debug DLL／PDB 重新生成，差异检查通过。未重新执行全量回归、Godot 原生运行或 WebGL／IL2CPP 导出；本版不声明全平台验收完成。

Unity SO VM 必须继承 `GlobalViewModel`，独立窗口和条目使用普通 C# VM。第三方项目需要按当前 API 迁移，不提供旧接口兼容。Demo 素材不统一适用 MIT，见 [素材记录](Demo/Godot/Assets/SOURCES.md)。

---

First public Beta of CommonGameUI (MIT). APIs may change without compatibility shims.

Renderer-independent Core with Unity uGUI and Godot Control adapters; panels, stacks, tabs, modals/dialogs, instance caching, lifecycle, resource management, conditional transitions and engine timeline support. Optional MVVM includes notifications, two-way bindings, commands, asynchronous collection items, converters and editor configuration. Bilingual guides and client UI demos are included.

For Unity 6000.3, install the base `.tgz` through Package Manager's **Add package from tarball**, then optionally install the MVVM `.tgz`. Both include Debug DLLs and matching PDBs; use the tagged source for debugging. For Godot 4.7.2 .NET, follow the [getting started guide](docs/site/en/getting-started/godot.md) from the tagged source.

Previous targeted Core, Weaver, collection, Unity native/editor and client-flow checks passed. This release adds a passing modal-return check and rebuilt Debug binaries. Full regression, Godot native execution and WebGL/IL2CPP exports were not repeated. Unity SO VMs must derive from `GlobalViewModel`; independent VMs remain ordinary C# objects. Demo assets have separate licenses.
