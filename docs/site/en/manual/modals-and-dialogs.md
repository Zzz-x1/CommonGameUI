# ModalContainerNode: modals and result dialogs

Use it for windows that temporarily block background input, such as purchase confirmation over product details or quit confirmation. The top modal owns input. When another modal opens, **lower modals retain their instances and remain visible**. Closing it restores input and focus to the previous modal.

Use [Stack](stacks.md) for one current window with stepwise history. Combine one Modal shell with an internal Stack when you need both a backdrop and history, as shown below.

## Resources and backdrops

Put window content in the resource. The container creates the backdrop; do not recreate it inside each window. modals is a mounted `ModalContainerNode`; `Observe` is the project's asynchronous task observer.

```csharp
modals.ConfigureBackdrop(Backdrops.Dim(Observe), ModalBackdropMode.Shared);
var details = await modals.OpenAsync<ProductDetailsView>();
var confirm = await modals.OpenAsync<PurchaseConfirmView>();
await modals.CloseAsync(confirm);
```

Confirmation is destroyed by default or cached with KeepAlive; product details retain the same instance. `Shared` reuses one backdrop beneath the current modal. `PerModal` creates a backdrop per window, allowing the background to become darker at each level. Neither mode automatically hides lower windows.

### Animation timing

After window initialization, backdrop creation and entry preparation, the window and new backdrop play their entry animations together. On close, the window and any backdrop being removed also animate together. The operation waits for both before committing input/focus or disposing resources.

A Shared backdrop remains when a lower modal still uses it, without replaying its fade. `playAnimation: false` disables both window and backdrop animations. A failure cancels the other animation, awaits cleanup, restores state and propagates the original exception.

## One window with multiple menu levels: Modal + Stack

Settings → graphics → advanced graphics shows only the current content, and each Back restores one history window. Open one Modal for the whole flow. Each menu window uses a separate resource and is pushed into its internal Stack.

```text
ModalContainerNode
└─ SettingsFlowWindow              ← one Modal shell
   └─ PageMount                    ← StackContainerNode mount
      ├─ SettingsWindow            ← retained history, hidden
      ├─ GraphicsWindow            ← retained history, hidden
      └─ AdvancedGraphicsWindow    ← current window
```

Attach the business script to the SettingsFlowWindow resource root and place an empty PageMount inside it. Each window resource has its own root business script and defines its own background/content styling. The shell does not need a visible panel.

In Godot, set the full-screen shell and PageMount to `MouseFilter.Ignore`; actual window panels receive input. In Unity, do not add a raycast-blocking Graphic to empty mounts. This lets clicks outside the window reach the container's backdrop.

Godot shell script:

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

Unity uses the same property and initialization method. Import `UnityEngine` and `UIFramework.Unity`, point the UINode alias to `UIFramework.Unity.UINode`, and replace the mount lookup with:

```csharp
Pages = await this.CreateContainerAsync<StackContainerNode>(
    "Pages", this.GetControl<RectTransform>("PageMount"), token);
```

After registering the shell and the three window resources, the flow is:

```csharp
var window = await modals.OpenAsync<SettingsFlowWindow>();
await window.Pages.PushAsync<GraphicsWindow>();
await window.Pages.PushAsync<AdvancedGraphicsWindow>();
await window.Pages.PopAsync(); // AdvancedGraphics → Graphics
await window.Pages.PopAsync(); // Graphics → Settings
```

The project routes Back/Esc according to history, either stepping back or closing the whole flow:

```csharp
if (window.Pages.History.Count > 1)
    await window.Pages.PopAsync();
else
    await modals.CloseAsync(window);
```

Internal switching reuses the outer backdrop without adding Modal layers. Closing the shell cleans the Stack and all history windows. Only the current window is visible in a stable state. Stack transitions may show both windows; pass `playAnimation: false` when no animation is needed.

## Await confirmation and return across layers

To return an answer, ConfirmView inherits the engine's `DialogNode<bool>`. Confirm/Cancel buttons call `Complete(true)`/`Complete(false)` respectively.

```csharp
var shop = await modals.OpenAsync<ShopView>();
await modals.OpenAsync<PurchaseView>();
bool confirmed = await modals.ShowDialogAsync<ConfirmView, bool>();
if (confirmed) player.BuySelectedItem();
await modals.CloseToAsync(shop);
```

ShowDialogAsync returns the answer after the confirmation window closes and cleanup completes. CloseToAsync retains the original shop and cleans the modals above it; skipped intermediate layers do not regain input one by one.

## Behavior and boundaries

- `OpenAsync` creates and layers a new modal each time; there is no automatic hide-previous-layer option.
- `CloseAsync` closes only the current top. `CloseToAsync` requires a live earlier instance in the same container.
- Lower windows retain their display and state but receive no input. Restoration does not replay entry lifecycle or animation.
- `Complete` is allowed once while interactive. Closing an unanswered dialog cancels its waiter rather than returning false.
- Configure `DismissRequested` for backdrop dismissal. The project handles Esc and its Back action.
- Independent Modal containers cannot own input simultaneously. Put related modals in the same container.

[Container selection](concepts.md#choose-by-the-flow) · [Stack history](stacks.md) · [Input and focus](input-and-focus.md) · [Async shutdown](async-and-errors.md)

KeepAlive enables [closed-instance caching](resources.md#closed-instance-caching); backdrops still follow normal release rules, and each Dialog opening uses its own result session.
