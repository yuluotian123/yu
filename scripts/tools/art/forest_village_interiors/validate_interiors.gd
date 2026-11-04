extends SceneTree
func _initialize() -> void:
	run.call_deferred()
func run() -> void:
	root.add_child(load("res://scripts/tools/art/forest_village_interiors/ForestInteriorValidation.cs").new())
