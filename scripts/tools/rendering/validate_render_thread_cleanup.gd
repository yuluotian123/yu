extends SceneTree
## Run with the project's Godot 4.8 Vulkan build, not the headless dummy renderer.

func _initialize() -> void:
	root.add_child.call_deferred(load("res://scripts/tools/rendering/RenderThreadCleanupValidation.cs").new())
