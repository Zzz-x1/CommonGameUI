# ModalContainerNode：模态与结果弹窗

用于暂时阻止背景操作的窗口，例如商品详情上的购买确认、退出确认。顶层弹窗获得输入；打开新弹窗时，**下层弹窗保留实例并保持可见**，关闭后恢复其交互和焦点。

只显示一个窗口、按历史逐级返回的菜单，用 [Stack](stacks.md)。需要同时具备遮罩和历史时，组合一个 Modal 外壳与内部 Stack，见下例。

## 资源与遮罩

弹窗资源放窗口内容，遮罩由容器创建，不在每个弹窗里重复制作。modals 为已挂载的 `ModalContainerNode`；`Observe` 是项目的异步任务观察入口。

```csharp
ui.DefaultBackdropFactory = Backdrops.Dim(Observe, duration: .2);

// Most Modal containers inherit this configuration automatically.
var details = await modals.OpenAsync<ProductDetailsView>();
var confirm = await modals.OpenAsync<PurchaseConfirmView>();
await modals.CloseAsync(confirm);
```

确认框默认关闭并销毁；KeepAlive 时缓存，原商品详情仍是同一个实例。`Shared` 共用一张遮罩，放在当前弹窗下；`PerModal` 每个弹窗创建一张，允许背景逐层加深。两种配置都不会自动隐藏下层窗口。

### 覆盖或禁用

`Backdrops` 使用对应引擎的命名空间。Factory 同时定义外观和动画，默认配置仅属于当前 Context；新容器自动继承，默认使用 Shared。需要覆盖、禁用或恢复默认时，在没有打开窗口且没有执行操作的容器上配置：

```csharp
modals.ConfigureBackdrop(Backdrops.Dim(Observe, duration: .35), ModalBackdropMode.PerModal);
modals.DisableBackdrop();
modals.UseDefaultBackdrop(ModalBackdropMode.Shared);
```

明确禁用时不创建遮罩；否则优先使用容器覆盖，再使用 Context 默认。默认为 null 时不创建。每次打开开始时确定默认配置，修改只影响后续打开时新建的遮罩；已有 Shared 遮罩保留到最后一层关闭。流程中途首次创建的 Shared 遮罩也会服务仍存在的下层窗口。

### 动画时序

窗口初始化、遮罩创建和进入准备完成后，窗口与新遮罩的入场动画同时播放。关闭时，窗口和需要移除的遮罩也同时退出。整个操作等待两者结束后才提交交互／焦点或销毁资源。

Shared 遮罩仍被下层弹窗使用时保留，不重新淡入／淡出。传 `playAnimation: false` 同时关闭窗口和遮罩动画。任一动画失败会取消另一侧，等待收尾后恢复状态并传播原异常。

窗口显示和内置动画起点与新遮罩在同一帧开始。这里的显示不等于交互激活：双方动画结束后才开放输入并提交焦点。Unity 基础转场同步刷新布局，不先等待一帧；自定义动画也应在首次等待前应用起点。

## 单窗口多级菜单：Modal + Stack

设置 → 画质 → 高级画质，每次只显示当前内容，返回一次恢复一个历史窗口。整个流程只打开一个 Modal；各级窗口使用独立资源，通过内部 Stack 推进。

```text
ModalContainerNode
└─ SettingsFlowWindow              ← 一个 Modal 外壳
   └─ PageMount                    ← StackContainerNode 挂载
      ├─ SettingsWindow            ← 历史实例，隐藏
      ├─ GraphicsWindow            ← 历史实例，隐藏
      └─ AdvancedGraphicsWindow    ← 当前显示
```

SettingsFlowWindow 资源的根挂载业务脚本，内部放一个空的 PageMount。各窗口资源根挂载自己的业务脚本；背景与内容样式由各窗口资源定义，外壳不必制作可见面板。

Godot 的全屏外壳和 PageMount 设 `MouseFilter.Ignore`，实际窗口面板接收输入；Unity 的空挂载不添加可拦截射线的 Graphic。这样窗口外的点击才能到达容器遮罩。

Godot 外壳脚本：

```csharp
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using UIFramework.Godot;
using UINode = UIFramework.Godot.UINode;

public partial class SettingsFlowWindow : UINode
{
    public StackContainerNode Pages { get; private set; } = null!;

    protected override async Task OnInitializeAsync(CancellationToken token)
    {
        Pages = await this.CreateContainerAsync<StackContainerNode>(
            "Pages", this.GetControl<Control>("PageMount"), token);
        await Pages.PushAsync<SettingsWindow>(
            playAnimation: false, cancellationToken: token);
    }
}
```

Unity 使用相同的属性和初始化方法；导入 `UnityEngine`、`UIFramework.Unity`，UINode 别名指向 `UIFramework.Unity.UINode`，挂载查询替换为：

```csharp
Pages = await this.CreateContainerAsync<StackContainerNode>(
    "Pages", this.GetControl<RectTransform>("PageMount"), token);
```

注册外壳及三个窗口资源后，业务流程为：

```csharp
var window = await modals.OpenAsync<SettingsFlowWindow>();
await window.Pages.PushAsync<GraphicsWindow>();
await window.Pages.PushAsync<AdvancedGraphicsWindow>();
await window.Pages.PopAsync(); // AdvancedGraphics → Graphics
await window.Pages.PopAsync(); // Graphics → Settings
```

返回按钮／Esc 由项目按历史决定回退还是关闭整个流程：

```csharp
if (window.Pages.History.Count > 1)
    await window.Pages.PopAsync();
else
    await modals.CloseAsync(window);
```

内部切换复用外层遮罩，不新增 Modal 层。关闭外壳会清理整个 Stack 和历史窗口。稳定状态只显示当前窗口；Stack 转场期间可能同时显示前后窗口，无需动画时传 `playAnimation: false`。

## 等待确认与多层返回

需要返回用户答案时，ConfirmView 继承对应引擎的 `DialogNode<bool>`。确认／取消按钮分别调用 `Complete(true)`／`Complete(false)`。

```csharp
var shop = await modals.OpenAsync<ShopView>();
await modals.OpenAsync<PurchaseView>();
bool confirmed = await modals.ShowDialogAsync<ConfirmView, bool>();
if (confirmed) player.BuySelectedItem();
await modals.CloseToAsync(shop);
```

ShowDialogAsync 等待确认窗口关闭及清理后返回答案。CloseToAsync 保留原商店并清理其上的弹窗；跳过的中间层不会逐层恢复交互。

## 行为与边界

- `OpenAsync` 每次创建并叠加新弹窗；没有“自动隐藏上一层”的选项。
- `CloseAsync` 只关闭当前顶层；`CloseToAsync` 目标必须是同一容器中存活的实例；当前目标正常完成。
- 下层窗口保留显示和状态，但不接受输入；恢复时不重播进入生命周期和动画。
- `Complete` 只能在可交互时调用一次。未回答就外部关闭会取消等待任务，不自动返回 false。
- 遮罩点击关闭通过 `DismissRequested` 配置；Esc 和项目返回入口由项目处理。
- 独立 Modal 容器不能同时拥有输入。相关弹窗放在同一个容器中。

[容器选择](concepts.md#按流程选择容器) · [Stack 历史](stacks.md) · [输入与焦点](input-and-focus.md) · [异步关闭](async-and-errors.md)

KeepAlive 支持[关闭后缓存](resources.md#关闭后缓存)；遮罩仍按原规则释放，Dialog 每次打开使用独立结果会话。

同一次打开的重复 `CloseAsync` 共享关闭任务，首个请求决定动画与执行取消；后续取消只停止自己的等待。已正常关闭的实例再次 Close 成功，关闭失败仍传播原错误。`ShowDialogAsync` 等待答案期间不占容器队列，其他弹窗可以正常打开。详见[操作协调](async-and-errors.md)。
