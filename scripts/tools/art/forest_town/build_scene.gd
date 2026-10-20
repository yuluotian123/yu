extends SceneTree
## Build editor-owned scenes from the independently authored Blender models.

const MODEL_ROOT := "res://assets/models/environment/forest_town/"
const SCENE_ROOT := "res://assets/scenes/environment/forest_town/"
const MATERIAL_ROOT := "res://assets/materials/environment/forest_town/"
const META_ROOT := "res://assets/resources/art/forest_town_v1/"
var checks: Array = []

func _initialize() -> void:
	call_deferred("build")

func save_resource(resource: Resource, path: String) -> void:
	var error := ResourceSaver.save(resource, path)
	assert(error == OK, "Could not save " + path)
	resource.take_over_path(path)

func save_scene(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	save_resource(packed, path)

func attach(parent: Node, child: Node, owner_root: Node) -> void:
	parent.add_child(child)
	child.owner = owner_root

func make_material(label: String, softness: float, specular: float) -> ShaderMaterial:
	var material := ShaderMaterial.new()
	material.resource_name = "Forest Town · " + label
	material.shader = load("res://assets/shaders/town/town_toon.gdshader")
	material.set_shader_parameter("ggx_energy_lut", load("res://assets/textures/rendering/ggx_energy.res"))
	material.set_shader_parameter("use_vertex_color", true)
	material.set_shader_parameter("base_color", Color.WHITE)
	material.set_shader_parameter("use_texture", true)
	material.set_shader_parameter("albedo_texture", load("res://assets/textures/environment/forest_town/painterly_atlas_v3.res"))
	material.set_shader_parameter("texture_average", Vector3(0.93, 0.93, 0.93))
	material.set_shader_parameter("texture_detail", 0.58)
	material.set_shader_parameter("painterly_strength", 0.92)
	material.set_shader_parameter("painterly_light_wrap", 0.90)
	material.set_shader_parameter("painterly_atlas", load("res://assets/textures/environment/forest_town/painterly_atlas_v3.res"))
	material.set_shader_parameter("painterly_dapple_strength", 0.32)
	material.set_shader_parameter("painterly_dapple_scale", 0.18)
	material.set_shader_parameter("painterly_shadow_lift", 0.78)
	material.set_shader_parameter("uv_scale", Vector2.ONE)
	material.set_shader_parameter("uv_offset", Vector2.ZERO)
	material.set_shader_parameter("surface_metallic", 0.0)
	material.set_shader_parameter("emission_color", Vector3.ZERO)
	material.set_shader_parameter("emission_intensity", 1.0)
	material.set_shader_parameter("use_normal_texture", false)
	material.set_shader_parameter("use_orm_texture", false)
	material.set_shader_parameter("color_saturation", 1.0)
	material.set_shader_parameter("toon_strength", 1.0)
	material.set_shader_parameter("toon_softness", softness)
	material.set_shader_parameter("surface_roughness", 1.0)
	material.set_shader_parameter("specular_strength", specular)
	return material

func make_leaf_material() -> ShaderMaterial:
	# Reuse the existing alpha-tested leaf/DDGI/weather protocol. Standard vertex
	# colors are not supported by this project's DDGI StandardMaterial extractor.
	var material := ShaderMaterial.new()
	material.resource_name = "Forest Town · painted oak sprays"
	material.shader = load("res://assets/shaders/town/leaf_article.gdshader")
	material.set_shader_parameter("leaf_texture", load("res://assets/textures/environment/forest_town/oak_sprigs_v2.res"))
	material.set_shader_parameter("alpha_cutoff", 0.38)
	material.set_shader_parameter("normal_roundness", 0.0)
	material.set_shader_parameter("gradient_strength", 0.0)
	material.set_shader_parameter("vertex_color_strength", 1.0)
	material.set_shader_parameter("texture_detail", 0.30)
	material.set_shader_parameter("lower_color", Vector3.ONE)
	material.set_shader_parameter("upper_color", Vector3.ONE)
	material.set_shader_parameter("painterly_strength", 0.90)
	material.set_shader_parameter("directional_shadow_lift", 0.04)
	material.set_shader_parameter("transmission_strength", 0.03)
	# The old combined-mesh crown centers must not override the new authored normals.
	material.set_shader_parameter("wind_strength", 0.0)
	return material

func add_box_collision(parent: Node3D, owner_root: Node, position: Vector3, size: Vector3) -> void:
	var body := StaticBody3D.new()
	body.name = "Collision" + str(parent.get_child_count())
	body.collision_mask = 0
	attach(parent, body, owner_root)
	var shape := CollisionShape3D.new()
	shape.position = position
	var box := BoxShape3D.new()
	box.size = size
	shape.shape = box
	attach(body, shape, owner_root)

func vec(data: Array) -> Vector3:
	return Vector3(float(data[0]), float(data[1]), float(data[2]))

func add_stair_ramp(parent: Node3D, owner_root: Node, width: float, run: float, height: float) -> void:
	var body := StaticBody3D.new()
	body.name = "WalkableStairRamp"
	body.collision_mask = 0
	attach(parent, body, owner_root)
	var shape := CollisionShape3D.new()
	shape.name = "RampShape"
	var ramp := ConvexPolygonShape3D.new()
	ramp.points = PackedVector3Array([
		Vector3(-width * 0.5, 0, 0.15), Vector3(width * 0.5, 0, 0.15),
		Vector3(-width * 0.5, 0, 0.15 - run), Vector3(width * 0.5, 0, 0.15 - run),
		Vector3(-width * 0.5, height, 0.15 - run), Vector3(width * 0.5, height, 0.15 - run)])
	shape.shape = ramp
	attach(body, shape, owner_root)

func build() -> void:
	for path in [SCENE_ROOT, SCENE_ROOT + "models/", MATERIAL_ROOT]:
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path))
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(META_ROOT + "models.json"))
	var atlas_image := Image.load_from_file("res://assets/textures/environment/forest_town/painterly_atlas_v3.png")
	assert(atlas_image != null)
	atlas_image.generate_mipmaps()
	save_resource(ImageTexture.create_from_image(atlas_image), "res://assets/textures/environment/forest_town/painterly_atlas_v3.res")
	var leaf_image := Image.load_from_file("res://assets/textures/environment/forest_town/oak_sprigs_v2.png")
	assert(leaf_image != null and leaf_image.detect_alpha() != Image.ALPHA_NONE, "Oak sprays require real alpha")
	leaf_image.fix_alpha_edges()
	leaf_image.generate_mipmaps()
	save_resource(ImageTexture.create_from_image(leaf_image), "res://assets/textures/environment/forest_town/oak_sprigs_v2.res")
	var architecture := make_material("warm plaster and terracotta", 0.22, 0.02)
	var timber: ShaderMaterial = architecture.duplicate()
	timber.resource_name = "Forest Town · dark painted timber"
	timber.set_shader_parameter("painterly_shadow_lift", 0.18)
	timber.set_shader_parameter("texture_detail", 0.68)
	var roof_material: ShaderMaterial = architecture.duplicate()
	roof_material.resource_name = "Forest Town · warm clay shingles"
	roof_material.set_shader_parameter("painterly_shadow_lift", 0.30)
	roof_material.set_shader_parameter("painterly_dapple_strength", 0.40)
	roof_material.set_shader_parameter("texture_detail", 0.48)
	var masonry: ShaderMaterial = architecture.duplicate()
	masonry.resource_name = "Forest Town · painted limestone"
	masonry.set_shader_parameter("painterly_shadow_lift", 0.24)
	masonry.set_shader_parameter("texture_detail", 0.72)
	var glass: ShaderMaterial = timber.duplicate()
	glass.resource_name = "Forest Town · dark window panes"
	glass.set_shader_parameter("painterly_shadow_lift", 0.04)
	glass.set_shader_parameter("texture_detail", 0.18)
	var shaded_architecture: ShaderMaterial = architecture.duplicate()
	shaded_architecture.resource_name = "Forest Town · shaded foreground timber house"
	shaded_architecture.set_shader_parameter("painterly_shadow_lift", 0.04)
	shaded_architecture.set_shader_parameter("painterly_light_wrap", 0.40)
	var bark := make_material("curved woodland bark", 0.55, 0.02)
	bark.set_shader_parameter("toon_strength", 0.85)
	bark.set_shader_parameter("texture_detail", 0.68)
	bark.set_shader_parameter("painterly_light_wrap", 0.40)
	bark.set_shader_parameter("painterly_dapple_strength", 0.55)
	bark.set_shader_parameter("painterly_shadow_lift", 0.03)
	var hero_bark: ShaderMaterial = bark.duplicate()
	hero_bark.resource_name = "Forest Town · sun brushed ancient oak"
	hero_bark.set_shader_parameter("painterly_shadow_lift", 0.34)
	hero_bark.set_shader_parameter("painterly_light_wrap", 0.90)
	var foliage := make_leaf_material()
	var ground := make_material("earth and paving", 0.42, 0.08)
	ground.set_shader_parameter("texture_detail", 0.48)
	ground.set_shader_parameter("painterly_dapple_strength", 0.94)
	ground.set_shader_parameter("painterly_dapple_scale", 0.16)
	ground.set_shader_parameter("painterly_path_clearance", 1.0)
	ground.set_shader_parameter("painterly_shadow_lift", 0.0)
	ground.set_shader_parameter("weather_puddles_enabled", true)
	save_resource(architecture, MATERIAL_ROOT + "architecture.tres")
	save_resource(timber, MATERIAL_ROOT + "timber.tres")
	save_resource(roof_material, MATERIAL_ROOT + "roof.tres")
	save_resource(masonry, MATERIAL_ROOT + "masonry.tres")
	save_resource(glass, MATERIAL_ROOT + "glass.tres")
	save_resource(shaded_architecture, MATERIAL_ROOT + "architecture_shaded.tres")
	save_resource(foliage, MATERIAL_ROOT + "foliage.tres")
	save_resource(bark, MATERIAL_ROOT + "bark.tres")
	save_resource(hero_bark, MATERIAL_ROOT + "bark_hero.tres")
	save_resource(ground, MATERIAL_ROOT + "ground.tres")
	var river: ShaderMaterial = load(MATERIAL_ROOT + "river.tres")
	var categories: Dictionary = {}
	for asset: Dictionary in manifest.assets:
		var name: String = asset.name
		# Read the current GLB directly; another open editor may retain an old import cache.
		var document := GLTFDocument.new()
		var state := GLTFState.new()
		assert(document.append_from_file(MODEL_ROOT + asset.file, state) == OK, "GLB import missing: " + name)
		var source := document.generate_scene(state)
		var meshes := source.find_children("*", "MeshInstance3D", true, false)
		if source is MeshInstance3D:
			meshes.push_front(source)
		assert(meshes.size() == 1, "Each asset must remain one independently editable model: " + name)
		var source_mesh: MeshInstance3D = meshes[0]
		var triangles := 0
		var leaf_surfaces := 0
		for surface in source_mesh.mesh.get_surface_count():
			var arrays: Array = source_mesh.mesh.surface_get_arrays(surface)
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			triangles += indices.size() / 3 if not indices.is_empty() else arrays[Mesh.ARRAY_VERTEX].size() / 3
			assert(arrays[Mesh.ARRAY_COLOR] != null, "Missing authored palette colors: " + name)
			assert(not arrays[Mesh.ARRAY_COLOR][0].is_equal_approx(Color.WHITE), "Authored palette was replaced by a white export channel: " + name)
			assert(arrays[Mesh.ARRAY_TEX_UV] != null and arrays[Mesh.ARRAY_TEX_UV].size() == arrays[Mesh.ARRAY_VERTEX].size(), "Missing painted UVs: " + name)
			var imported_material := source_mesh.mesh.surface_get_material(surface)
			var is_sprig: bool = imported_material != null and imported_material.resource_name.begins_with("FT_OakSprigs")
			if is_sprig:
				leaf_surfaces += 1
			var structure_material: ShaderMaterial = shaded_architecture if name == "cottage_right" else architecture
			var material_name: String = imported_material.resource_name if imported_material != null else ""
			if material_name.begins_with("FT_Timber"): structure_material = timber
			elif material_name.begins_with("FT_Roof"): structure_material = roof_material
			elif material_name.begins_with("FT_Stone"): structure_material = masonry
			elif material_name.begins_with("FT_Glass"): structure_material = glass
			var tree_bark: ShaderMaterial = hero_bark if name == "ancient_oak" else bark
			var surface_material: ShaderMaterial = foliage if is_sprig else (tree_bark if asset.category in ["tree", "plant"] else (ground if asset.category == "ground" else structure_material))
			if asset.category == "water": surface_material = river
			source_mesh.mesh.surface_set_material(surface, surface_material)
		assert(triangles <= 8000 and int(asset.polygons) <= 8000, "Model over budget: " + name)
		assert(triangles == int(asset.triangles), "Imported triangle count differs from Blender: " + name)
		assert(leaf_surfaces == (1 if int(asset.get("cutout_sprigs", 0)) > 0 else 0), "Painted leaf surface not preserved: " + name)
		save_resource(source_mesh.mesh, MODEL_ROOT + name + ".mesh.res")
		checks.append({"asset": name, "polygons": int(asset.polygons), "godot_triangles": triangles, "within_budget": true, "painted_uvs": true, "authored_colors": true, "cutout_leaf_surfaces": leaf_surfaces})
		var prefab := Node3D.new()
		prefab.name = name.to_pascal_case()
		prefab.set_meta("asset_category", asset.category)
		prefab.set_meta("source_model", MODEL_ROOT + asset.file)
		prefab.set_meta("source_polygons", int(asset.polygons))
		prefab.set_meta("triangles", triangles)
		var visual := MeshInstance3D.new()
		visual.name = "Model"
		visual.mesh = source_mesh.mesh
		visual.gi_mode = GeometryInstance3D.GI_MODE_STATIC
		if asset.category == "water":
			visual.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
			visual.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		attach(prefab, visual, prefab)
		for collision: Dictionary in asset.collision:
			if collision.get("shape", "box") == "ramp":
				add_stair_ramp(prefab, prefab, float(collision.width), float(collision.run), float(collision.height))
			else:
				add_box_collision(prefab, prefab, vec(collision.position), vec(collision.size))
		if asset.category == "ground" and not asset.get("river_cut", false):
			add_box_collision(prefab, prefab, Vector3(1, -0.51, -17), Vector3(58, 1, 56))
		save_scene(prefab, SCENE_ROOT + "models/" + name + ".tscn")
		categories[name] = asset.category
		source.free()
		prefab.free()
	var town := Node3D.new()
	town.name = "RPGTown"
	town.editor_description = "森林小镇：八栋完整外观建筑，独立树木和植物；所有模型 <=8000 多边形及三角面。"
	var groups: Dictionary = {}
	for label in ["Buildings", "Trees", "Plants", "Props", "Terrain", "Water"]:
		var group := Node3D.new()
		group.name = label
		attach(town, group, town)
		groups[label] = group
	var category_groups := {"building": "Buildings", "tree": "Trees", "plant": "Plants", "prop": "Props", "ground": "Terrain", "water": "Water"}
	for placement: Dictionary in manifest.instances:
		var packed: PackedScene = load(SCENE_ROOT + "models/" + placement.asset + ".tscn")
		var instance := packed.instantiate()
		instance.name = placement.name
		instance.position = vec(placement.position)
		instance.rotation.y = deg_to_rad(float(placement.rotation_y))
		instance.scale = Vector3.ONE * float(placement.scale)
		attach(groups[category_groups[categories[placement.asset]]], instance, town)
	save_scene(town, SCENE_ROOT + "forest_town.tscn")
	town.free()
	write_level()
	var file := FileAccess.open(META_ROOT + "godot_validation.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"checks": checks, "models": checks.size(), "all_within_budget": true}, "\t"))
	print("FOREST_TOWN_BUILT: ", checks.size(), " independent models, ", manifest.instances.size(), " instances; imported budgets verified.")
	quit()

func write_level() -> void:
	var text := """[gd_scene format=3]

[ext_resource type="PackedScene" path="res://assets/scenes/levels/whitebox_test.tscn" id="base"]
[ext_resource type="PackedScene" path="res://assets/scenes/environment/forest_town/forest_town.tscn" id="town"]

[node name="ForestTownLevel" instance=ExtResource("base")]
editor_description = "森林小镇关卡：参考 scene-concept.png 的构图、暖色建筑与青绿森林。所有房屋为完整四面外观模型，分别维护；WASD / 跳跃及现有角色框架沿用白盒。"

[node name="Visual" parent="Ground" index="0"]
visible = false

[node name="GameplayBody" parent="Ground" index="1"]
collision_layer = 0

[node name="RPGTown" parent="." instance=ExtResource("town")]
"""
	var file := FileAccess.open("res://assets/scenes/levels/forest_town_level.tscn", FileAccess.WRITE)
	file.store_string(text)
