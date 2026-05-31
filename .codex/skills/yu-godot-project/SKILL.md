---
name: yu-godot-project
description: Work effectively in the yu Godot C# repository. Use when Codex is modifying or explaining this project's framework, gameplay, resources, FSM, UI, missions, AI, generated config, build setup, or Godot/C# code under scripts, assets, project.godot, or yu.csproj.
---

# Yu Godot Project

## Overview

Use this skill to get oriented in the yu Godot.NET project before changing code. Prefer existing project patterns, module boundaries, and Godot C# conventions over introducing new structure.

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
