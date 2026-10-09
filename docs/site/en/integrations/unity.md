# Unity integration

Follow [Unity Getting Started](../getting-started/unity.md) for setup.

## Resources and controls

- Prefab roots use RectTransform and a business script inheriting the adapter UINode (MonoBehaviour) under Resources; register paths without extensions.
- Mounts/roots have no raycastable full-screen Graphic. Modal creates the backdrop.
- Tabs bind existing RectTransforms without cloning. GetRoot/GetControl access native controls.

```csharp
var ui = UI.Create(canvas, eventSystem);
ui.Register<InventoryView>("UI/Inventory");
var pages = await ui.Root.CreateContainerAsync<StackContainerNode>("Pages", pageMount);
pages.ChildTransition = Transitions.SlideHorizontal(targetPath: "Content");
var inventory = await pages.PushAsync<InventoryView>();
Button close = inventory.GetControl<Button>("Content/Close");
```

## Lifecycle and animation

- Create configures a hidden persistent frame pump. Use BindLifetime for a Scene or GameObject; see [Lifecycle](../manual/lifecycle.md).
- Input gates preserve business interactable state. EventSystem configures keyboard navigation.
- Supports Timeline/PlayableDirector and non-looping legacy AnimationClips, both using unscaled time.
- Resources requests cannot be canceled directly; releasing references does not immediately clear engine caches.

## Debugging the framework in Visual Studio

Generate and synchronize Debug DLLs and matching PDBs from the repository root:

```powershell
./scripts/Prepare-Mvvm.ps1 -Configuration Debug
```

After Unity reloads the assemblies, open this repository's source in VS, set breakpoints, select **Debug → Attach Unity Debugger**, connect to the project using the package, and trigger a UI operation. Disable Just My Code if stepping skips framework code; use Debug code optimization in the Unity Editor.

DLLs and PDBs are copied into the existing packages' Plugins directories; no source package is required. Source must match the compiled version. Rerun the command after changes. Also pass `-Configuration Debug` when using `Run-UnityDemo.ps1` so it does not prepare Release assemblies instead. Use `-Configuration Release` to restore Release; both configurations synchronize matching PDBs.

## Verification status

Current environment: Unity 6000.3.24f1/uGUI 2.0.0. The client, adapters, lifetime, saved Timeline and AnimationClip resources passed 31 native Play Mode tests. MVVM editor Undo/Prefab overrides and preview were verified. The standalone tutorial scripts compile in Unity 6000.3.24f1 batch mode; native UI execution of the tutorial remains unverified. The Unity Source Generator workflow is not integrated.

[Unity API](../api/unity.md) · [Timelines](../manual/timeline-animation.md)
