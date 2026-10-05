# Game client Samples

Star Court Expedition is a UI-only client with local business data and saves, without networking or battle.

## Run

From the repository root:

```powershell
# Godot: .NET 10 SDK + Godot 4.7.2 .NET
./scripts/Get-Godot.ps1
./scripts/Run-Demo.ps1

# Unity: prepare DLL, then open Demo/Unity manually.
./scripts/Run-UnityDemo.ps1 -PrepareOnly
```

Godot scene: Scenes/Client/Main.tscn. Unity scene: Assets/Scenes/Client.unity. See [Unity integration](../integrations/unity.md) for status.

## Try these flows

| Path | Capability |
| --- | --- |
| Title→lobby | Resource preparation, lifecycle and timeline entry |
| Inventory→details→back | Page history and retained instances |
| Settings→graphics→back | Nested modals and input restoration |
| Shop→confirmation→rewards | Awaited answer, business commit and presentation |
| Equipment comparison | Multiple floating instances |
| Inventory↔shop, mail claims | Source-based animation on the same screen (Godot) |
| Fold/expand event goals | Retained embedded pages (Godot) |

Godot includes characters, inventory, team, stages, shop, tasks, mail, friends, events, profile and settings. Unity covers characters, inventory, team, shop, tasks and settings.

## Examples and tests

- Small tutorial: docs/examples/godot. Unity host: docs/examples/unity.
- Full checks: `./scripts/Test.ps1`; tests use isolated profiles.
- Each Demo contains operation instructions in README.md and attribution in Assets/SOURCES.md.

[Recipes](game-ui-recipes.md) · [Timelines](../manual/timeline-animation.md)

Both clients use MVVM for shared currency, private equipment details, two-way settings, purchase commands and inventory rows. See [MVVM setup](../manual/mvvm.md).
