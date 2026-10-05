# API Reference

## 查找入口

| API | 典型用途 |
| --- | --- |
| [Core](core.md) | Context、Node、四种 Container、Dialog、生命周期及适配契约 |
| [Godot](godot.md) | Control／PackedScene 接入、AnimationPlayer、Tween 配方和遮罩 |
| [Unity](unity.md) | RectTransform／Resources 接入、legacy AnimationClip 和遮罩 |

API 页列出实际签名与默认值，使用过程见 [Manual](../manual/concepts.md)。签名列表不是一整份可编译 C# 文件。代码片段的异步操作需 await，短示例的业务类型与数据服务由项目定义。

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

没有 Replace、全局 Navigation／Selection 对象或统一 Skip API。公共接口保持渲染无关，引擎专属配置在对应适配器。
