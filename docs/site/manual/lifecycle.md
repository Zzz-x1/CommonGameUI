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
using EngineUI = UIFramework.Godot;

public sealed class PlayerProfile
{
    public string Nickname { get; set; } = "Traveller";
}
public partial class ProfileView : EngineUI.UINode
{
    public PlayerProfile Profile { get; set; } = null!;
    private AnimationPlayer _decoration = null!;
    protected override Task OnInitializeAsync(CancellationToken token)
    {
        _decoration = this.GetRoot().GetNode<AnimationPlayer>("Decoration");
        _decoration.Play("idle");
        _decoration.Active = false;
        return Task.CompletedTask;
    }
    protected override Task OnEnteringAsync(LifecycleContext context, CancellationToken token)
    {
        this.GetControl<Label>("Content/PlayerName").Text = Profile.Nickname;
        return Task.CompletedTask;
    }
    protected override void OnActivated(StateChangeContext context) => _decoration.Active = true;
    protected override void OnDeactivated(StateChangeContext context) => _decoration.Active = false;
}
```

```csharp
ui.Register<ProfileView>("res://Profile.tscn", view => view.Profile = profile);
await pages.PushAsync<ProfileView>();
```

## 绑定引擎生命周期

退出关卡时，让游戏 UI 跟随关卡对象或场景销毁。绑定是同步配置，不负责打开界面：

```csharp
// Unity: using UIFramework.Unity;
var objectLifetime = gameUI.BindLifetime(gameObject);
// Or bind the scene through a hidden scene-local GameObject:
var sceneLifetime = ui.BindLifetime(gameObject.scene);

// Godot: using UIFramework.Godot;
using EngineUI = UIFramework.Godot;
// A scene is represented by its root node.
var godotLifetime = gameUI.BindLifetime(sceneRoot);
```

- Node 绑定清理自身及子树；Context／Root 绑定还释放 Context 资源。
- Unity 的场景管理对象和持续观察对象均隐藏于 Hierarchy。Godot 不增加场景节点。
- 销毁触发清理；禁用、隐藏或移出层级不触发。Godot QueueFree 也会触发。
- 原生业务脚本自动绑定所属组件生命周期；还可显式绑定一个其他对象或场景。`binding.Dispose()` 解除绑定，保留节点和传入的引擎对象。
- 销毁后在后续引擎帧开始清理，不假定 Destroy／Free 返回时已经完成。
- `binding.Completion` 等待清理结束；明确解除绑定时立即完成。正常主动关闭仍 await 容器关闭或 DisposeAsync。
- 绑定不负责切换到主菜单。自动清理不播放退出转场；进行中的操作取消。项目仍须观察自己发起的操作任务。
- OnDispose 释放业务资源，不能依赖已销毁的控件；业务回调异常原样传播。需要访问控件或播放退场时，先主动关闭，再卸载场景。
- 应用退出前仍需 await ui.DisposeAsync；引擎主循环停止后无法继续异步清理。

## 要点

- Initialize 一次，进入／退出可重复；隐藏保留实例，销毁结束子树。
- IsVisible 表示可见，IsActive 表示稳定激活，IsInteractive 还包含临时输入锁。
- 同步通知只读框架；生命周期中不能销毁自己或祖先。
- AddLifecycle 在挂载前注册；空闲时可移除通知，已初始化行为仍需最终清理。

[阶段签名](../api/core.md) · [错误与提交](async-and-errors.md)

KeepAlive 关闭只退出和失活，初始化不重复，Lifetime 保留。重开时更新参数，`IsCacheReuse` 为 true；最终释放才执行 Dispose。见[关闭后缓存](resources.md#关闭后缓存)。
