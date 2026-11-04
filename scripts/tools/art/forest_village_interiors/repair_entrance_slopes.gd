extends SceneTree
## Bury each stair ramp's thin toe below the floor so a box character cannot snag it.
const HOUSES := "res://assets/scenes/environment/forest_village/houses/"
const MANIFEST := "res://assets/resources/art/forest_village/houses_models_v2.json"
func _initialize() -> void:
	run.call_deferred()
func run() -> void:
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
	var saver := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()
	var slopes := preload("res://scripts/tools/art/forest_village_interiors/entrance_slopes.gd").new()
	for asset: Dictionary in manifest.assets:
		var model: Node3D = load(HOUSES+asset.name+".tscn").instantiate()
		var count := slopes.apply(model,asset)
		var packed := PackedScene.new()
		assert(packed.pack(model) == OK)
		saver.save_resource(packed,HOUSES+asset.name+".tscn")
		model.free()
		print("ENTRANCE_RAMP_REPAIRED ",asset.name," ",count)
	var path := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
	var village: Node3D = load(path).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var home: Node3D = village.get_node("Households/Gatherer")
	var building: Node3D = home.get_node("Building")
	# The older scene marker landed halfway up the new raised stair; use dry ground.
	home.get_node("DoorApproach").position = building.transform*building.get_node("ApproachAnchor").position+building.basis*Vector3(0,0,.30)
	preload("res://scripts/tools/art/forest_village_interiors/entrance_library.gd").new().own_children(village,village)
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	saver.save_resource(packed,path)
	village.free()
	quit()
