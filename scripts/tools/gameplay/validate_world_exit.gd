extends SceneTree
## In-memory checkpoint regression; does not read or write player save slots.
func _initialize() -> void:
	root.add_child.call_deferred(load("res://scripts/tools/gameplay/WorldExitValidation.cs").new())
