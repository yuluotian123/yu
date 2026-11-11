extends SceneTree
## Run with the patched Vulkan/Forward+ engine, without --headless.
func _initialize() -> void:
	root.add_child.call_deferred(load("res://scripts/tools/rendering/DdgiValidation.cs").new())
