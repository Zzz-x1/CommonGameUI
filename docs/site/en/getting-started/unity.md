# Unity Getting Started

This tutorial starts with a window that has a title and a close button. When finished, a scene button opens it; closing and opening it again creates a new instance.

## 1. Prepare the project and framework

Install Unity 6000.3.24f1 and the .NET 10 SDK. Create a project and a `Main` scene. In the scene, choose **GameObject → UI → Canvas** and keep its CanvasScaler and GraphicRaycaster. Download this repository and prepare its local Unity package from the repository root:

```powershell
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

In Unity Package Manager, choose **Add package from disk** and select `src/UIFramework.Unity/package.json` in the repository. The package references Core and uGUI.

## 2. Create the window Prefab

Build the Prefab in this order:

1. Select Canvas in the Hierarchy, right-click, then choose **UI → Panel**. Name the new object `HelloWindow`.
2. Select its root RectTransform and choose the four-sided Stretch preset. Set Left, Top, Right and Bottom to 0. Remove the root Image so a full-screen object does not block scene input.
3. Create **UI → Panel** under `HelloWindow` and name it `Content`. Set its RectTransform to centered anchors, Pos X/Y 0, Width 420 and Height 240. Keep its Image.
4. Create **UI → Legacy → Text** under Content and name it `Title`. Set its RectTransform anchors to Center, Width=360, Height=56, Pos X=0 and Pos Y=45. Set the text to `Hello, UIFramework!` and center its horizontal/vertical alignment.
5. Create **UI → Button** under Content and name it `Close`. Set its RectTransform anchors to Center, Width=140, Height=44, Pos X=0 and Pos Y=-65. Change its child Text to `Close`.

```text
HelloWindow (RectTransform, Stretch)
└── Content (Image, centered 420×240)
    ├── Title (Text: "Hello, UIFramework!")
    └── Close (Button)
```

Drag `HelloWindow` into `Assets/Resources/UI/HelloWindow.prefab`, then delete the temporary scene instance. The resource root has no Image; the visible `Content` panel receives mouse input.

## 3. Write the window script

Create `HelloWindow.cs`. `EngineUI.UINode` derives from MonoBehaviour; `EngineUI` aliases the `UIFramework.Unity` namespace. After compilation, open the window Prefab, attach HelloWindow to its root and drag Content/Close into the Close Button field. The serialized reference continues to identify the control after renaming or reparenting.

```csharp
using System.Threading;
using System.Threading.Tasks;
using UIFramework;
using UnityEngine;
using UnityEngine.UI;
using EngineUI = global::UIFramework.Unity;

public sealed class HelloWindow : EngineUI.UINode
{
    [SerializeField] private Button closeButton = null;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        closeButton.onClick.AddListener(() => Observe(((PanelContainerNode)Parent).CloseAsync(this)));
        return Task.CompletedTask;
    }
}
```

## 4. Build the Main scene and write its host

Delete the temporary `HelloWindow` instance from the scene after saving its Prefab. Under Canvas, add a Button named `OpenWindowButton`; use bottom-center anchors, Width 180, Height 44, Pos Y 40, and the label `Open Window`.

Set Canvas to Screen Space - Overlay. Set CanvasScaler to **Scale With Screen Size** with a reference resolution of `1000 × 700`. The scene needs an EventSystem; if Unity created one when you added a Button, keep that object.

```text
Canvas (Canvas, CanvasScaler, TutorialHost)
└── OpenWindowButton (Button)
EventSystem
```

The host creates a Context and Panel container, registers the Prefab, and opens the window at startup. A later `OpenAsync` reuses the current single instance; after it closes, the next click creates a new instance. Copy the full script into `TutorialHost.cs`:

<details>
<summary>Expand the complete TutorialHost.cs</summary>

```csharp
using EngineUI = global::UIFramework.Unity;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Unity;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class TutorialHost : MonoBehaviour
{
    public Canvas Canvas;
    public EventSystem Events;
    public Button OpenWindowButton;

    private readonly List<Task> _tasks = new List<Task>();
    private UIContext _ui;
    private PanelContainerNode _windows;

    private void Start() => Observe(InitializeAsync());

    private async Task InitializeAsync()
    {
        _ui = EngineUI.UI.Create(Canvas, Events);
        _ui.BindLifetime(gameObject);
        _windows = await _ui.Root.CreateContainerAsync<PanelContainerNode>("Windows");
        _ui.Register<HelloWindow>("UI/HelloWindow");
        await _windows.OpenAsync<HelloWindow>();
        OpenWindowButton.onClick.AddListener(OpenWindow);
        OpenWindowButton.interactable = true;
    }

    private void OpenWindow()
    {
        OpenWindowButton.interactable = false;
        Observe(OpenWindowAsync());
    }

    private async Task OpenWindowAsync()
    {
        await _windows.OpenAsync<HelloWindow>();
        OpenWindowButton.interactable = true;
    }

    private void Observe(Task task) => _tasks.Add(task);

    private void Update()
    {
        for (var i = _tasks.Count - 1; i >= 0; i--)
        {
            var task = _tasks[i];
            if (!task.IsCompleted) continue;
            _tasks.RemoveAt(i);
            task.GetAwaiter().GetResult();
        }
    }
}
```

</details>

## 5. Attach the script and assign scene objects

Attach `TutorialHost.cs` to the Canvas. Assign these references in the Inspector:

| Field | Object |
| --- | --- |
| Canvas | The scene Canvas |
| Events | The scene EventSystem |
| OpenWindowButton | OpenWindowButton under Canvas |

Set OpenWindowButton's Interactable value off initially. Confirm that the window Prefab root has a HelloWindow component and its Close Button reference is assigned. The container loads the Prefab and returns its business script; it does not call `new HelloWindow()`.

## 6. Run and check

Enter Play Mode. The window should appear at startup. Close destroys it; Open Window creates it again. Clicking Open Window repeatedly while it is open does not stack duplicate windows.

The host observes every asynchronous button operation, so Unity Console receives the original exception on failure. Destroying the scene Canvas cleans up its Context.

[Next steps](next-steps.md) · [Panel instances](../manual/panels.md) · [Lifecycle](../manual/lifecycle.md)
