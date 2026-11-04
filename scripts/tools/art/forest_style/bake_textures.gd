extends SceneTree
## Pack the paint swatches with mipmaps and match the grass roots to the new ground.
const STYLE := "res://assets/textures/environment/forest_village/character_style/"
const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const PALETTE := preload("res://scripts/tools/art/forest_town/material_palette.gd")
const CONTACT := preload("res://scripts/tools/art/forest_village_whitebox/ground_contact.gd")
const TEXTURES := ["house", "house_normal", "house_roughness", "terrain", "bark", "bark_normal", "floor", "sprays", "grass"]

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	var writer := PALETTE.new()
	for kind: String in TEXTURES:
		var image := Image.load_from_file(STYLE+kind+".png")
		assert(image != null and not image.is_empty(), kind)
		if kind in ["sprays","grass"]:
			image.fix_alpha_edges()
		image.generate_mipmaps(kind.ends_with("_normal"))
		var texture := ImageTexture.create_from_image(image)
		texture.resource_name = "Forest paint · "+kind
		writer.save_resource(texture, STYLE+kind+".res")
	var village: Node3D = load(ENV).instantiate()
	var contact := CONTACT.new()
	contact.bake_ground(village, null)
	var count := 0
	for patch in village.get_node("GroundFlora").get_children():
		if not patch is MultiMeshInstance3D:
			continue
		var mesh: MultiMesh = patch.multimesh
		var buffer := mesh.buffer
		assert(buffer.size() == mesh.instance_count*16)
		for i in mesh.instance_count:
			var offset := i*16
			var at := Vector2(buffer[offset+3]+patch.position.x, buffer[offset+11]+patch.position.z)
			var color := contact.ground_color(at)
			buffer[offset+12] = color.r
			buffer[offset+13] = color.g
			buffer[offset+14] = color.b
		mesh.buffer = buffer
		writer.save_resource(mesh,mesh.resource_path)
		count += mesh.instance_count
	assert(count == int(village.get_node("GroundFlora").get_meta("plant_counts").grass))
	print("FOREST_PAINT_BAKED: ",TEXTURES.size()," textures, ground contact, ",count," grass root colours")
	village.free()
	quit()
