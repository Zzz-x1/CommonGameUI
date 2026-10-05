# 时间轴与自定义动画

奖励先保存，再播放立绘、文字和卡片入场；跳过只改变表现，不重复发奖。

## 用法

Godot 使用 AnimationPlayer；Unity 同时支持 Timeline／PlayableDirector 和 legacy Animation／AnimationClip。轨道只写表现，restore 回调恢复所有轨道属性。

```csharp
Transition = Transitions.Animation("AnimationPlayer", "enter", "exit", root =>
{
    root.Modulate = Colors.White;
    root.GetNode<Control>("Content").Scale = Vector2.One;
});
```

## Unity Timeline

大厅立绘、欢迎文字和按钮可分别放在 Animation 轨道中。Prefab 根节点下建立 `Transitions/Enter`、`Transitions/Exit`，各添加 PlayableDirector，指定有限时长的 Timeline 资产并绑定本地控件。关闭 **Play On Awake**。

在节点初始化时捕获轨道修改的属性并配置转场：

```csharp
var root = this.GetRoot();
var rects = root.GetComponentsInChildren<RectTransform>(true)
    .ToDictionary(r => r, r => (r.anchoredPosition, r.localScale, r.localRotation));
var groups = root.GetComponentsInChildren<CanvasGroup>(true)
    .ToDictionary(g => g, g => g.alpha);
Transition = Transitions.Timeline("Transitions/Enter", "Transitions/Exit", _ =>
{
    foreach (var pair in rects)
    {
        pair.Key.anchoredPosition = pair.Value.anchoredPosition;
        pair.Key.localScale = pair.Value.localScale;
        pair.Key.localRotation = pair.Value.localRotation;
    }
    foreach (var pair in groups) pair.Key.alpha = pair.Value;
});
```

首次等待前采样起点，按未缩放时间播放。完成、取消或失败后停止播放图、恢复 Director 配置，并执行恢复回调。绑定对象在播放中销毁会取消转场；无关的外部提前停止仍报错。

`playAnimation: false` 跳过播放。不同来源或简化演出通过 [Transitions.Select](transitions.md) 选择配方。制作资产需安装 `com.unity.timeline`；运行时适配只依赖 Unity 的 Director 模块。轨道只负责表现，不控制框架根节点显隐，不执行购买、保存或发奖。

## 要点

- 自定义转场通过对应引擎 Host.NextFrameAsync 等待帧，随后检查取消 token；不要把等待任务绑定到即将销毁的控件。
- 示例恢复白色和单位缩放；资源原值不同时，捕获并恢复实际值。
- 首次等待前应用起点；需要等待布局时先遮挡进入页，避免闪烁。
- 自定义动画实现 IUITransition／IUITransitionRun，每次 Create 保存独立状态；Restore 在成功和失败后同步清理。
- ReportProgress 使用单调 0..1 时间。跳过与 Partner 图片转场由项目实现，参考 Demo。

[转场](transitions.md) · [Samples](../samples/index.md)
