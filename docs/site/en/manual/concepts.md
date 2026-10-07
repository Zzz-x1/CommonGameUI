# Concepts and responsibilities

A Node can represent a screen or simply organize the hierarchy. A Container is a Node with opening, switching or return operations.

Business UI scripts inherit `UIFramework.Unity.UINode` (MonoBehaviour) or `UIFramework.Godot.UINode` (Control) and attach to the resource root. Core `UINode` provides Root, containers and renderer-independent extensions. Containers return the actual business script; `IUINode` represents shared identity and state.

## Choose by the flow

Business classes may be named Window, View or Page. Choose the container by its display and return behavior.

| Need and example | Container | Main operations | State and history |
| --- | --- | --- | --- |
| Open inventory independently or show two equipment comparison cards | [PanelContainerNode](panels.md) | Open／Hide／Show／Close | Independent windows; no return history |
| Settings → graphics → advanced graphics, one current window with stepwise Back | [StackContainerNode](stacks.md) | Push／Pop／PopTo | Retains and hides history windows; Back restores the original instance |
| Equipment/material categories authored together in one resource | [TabContainerNode](tabs.md) | Bind／Switch／Hide | Selects bound items; no return history |
| Confirmation over product details, retaining the background but blocking its input | [ModalContainerNode](modals-and-dialogs.md) | Open／Close／CloseTo／ShowDialog | Lower modals stay visible; the top owns input |

**Use Stack for one current window with multiple history levels. For a modal backdrop, host that Stack inside one Modal shell.**

## Create and mount

UIContext provides registration, resource services and a default Root. The project creates containers and mounts them on its controls. Put this example in the host initialization method:

```csharp
var ui = UI.Create(this);
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
```

In Godot, import `UIFramework` and `UIFramework.Godot`; pageMount is a `Control`. In Unity, import `UIFramework` and `UIFramework.Unity`; pageMount is a `RectTransform`. Complete hosts are in [Godot Getting Started](../getting-started/godot.md) and [Unity Getting Started](../getting-started/unity.md).

- Containers are Nodes and may be children of other nodes. Call their operations directly.
- Register business types with `ui.Register<T>(resource)` before opening resources. Binding existing Tab controls needs no resource registration.
- Root is structural. The project defines container names, mounts, layout and drawing order.
- Disposing a container or its owning window cleans the subtree; KeepAlive close retains it. See [closed-instance caching](resources.md#closed-instance-caching). Await asynchronous operations and observe their errors in project code.

[Hierarchy](hierarchy.md) · [API](../api/core.md)
