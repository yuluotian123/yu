extends SceneTree
## Repaint surfaces and refresh baked terrain contacts without moving authored objects.

const PALETTE = preload("res://scripts/tools/art/forest_town/material_palette.gd")
const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"

func _initialize() -> void:
	run.call_deferred()

func own_children(parent: Node, owner_: Node) -> void:
	for child in parent.get_children():
		child.owner = owner_
		if child.scene_file_path.is_empty(): own_children(child,owner_)

func run() -> void:
	var palette := PALETTE.new()
	palette.prepare_textures()
	palette.apply()
	var village: Node3D = load(ENV).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	palette.apply_terrain(village)
	var source: ShaderMaterial = load("res://assets/materials/environment/forest_town/ground.tres")
	var paving := preload("res://scripts/tools/art/forest_village_whitebox/flagstone_paving.gd").new()
	paving.build(village.get_node("Paths"),source)
	preload("res://scripts/tools/art/forest_village_whitebox/ground_contact.gd").new().build(village,paving,source)
	preload("res://scripts/tools/art/forest_village_whitebox/ground_flora.gd").new().build(village)
	own_children(village,village)
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	palette.save_resource(packed,ENV)
	village.free()
	print("FOREST_MATERIALS_SAVED: atlas v4; grass/timber/plaster/roof/bark/stone; terrain contact rebaked; placements unchanged")
	quit()
