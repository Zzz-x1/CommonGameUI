# Getting Started

Start with manual registration for menu/settings, then add animation, preloading and generation as needed.

| Component | Current requirements / integration |
| --- | --- |
| Core | netstandard2.1; source ProjectReference or a built DLL |
| Godot | 4.7.2 .NET, .NET 10 SDK; reference Core and Godot projects |
| Unity | Compiled with 6000.3.24f1 and uGUI 2.0.0; prepare DLL then add the local UPM package |
| Generator | Optional Roslyn generator via an Analyzer project reference; not integrated into the Unity workflow |

Integrate from source. Short Manual snippets assume initialized containers and project-defined Views; tutorials provide complete code.

[Godot tutorial](godot.md) · [Unity tutorial](unity.md) · [Next steps](next-steps.md)
