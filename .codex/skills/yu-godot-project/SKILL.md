---
name: yu-godot-project
description: Work effectively in the yu Godot C# repository. Use when Codex is modifying or explaining this project's GameObject/Component EC framework, gameplay, resources, FSM, UI, missions, AI, generated config, build setup, or Godot/C# code under scripts, assets, project.godot, or yu.csproj.
---

# Yu Godot Project

## Overview

Use this skill to get oriented in the yu Godot.NET project before changing code. Prefer existing project patterns, module boundaries, and Godot C# conventions over introducing new structure.

## Entity-Component Architecture

- This project's gameplay uses an entity-component (EC) framework. Keep behavior in `Component2D` or `Component3D` resources attached to `GameObject2D` or `GameObject3D` hosts.
- Do not implement gameplay, presentation synchronization, platform behavior, or camera control as standalone scripts inheriting directly from `Node`, `Node2D`, `Node3D`, `Camera3D`, or physics bodies. Use components to control those native child nodes. Preserve the EC structure during 2D/3D migrations.
- SpaceLevel uses native 3D physics with 2D sprite artwork. Keep SideView and Free3D as movement constraints on the same Component3D implementation; do not restore the former 2D-to-3D projection bridge.
- Before changing a host, component, or gameplay scene, read [references/ec-framework.md](references/ec-framework.md) for lifecycle, ordering, ownership, and scene wiring.
- This rule concerns gameplay behavior. Existing framework infrastructure, editor plugins, test runners, and UI framework classes may retain their required Godot/framework bases.

## Workflow

- Start by searching with `rg` and reading the nearby implementation before editing.
- Load `references/project-map.md` when you need the project layout, build targets, namespaces, or subsystem entry points.
- Load `references/coding-patterns.md` when adding or changing C# code, Godot nodes/resources, generated config, or framework integrations.
- Keep framework code under `Framework` boundaries and gameplay code under `GameLogic` unless existing code shows a more specific pattern.
- Reuse existing modules and helpers before adding new services or abstractions.
- After code changes, run `dotnet build yu.csproj` from the repository root when feasible.

## Editing Guidance

- Do not hand-edit generated config files under `scripts/generated/config`; extend behavior outside generated output.
- Preserve Godot resource paths and serialized exported members unless the task explicitly requires migration.
- Keep comments concise and useful. The project already contains a mix of Chinese and English comments; follow nearby file style.
- For tests or smoke checks, prefer existing `scripts/test` patterns and project-local build commands.
