# 时间轴与自定义动画

## 使用场景

奖励保存后，立绘、文字、装饰和卡片按时间轴逐段入场；用户跳过只到表现终态。背包图标连续飞入详情使用项目自定义动画。

## 资源与职责

Godot 场景内保存 AnimationPlayer 和非循环 enter／exit。Unity Prefab 保存原生 legacy Animation 与非循环 AnimationClip，不是 Animator。轨道只写表现属性；项目 restore 回调恢复所有轨道属性，框架不推断任意时间轴快照。

## 使用代码

```csharp
Transition = GodotTransitions.Animation("AnimationPlayer", "enter", "exit", root =>
{
    root.Modulate = Colors.White;
    root.GetNode<Control>("Content").Scale = Vector2.One;
});
// For Unity, use UnityTransitions.Animation(componentPath, enterClip, exitClip, restore).
// Custom recipes implement these two contracts:
public sealed class ProjectTransition : IUITransition
{
    public IUITransitionRun Create(TransitionContext context) => new Run(context);
    private sealed class Run : IUITransitionRun
    {
        private readonly TransitionContext _context;
        public Run(TransitionContext context) { _context = context; }
        public Task PlayAsync(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            _context.ReportProgress(1);
            return Task.CompletedTask;
        }
        public void Restore() { /* Restore captured properties and free temporary visuals. */ }
    }
}
```

## 行为与限制

Godot 片段假设资源原值确实为白色与单位缩放；非默认原值应由项目捕获并恢复。上面的自定义 Run 仅展示接口，不伪装成完整演出。真实动画每次 Create 返回独立状态，首次等待前安装起始状态；需要布局帧时先遮挡进入页，避免最终状态闪现。ReportProgress 为有限、单调的 0..1 播放时间，不能用缓动后的超调值。Restore 同步、成功失败都执行，不重播动画；取消时停止播放器、清理临时节点并恢复属性。跳过、持续动画暂停和 Partner 矩形计算是 Demo 项目实现，不是核心通用 Skip API。Unity 最新首帧修复尚未原生验证，Demo 已保存轨道也有待验证项。

## 相关章节

[转场](transitions.md) · [Samples](../samples/index.md)
