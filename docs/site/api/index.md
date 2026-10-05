# API Reference

## 查找入口

| API | 典型用途 |
| --- | --- |
| [Core](core.md) | Context、Node、四种 Container、Dialog、生命周期及适配契约 |
| [Godot](godot.md) | Control／PackedScene 接入、AnimationPlayer、Tween 配方和遮罩 |
| [Unity](unity.md) | RectTransform／Resources 接入、legacy AnimationClip 和遮罩 |
| [MVVM](mvvm.md) | 可选数据源、绑定、命令、列表与编辑器配置 |

按类型查签名与默认值；场景用法见 [Manual](../manual/concepts.md)。

## 选操作

| 意图 | API |
| --- | --- |
| 打开独立窗口，允许隐藏恢复 | Panel.OpenAsync／HideAsync／ShowAsync／CloseAsync |
| 前进页面与返回 | Stack.PushAsync／PopAsync／PopToAsync |
| 已有分类页面切换 | Tab.BindAsync／SwitchAsync(name)／HideAsync |
| 嵌套弹窗与跨层关闭 | Modal.OpenAsync／CloseAsync／CloseToAsync |
| 等待业务答案 | Modal.ShowDialogAsync + Dialog.Complete |
| 准备资源而不显示 | Context.PreloadAsync |
| 按来源选择动画 | Transitions.Select(context => recipe) |
