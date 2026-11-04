extends SceneTree
## Update only the village paving; all authored placements stay in their scene.

const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const PAVING = preload("res://scripts/tools/art/forest_village_whitebox/flagstone_paving.gd")

func _initialize() -> void:
	run.call_deferred()

func own_children(parent: Node, owner_: Node) -> void:
	for child in parent.get_children():
		child.owner = owner_
		if child.scene_file_path.is_empty(): own_children(child,owner_)

func run() -> void:
	var village: Node3D = load(ENV).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	var source: ShaderMaterial = load("res://assets/materials/environment/forest_town/ground.tres")
	var paving := PAVING.new()
	paving.build(village.get_node("Paths"),source)
	preload("res://scripts/tools/art/forest_village_whitebox/ground_contact.gd").new().build(village,paving,source)
	preload("res://scripts/tools/art/forest_village_whitebox/ground_flora.gd").new().build(village)
	own_children(village,village)
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	assert(ResourceSaver.save(packed,ENV) == OK)
	village.free()
	print("FOREST_PAVING_SAVED: ",ENV)
	quit()
