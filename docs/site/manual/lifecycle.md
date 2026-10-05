# 生命周期

绑定控件只做一次；每次进入刷新数据；失活时暂停装饰，恢复后继续。

## 阶段

| 阶段 | 职责 |
| --- | --- |
| OnInitializeAsync | 首次挂载：绑定控件和事件 |
| OnEnteringAsync／OnExitingAsync | 进入／退出前的可等待准备 |
| OnEntered／OnExited | 操作提交后的同步通知 |
| OnActivated／OnDeactivated | 开始／暂停持续表现 |
| OnVisibilityChanged | 有效可见性变化 |
| OnDisposeAsync | 最终释放业务持有的资源 |

## 示例：资料刷新与装饰暂停

Godot 资源包含 Content/PlayerName（Label）及 Decoration（AnimationPlayer，循环 idle 动画）：

```csharp
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;

public sealed class PlayerProfile
{
    public string Nickname { get; set; } = "Traveller";
}
public sealed class ProfileView : UINode
{
    private readonly PlayerProfile _profile;
    private AnimationPlayer _decoration = null!;
    public ProfileView(PlayerProfile profile) { _profile = profile; }
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _decoration = this.GetControl().GetNode<AnimationPlayer>("Decoration");
        _decoration.Play("idle");
        _decoration.Active = false;
        return Task.CompletedTask;
    }
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        this.GetControl<Label>("Content/PlayerName").Text = _profile.Nickname;
        return Task.CompletedTask;
    }
    protected override void OnActivated(StateChangeContext context) => _decoration.Active = true;
    protected override void OnDeactivated(StateChangeContext context) => _decoration.Active = false;
}
```

```csharp
ui.Register(() => new ProfileView(profile), "res://Profile.tscn");
await pages.PushAsync<ProfileView>();
```

## 要点

- Initialize 一次，进入／退出可重复；隐藏保留实例，销毁结束子树。
- IsVisible 表示可见，IsActive 表示稳定激活，IsInteractive 还包含临时输入锁。
- 同步通知只读框架；生命周期中不能销毁自己或祖先。
- AddLifecycle 在挂载前注册；空闲时可移除通知，已初始化行为仍需最终清理。

[阶段签名](../api/core.md) · [错误与提交](async-and-errors.md)
