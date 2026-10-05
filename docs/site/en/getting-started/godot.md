# Godot Getting Started

This tutorial starts with a window that has a title and a close button. When finished, a scene button opens it; closing and opening it again creates a new instance.

## 1. Create a project and reference the framework

Install Godot 4.7.2 .NET and the .NET 10 SDK, then create a Godot .NET project. Add references to Core and the Godot adapter in the project `.csproj`. These relative paths apply to `docs/examples/godot` inside this repository; adjust them for your project:

```xml
<ItemGroup>
  <ProjectReference
    Include="../../../src/UIFramework.Core/UIFramework.Core.csproj" />
  <ProjectReference
    Include="../../../src/UIFramework.Godot/UIFramework.Godot.csproj" />
</ItemGroup>
```

Create `HelloWindow.cs` and `TutorialHost.cs` in the project root. The repository's `docs/examples/godot` folder contains the finished scenes for reference; this tutorial builds `HelloWindow.tscn` and `Main.tscn` step by step in the editor.

## 2. Create the HelloWindow scene

Create the scene in this order:

1. Create a Control scene and save it as `HelloWindow.tscn`. Set the root Control layout to Full Rect and Mouse Filter to Ignore.
2. Add a Panel under the root and name it `Content`. Set Layout to Center and Offsets to Left=-210, Top=-120, Right=210, Bottom=120 for a 420×240 panel. Keep Mouse Filter at Stop.
3. Add a Label under Content and name it `Title`; set Text to `Hello, UIFramework!`. Set its rectangle to Left=20, Top=28, Right=400, Bottom=84, and center both horizontal and vertical alignment.
4. Add a Button under Content and name it `Close`; set Text to `Close`. Set its rectangle to Left=140, Top=154, Right=280, Bottom=198.

```text
HelloWindow (Control, Full Rect, Mouse Filter=Ignore)
└── Content (Panel, centered 420×240)
    ├── Title (Label: "Hello, UIFramework!")
    └── Close (Button)
```

Keep Mouse Filter=Stop on `Content` so the panel receives input. The script expects the relative paths `Content/Title` and `Content/Close`; use those node names. Save the scene as `HelloWindow.tscn`.

## 3. Write the window script

Create `HelloWindow.cs`. `EngineUI.UINode` derives from Control; `EngineUI` aliases `UIFramework.Godot`. After building, attach the script to the HelloWindow scene root and drag its Close button into the Inspector's Close Button field. The framework manages that scene script directly, and the button is a native reference.

```csharp
using System.Threading;
using System.Threading.Tasks;
using Godot;
using UIFramework;
using EngineUI = global::UIFramework.Godot;

public partial class HelloWindow : EngineUI.UINode
{
    [Export] public Button CloseButton { get; set; } = null!;

    protected override Task OnInitializeAsync(CancellationToken token)
    {
        CloseButton.Pressed += () => Observe(((PanelContainerNode)Parent!).CloseAsync(this));
        return Task.CompletedTask;
    }
}
```

## 4. Create the Main scene and its host

Create a Control scene and save it as `Main.tscn`. Add a `Windows` Control under the root; set its layout to Full Rect and Mouse Filter to Ignore. Add a Button named `OpenWindow`; set Layout to Bottom Center, Offsets to Left=-90, Top=-64, Right=90, Bottom=-20, Text to `Open window`, and enable Disabled initially.

```text
Main (Control, TutorialHost.cs)
├── Windows (Control, Full Rect, Mouse Filter=Ignore)
└── OpenWindow (Button)
```

The host creates a Context and Panel container, registers the window scene, and opens it on startup. `OpenAsync` reuses the current single instance; after closing it, another click creates a new one. Save the complete code below as `TutorialHost.cs`:

<details>
<summary>Expand the complete TutorialHost.cs</summary>

```csharp
using EngineUI = global::UIFramework.Godot;
using Godot;
using System.Collections.Generic;
using System.Threading.Tasks;
using UIFramework;
using UIFramework.Godot;

public partial class TutorialHost : Control
{
    private readonly List<Task> _tasks = new();
    private Button _openWindowButton = null!;
    private PanelContainerNode _windows = null!;

    public UIContext UI { get; private set; } = null!;
    public Task Initialization { get; private set; } = null!;

    public override void _Ready()
    {
        Initialization = InitializeAsync();
        Observe(Initialization);
    }

    private async Task InitializeAsync()
    {
        _openWindowButton = GetNode<Button>("OpenWindow");
        UI = EngineUI.UI.Create(this);
        UI.BindLifetime(this);
        _windows = await UI.Root.CreateContainerAsync<PanelContainerNode>("Windows", GetNode<Control>("Windows"));
        UI.Register<HelloWindow>("res://HelloWindow.tscn");
        await _windows.OpenAsync<HelloWindow>();
        _openWindowButton.Pressed += OpenWindow;
        _openWindowButton.Disabled = false;
    }

    private void OpenWindow()
    {
        _openWindowButton.Disabled = true;
        Observe(OpenWindowAsync());
    }

    private async Task OpenWindowAsync()
    {
        await _windows.OpenAsync<HelloWindow>();
        _openWindowButton.Disabled = false;
    }

    private void Observe(Task task) => _tasks.Add(task);

    public override void _Process(double delta)
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

## 5. Attach the script and set the main scene

Attach `TutorialHost.cs` to the root Control in Main. Confirm that the HelloWindow scene root has `HelloWindow.cs` attached and its Close Button field references the close button. Match the node names to the `GetNode` paths in the script, then set `Main.tscn` as the main scene in Project Settings → Application → Run.

## 6. Run and check

Run the project. The window appears first. Click Close to destroy the instance; click OpenWindow to load the resource and create a new one. While it is open, another `OpenAsync` call reuses the same single instance.

The host observes button tasks each frame, and failures propagate as their original exceptions. The lifetime binding disposes the Context when Main leaves the scene tree.

[Next steps](next-steps.md) · [Panel instances](../manual/panels.md) · [Lifecycle](../manual/lifecycle.md)
