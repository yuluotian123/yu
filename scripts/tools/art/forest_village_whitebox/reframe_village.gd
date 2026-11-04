extends SceneTree
## Targeted authoring: existing forest/river/house placements remain; update entries and their paving.
const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var saver := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()
	var village: Node3D = load(ENV).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var builder = load("res://scripts/tools/art/forest_village_whitebox/build_village.gd").new()
	builder.village = village
	builder.prepare_ground()
	var surface: MeshInstance3D = village.get_node("Paths/Surface")
	var arrays := surface.mesh.surface_get_arrays(0)
	var previous_wear := {}
	for i in arrays[Mesh.ARRAY_VERTEX].size():
		var at: Vector3 = arrays[Mesh.ARRAY_VERTEX][i]
		var grid := Vector2i(roundi((at.x+40.8)/.3),roundi((at.z+37.5)/.3))
		previous_wear[grid.y*273+grid.x] = arrays[Mesh.ARRAY_COLOR][i].a
	preload("res://scripts/tools/art/forest_village_whitebox/front_facing_houses.gd").new().apply(village,builder)
	surface.free()
	builder.build_paths(previous_wear)
	preload("res://scripts/tools/art/forest_village_whitebox/ground_flora.gd").new().build(village)
	builder.own_children(village,village)
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	saver.save_resource(packed,ENV)
	village.free()
	builder.free()
	print("FOREST_REFRAMED: six front entrances and connected paving; use replace_forest_level.gd to update the formal portal and navigation")
	quit()
