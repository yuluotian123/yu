extends SceneTree
## Change the river and its bank colliders without moving village placements.
const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
func _initialize() -> void:
	run.call_deferred()
func own_children(parent: Node, scene: Node) -> void:
	for child in parent.get_children():
		child.owner = scene
		if child.scene_file_path.is_empty(): own_children(child,scene)
func run() -> void:
	var village: Node3D = load(ENV).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	preload("res://scripts/tools/art/forest_village_whitebox/river_course.gd").new().build(village)
	own_children(village,village)
	var scene := PackedScene.new()
	assert(scene.pack(village) == OK)
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(scene,ENV)
	village.free()
	print("FOREST_RIVER_SAVED")
	quit()
