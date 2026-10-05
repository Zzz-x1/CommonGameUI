# Getting Started

## What the framework manages

UIFramework manages UI identity, hierarchy, open/return flows, lifecycle, input state and resource ownership. It does not draw widgets, unify engine layout files or create business layers. Your game still owns models, networking, saves and rewards.

## Environment and installation

| Component | Current requirements / integration |
| --- | --- |
| Core | netstandard2.1; source ProjectReference or a built DLL |
| Godot | 4.7.2 .NET, .NET 10 SDK; reference Core and Godot projects |
| Unity | Compiled with 6000.3.24f1 and uGUI 2.0.0; prepare DLL then add the local UPM package |
| Generator | Optional Roslyn generator via an Analyzer project reference; not integrated into the Unity workflow |

There is no public NuGet package or release download URL promised by this repository. Use the source. These versions describe the environment used here, not compatibility with every SDK or engine version.

## Two tutorial routes

1. [Godot tutorial](godot.md): complete scenes and host for menu→settings→categories→quit confirmation.
2. [Unity tutorial](unity.md): complete host and Inspector/Prefab setup for the same flow; native execution remains unverified.

Start with manual Register calls to understand resources and instances. Then read [Source Generator](../manual/source-generator.md). Short Manual snippets assume initialized ui, pages, windows, modals and tabs variables and project-defined View types. Full runnable hosts live in the tutorials rather than being repeated in every chapter.

[Next steps](next-steps.md)
