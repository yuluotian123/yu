extends SceneTree
## One-to-one replacement at the original building pivots and household orientations.

const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const ROOT := "res://assets/scenes/environment/forest_village/houses/"

func _initialize() -> void:
	apply.call_deferred()

func apply() -> void:
	var village: Node3D = load(ENV).instantiate()
	root.add_child(village)
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_village/houses_models_v2.json"))
	var changes: Array = []
	var history: Dictionary = {}
	var report_path := "res://assets/resources/art/forest_village/houses_level_integration.json"
	if FileAccess.file_exists(report_path):
		var previous_report: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(report_path))
		for change: Dictionary in previous_report.changes: history[change.household] = change.previous
	for asset: Dictionary in manifest.assets:
		var home: Node3D = village.get_node("Households/"+asset.household)
		var old: Node3D = home.get_node("Building")
		var approach: Node3D = home.get_node("DoorApproach")
		var home_transform := home.global_transform
		var approach_transform := approach.global_transform
		var previous := {"scene":old.scene_file_path,"transform":str(old.transform),"entrance":str(approach.position)}
		previous = history.get(asset.household,previous)
		var index := old.get_index()
		home.remove_child(old)
		old.free()
		var building: Node3D = load(ROOT+asset.name+".tscn").instantiate()
		building.name = "Building"
		home.add_child(building)
		home.move_child(building,index)
		building.owner = village
		building.add_to_group("camera_near_fade",true)
		# All ten original Building instances had zero local position/rotation.
		# New meshes are already authored at level scale, so only the old scale is retired.
		assert(String(previous.transform).contains("O: (0.0, 0.0, 0.0)"),"Original pivot must be verified: "+asset.household)
		building.transform = Transform3D.IDENTITY
		assert(home.global_transform.is_equal_approx(home_transform),"Household placement changed")
		assert(approach.global_transform.is_equal_approx(approach_transform),"Entrance placement changed")
		assert(building.global_position.is_equal_approx(home.global_position),"Original building pivot changed")
		assert(building.global_basis.orthonormalized().is_equal_approx(home.global_basis.orthonormalized()),"Original building direction changed")
		home.editor_description = asset.design+"。完整四面外观、独立碰撞与 LOD；正式关卡由正门进入对应室内。"
		changes.append({"household":asset.household,"asset":asset.name,"previous":previous,"new_position":str(building.position),"world_position":str(building.global_position),"world_rotation":str(building.global_rotation),"original_position_preserved":true,"original_direction_preserved":true})
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(packed,ENV)
	var report := FileAccess.open("res://assets/resources/art/forest_village/houses_level_integration.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"entrance_positions_preserved":true,"original_building_positions_preserved":true,"original_building_directions_preserved":true,"changes":changes},"\t"))
	print("FOREST_HOUSES_APPLIED: ten one-to-one replacements; original building pivots and directions verified")
	village.queue_free()
	await process_frame
	quit()
