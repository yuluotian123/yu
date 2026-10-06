# Entity-Component Framework

## Hosts and components

The project uses entity-component composition, not a data-only ECS with separate systems.

- `scripts/gamelogic/gameobject/GameObject2D.cs` and `GameObject3D.cs` are the scene hosts. They own a typed exported `Components` array and runtime component instances.
- `Component2D` and `Component3D` are Godot `Resource` subclasses. Their `Owner` is respectively `GameObject2D` or `GameObject3D`; choose the component base by its host, not by the dimension of a child node it controls.
- Implement concrete scene-configurable behaviors as `[GlobalClass] public partial class ... : Component2D/Component3D`, in `GameLogic`. Native child nodes such as `Camera3D`, `Sprite3D`, `SubViewport`, `CharacterBody2D`, and `MeshInstance3D` remain scene data controlled by components.
- SpaceLevel gameplay uses native 3D hosts and physics. A 2D character means sprite artwork (`AnimatedSprite3D`), not a 2D physics host projected into 3D. Do not reintroduce a `SubViewport`, 2D collision proxy, or Camera2D-to-Camera3D bridge for this scene.

## Lifecycle and ownership

- Put setup and node/dependency resolution in `OnInit`, frame updates in `OnUpdate`, physics work in `OnPhysicsUpdate`, and cleanup in `OnDestroy`. Components do not implement `_Ready`, `_Process`, or `_PhysicsProcess`; their hosts dispatch these hooks.
- Resolve sibling behavior using `Owner.GetComponent<T>()`; configure scene dependencies through exported `NodePath` or resource properties. Use `Owner.GetNode...` for actual nodes.
- The hosts clone resource definitions where necessary and assign `Owner` before initialization. Treat exported resources as configuration. Keep mutable runtime state on each runtime instance, and duplicate mutable shared materials/shapes before modifying them for a single host.
- Hosts initialize components and process them in descending `Priority` order. Larger values run earlier. Prefer the named values in `ComponentPriority`; account for dependencies during initialization and physics/frame updates.
- `Priority` only orders components on the same host. Order different hosts via Godot process priority or explicit dependencies when needed (for example a camera host updating after character hosts).
- `IsActive` gates updates, not initialization or teardown. Hosts also support pooling through `OnSpawn`/`OnRecycle`; release signals and references and avoid accumulating runtime children on repeated activation.
- Register components in the scene's typed `Components` array, or with `Owner.AddComponent<T>()` for deliberately dynamic behavior. Do not construct a second update loop for a component.

## Scene wiring and migration

Player and AI use GameObject3D with Component3D resources. Movement controls a native CharacterBody3D child; animation controls an AnimatedSprite3D with the existing SpriteFrames. Platforms use StaticBody3D and CollisionShape3D. The camera component controls Camera3D directly.

CharacterMovementComponent3D.MovementSpace defaults to SideView (X/Y movement, lock current Z). Free3D enables X/Z movement with the same gravity/jump/collision implementation. Commands contain X/Z axes; normalize after applying the movement constraint. Keep input, AI targeting, directional abilities and animation speed compatible with both modes. Native world coordinates use meters, positive Y up; sprite pixel_size is an artwork scale only.

Graph component discovery, invocation and binding use IGameObject/IComponent, supporting both dimensional host types. Do not hard-code Component2D in shared graph infrastructure. Serialized graph component names, action references, animation paths and thresholds must migrate with renamed components. Keep script UIDs when renaming scripts.

Character persistence schema 3 stores XYZ world coordinates and facing direction. Older schema 1/2 positions convert once from pixels: (x * 0.01, -y * 0.01, 0). Never use the old conversion every frame. GameObject3D.PersistentId is exported to retain scene-authored save identities.

Before finishing, check scene component arrays, per-instance state, activation/cleanup, physics and frame ordering, and graph/save compatibility. Run `dotnet build yu.csproj` and relevant Godot smoke scenes. Test runners may inherit from `Node`; they test the EC implementation rather than replace it.
