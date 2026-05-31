# Coding Patterns

## Godot C# Conventions

- Use `partial class` for Godot script classes that inherit from `Node`, `Resource`, or other Godot types.
- Use `[Export]` for properties that must be editable or serialized by Godot.
- Preserve Godot lifecycle methods, signal connections, and exported property names unless a migration is part of the task.
- Prefer Godot `res://` resource paths for project assets and resources.
- Use `ResourceLoader` or the project resource module according to nearby code.

## Project Boundaries

- Framework-level reusable systems belong under `scripts/framework` and usually use the `Framework` namespace.
- Game-specific behavior belongs under `scripts/gamelogic` and usually uses the `GameLogic` namespace.
- Reuse `ModuleSystem`, resource module APIs, FSM APIs, UI modules, mission systems, skill runtime, and AI helpers before adding new infrastructure.
- Match nearby file style for access modifiers, null handling, logging, and comments.

## Generated Config

- Treat `scripts/generated/config` as generated output.
- Do not manually patch generated config classes for behavior changes.
- If behavior must be extended, add a separate non-generated type, partial class, helper, or generator-side change depending on the existing pattern.

## Verification

- Run `dotnet build yu.csproj` after code changes when feasible.
- For gameplay runtime behavior, look for existing smoke tests under `scripts/test` or add focused Godot-side checks only when the task warrants it.
