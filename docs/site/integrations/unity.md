# Unity 适配

完整接入步骤见 [Unity Getting Started](../getting-started/unity.md)。

## 资源与控件

- Prefab 根为 RectTransform，并挂载继承适配 UINode 的业务脚本（MonoBehaviour），保存在 Resources 下，注册路径不带扩展名。
- 挂载和资源根不放可命中的全屏 Graphic；弹窗遮罩由 Modal 创建。
- Tab 绑定既有 RectTransform，不克隆。GetRoot／GetControl 访问真实控件。

```csharp
var ui = UI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory");
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = Transitions.SlideHorizontal(targetPath: "Content");
var inventory = await pages.PushAsync<InventoryView>();
Button close = inventory.GetControl<Button>("Content/Close");
```

## 生命周期与动画

- Create 配置隐藏且跨场景存活的帧泵；用 BindLifetime 绑定场景或 GameObject，详见[生命周期](../manual/lifecycle.md)。
- 输入门保留按钮的业务 interactable；键盘导航由 EventSystem 配置。
- 支持 Timeline／PlayableDirector 和非循环 legacy AnimationClip，均按未缩放时间播放。
- Resources 请求不可直接取消；释放框架引用不立即清空引擎缓存。

## 在 Visual Studio 调试框架

在仓库根目录生成并同步 Debug DLL 与匹配的 PDB：

```powershell
./scripts/Prepare-Mvvm.ps1 -Configuration Debug
```

Unity 刷新程序集后，在 VS 打开本仓库源码并设置断点，选择 **调试 → 附加 Unity 调试器**，连接使用该包的项目，再触发 UI 操作。单步跳过框架时关闭“启用仅我的代码”；Unity 编辑器须使用 Debug 优化模式。

DLL 与 PDB 已复制到现有包的 Plugins 目录，不需安装源码包。源码必须与编译版本一致；修改后重新运行上述命令。使用 `Run-UnityDemo.ps1` 时也传入 `-Configuration Debug`，避免重新准备为 Release。恢复 Release 使用 `-Configuration Release`；两种配置都同步匹配的 PDB。

## 验证状态

当前环境为 Unity 6000.3.24f1 / uGUI 2.0.0。客户端、适配器、生命周期及已保存 Timeline／AnimationClip 资源通过 31 项原生 Play Mode 回归。MVVM 编辑器 Undo／Prefab Override 与预览通过验证。独立入门教程的 C# 示例已在 Unity 6000.3.24f1 batchmode 编译；教程 UI 尚未原生运行验证。Unity Source Generator 工作流尚未接入。

[Unity API](../api/unity.md) · [时间轴](../manual/timeline-animation.md)
