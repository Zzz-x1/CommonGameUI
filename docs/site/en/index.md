# UIFramework Documentation

A renderer-independent C# UI management framework for games. Nodes and focused containers manage windows, page history, embedded tabs and dialogs. Your engine still owns widgets, layout and rendering.

## Start here

| Entry | What you will learn |
| --- | --- |
| [Getting Started](getting-started/index.md) | Build a menu and settings flow in Godot or Unity |
| [Manual](manual/concepts.md) | Understand instances, lifecycle, resources and animation through game scenarios |
| [Integrations](integrations/custom-engine.md) | Engine adapters and custom resource backends |
| [Samples](samples/index.md) | Run the Star Court client and combine common UI flows |
| [API Reference](api/index.md) | Check signatures, defaults and operation semantics |
| [Troubleshooting](troubleshooting.md) | Resolve integration errors and understand current limits |

## Choose a container

Use Panel for independent inventory windows, Stack for lobby→inventory→details→back, Tab for authored audio/graphics categories, and Modal/Dialog for graphics settings or purchase confirmation. Operations live directly on each container; there are no intermediate Navigation or Selection objects.

Only the structural Root is supplied. Business classes can be named InventoryView; mounts, layers and return policies belong to your project.

The Godot Demo uses 4.7.2 .NET; Core targets netstandard2.1. Unity source compiles against 6000.3.24f1, while native verification of recent changes and Demo timeline assets remains pending. Other engines require adapters and their own resources.

[简体中文文档](../index.md)
