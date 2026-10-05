# Getting Started

Start by creating a window: build its engine resource, then write the business node and host script. Continue with window instances, page history, transitions and resource preloading.

| Component | Current requirements / integration |
| --- | --- |
| Core | netstandard2.1; source ProjectReference or a built DLL |
| Godot | 4.7.2 .NET, .NET 10 SDK; reference Core and Godot projects |
| Unity | Compiled with 6000.3.24f1 and uGUI 2.0.0; prepare DLL then add the local UPM package |
| Generator | Optional Roslyn generator via an Analyzer project reference; not integrated into the Unity workflow |

Both tutorials start with a window you can open and close, and walk through its resource, script and host attachment.

[Godot tutorial](godot.md) · [Unity tutorial](unity.md) · [Next steps](next-steps.md)
