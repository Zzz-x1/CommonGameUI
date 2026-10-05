# 时间轴与自定义动画

奖励先保存，再播放立绘、文字和卡片入场；跳过只改变表现，不重复发奖。

## 用法

Godot 场景保存非循环 AnimationPlayer 轨道；Unity 使用 legacy Animation／AnimationClip。轨道只写表现，restore 回调恢复所有轨道属性。

```csharp
Transition = GodotTransitions.Animation("AnimationPlayer", "enter", "exit", root =>
{
    root.Modulate = Colors.White;
    root.GetNode<Control>("Content").Scale = Vector2.One;
});
```

## 要点

- 自定义转场通过对应引擎 Host.NextFrameAsync 等待帧，随后检查取消 token；不要把等待任务绑定到即将销毁的控件。
- 示例恢复白色和单位缩放；资源原值不同时，捕获并恢复实际值。
- 首次等待前应用起点；需要等待布局时先遮挡进入页，避免闪烁。
- 自定义动画实现 IUITransition／IUITransitionRun，每次 Create 保存独立状态；Restore 在成功和失败后同步清理。
- ReportProgress 使用单调 0..1 时间。跳过与 Partner 图片转场由项目实现，参考 Demo。

[转场](transitions.md) · [Samples](../samples/index.md)
