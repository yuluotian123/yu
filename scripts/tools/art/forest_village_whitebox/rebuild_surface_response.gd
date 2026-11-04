extends SceneTree
## Update existing materials in place; preserve vegetation, paths and navigation.
const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
var palette := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()
var configured := {}

func _initialize() -> void:
	run.call_deferred()

func configure(material: ShaderMaterial, kind: String) -> void:
	assert(material != null)
	if configured.has(material): return
	palette.apply_surface_response(material,kind)
	configured[material] = kind
	if not material.resource_path.contains("::") and material.resource_path.ends_with(".tres"):
		palette.save_resource(material,material.resource_path)

func own_children(parent: Node, scene: Node) -> void:
	for child in parent.get_children():
		child.owner = scene
		if child.scene_file_path.is_empty(): own_children(child,scene)

func run() -> void:
	load("res://scripts/tools/art/forest_town/bark_relief.gd").new().bake()
	for key in ["bark","bark_hero","ground","foliage"]:
		configure(load(palette.ROOT+key+".tres"),"bark" if key.begins_with("bark") else ("stone" if key == "ground" else "foliage"))
	configure(load(palette.GRASS),"soil")
	for kind in ["grass","fern","rosette","flowers"]:
		configure(load("res://assets/materials/environment/forest_village/ground_flora/"+kind+".tres"),"grass" if kind == "grass" else "plant")
	var village: Node3D = load(ENV).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	for tree: Node3D in village.get_node("Forest").find_children("Tree_*","Node3D",true,false):
		var model: MeshInstance3D = tree.get_node("Model")
		configure(model.get_active_material(0),"bark")
	for path in ["Terrain/VillageGround","Terrain/EastBank","GroundContact/RootLitter"]:
		configure(village.get_node(path).get_active_material(0),"soil")
	configure(village.get_node("Paths/Surface").get_active_material(0),"stone")
	for stone in village.get_node("Paths/Flagstones").get_children():
		configure(stone.get_active_material(0),"stone")
	for patch: MeshInstance3D in village.get_node("GroundContact").find_children("*","MeshInstance3D",true,false):
		if patch.name != "RootLitter": configure(patch.get_active_material(0),"grass")
	own_children(village,village)
	var packed := PackedScene.new()
	assert(packed.pack(village) == OK)
	palette.save_resource(packed,ENV)
	village.free()
	print("FOREST_SURFACE_RESPONSE_SAVED: ",configured.size()," materials; painted bark normals; porous wood/soil and soft leaf sheen; placements unchanged")
	quit()
