# Project Map

## Runtime and Build

- Godot project name: `yu`.
- Godot.NET SDK: `Godot.NET.Sdk/4.6.1`.
- Default target framework: `net8.0`.
- Android target framework: `net9.0` when `GodotTargetPlatform` is `android`.
- Build from the repository root with `dotnet build yu.csproj`.

## Key Paths

- `project.godot`: Godot project settings.
- `yu.csproj`: C# project settings and package references.
- `scripts/framework`: reusable engine/framework systems.
- `scripts/gamelogic`: game-specific systems, components, runtime behavior, AI, missions, skills, input, UI, and procedures.
- `scripts/generated/config`: generated config row classes. Do not edit generated output directly.
- `scripts/test`: smoke tests and test helpers intended to run inside or alongside Godot scenes.
- `assets`: Godot assets, scenes, resources, and imported content.

## Common Namespaces

- `Framework`: core modules and reusable framework services.
- `Framework.UI`: UI framework layer.
- `GameLogic`: gameplay code, components, AI, missions, skills, input, and procedures.
- `Generated.Config`: generated configuration classes.

## Subsystem Pointers

- FSM: `scripts/framework/fsm`.
- Resource loading and handles: `scripts/framework/resource`.
- UI module and widgets: `scripts/framework/ui` and `scripts/gamelogic/ui`.
- Missions: `scripts/gamelogic/missions`.
- Skills: `scripts/gamelogic/skills`.
- AI: `scripts/gamelogic/ai`.
- Config conversion/runtime: `scripts/framework/config` and `scripts/generated/config`.
