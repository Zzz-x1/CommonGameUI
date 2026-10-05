# Game client Samples

## Star Court Expedition: Godot

From the repository root run `./scripts/Get-Godot.ps1` to acquire the fixed portable engine, then `./scripts/Run-Demo.ps1`. Supply `-GodotPath` for your own installation. Sources live in Demo/Godot, with HierarchyDemo.csproj and project.godot. Scripts need PowerShell and the .NET 10 SDK.

The client includes local login, lobby, six characters, equipment/training, inventory, teams, stage deployment confirmation, tasks/achievements, shop, mail, friends, events, profile and settings. It is pure UI without networking or battle. Saves use user://client-save.json; tests use isolated profiles.

Watch lobby entry, training and rewards; their scenes contain editable AnimationPlayer tracks. Try inventory↔shop and mail attachments versus shop rewards to inspect source-dependent animation. Full/reduced presentation, skipping, folding categories and continuous item imagery are integrated into actual game flows.

`./scripts/Test.ps1` runs Core, generator, Godot headless and separate-process restart verification. Run engine tests alone using `./scripts/Run-Demo.ps1 -SelfTest -Headless`. Asset attribution lives in Demo/Godot/Assets/SOURCES.md; do not assume it licenses unrelated assets.

## Unity Demo

Run `./scripts/Run-UnityDemo.ps1 -PrepareOnly` to prepare the core DLL, then open Demo/Unity manually. Its scene is Assets/Scenes/Client.unity. It covers lobby, characters, equipment, inventory, team, shop, tasks and settings, not every Godot module.

Timeline asset verification remains pending and recent adapter changes have offline compilation only. Read [Unity integration](../integrations/unity.md) first. This documentation adds no native verification claim and starts no editor automatically.

## Standalone tutorials

Build and run docs/examples/godot. docs/examples/unity supplies the complete host; author Prefabs following the [Unity tutorial](../getting-started/unity.md). Neither accesses game saves.

[Recipes](game-ui-recipes.md) · [Timelines](../manual/timeline-animation.md)
