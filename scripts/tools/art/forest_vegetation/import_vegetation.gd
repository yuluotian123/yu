extends SceneTree
## Import the Blender kit with baked volume normals, native LOD and existing weather shaders.
const MODELS := "res://assets/models/environment/forest_vegetation/"
const SCENES := "res://assets/scenes/environment/forest_vegetation/"
const MATS := "res://assets/materials/environment/forest_vegetation/"
const META := "res://assets/resources/art/forest_vegetation/"

func _initialize() -> void:
	run.call_deferred()

func save_scene(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	assert(ResourceSaver.save(packed,path) == OK)

func run() -> void:
	for path in [SCENES,MATS]: DirAccess.make_dir_recursive_absolute(path)
	var texture := load("res://assets/textures/environment/forest_village/character_style/sprays.res") as Texture2D
	assert(texture != null)
	var foliage := load("res://assets/materials/environment/forest_town/foliage.tres").duplicate(true) as ShaderMaterial
	foliage.resource_name = "Forest vegetation - branch volume normals"
	foliage.set_shader_parameter("leaf_texture",texture)
	foliage.set_shader_parameter("lower_color",Vector3(.76,.92,.91))
	foliage.set_shader_parameter("upper_color",Vector3(.85,.98,.86))
	foliage.set_shader_parameter("gradient_strength",.78)
	foliage.set_shader_parameter("use_painted_sprays",true)
	foliage.set_shader_parameter("directional_shadow_lift",.32)
	foliage.set_shader_parameter("canopy_ambient_up",.90)
	foliage.set_shader_parameter("normal_roundness",0.0)
	foliage.set_shader_parameter("vertex_color_strength",1.0)
	foliage.set_shader_parameter("texture_detail",0.0)
	foliage.set_shader_parameter("painterly_strength",0.90)
	foliage.set_shader_parameter("wind_strength",0.022)
	foliage.set_shader_parameter("wind_speed",0.75)
	foliage.set_shader_parameter("alpha_cutoff",0.4)
	assert(ResourceSaver.save(foliage,MATS+"foliage.tres") == OK)
	foliage.take_over_path(MATS+"foliage.tres")
	var white := Image.create(2,2,false,Image.FORMAT_RGBA8)
	white.fill(Color.WHITE)
	var fern_material := foliage.duplicate(true) as ShaderMaterial
	fern_material.set_shader_parameter("use_painted_sprays",false)
	fern_material.set_shader_parameter("canopy_ambient_up",0.0)
	fern_material.set_shader_parameter("leaf_texture",ImageTexture.create_from_image(white))
	assert(ResourceSaver.save(fern_material,MATS+"fern.tres") == OK)
	fern_material.take_over_path(MATS+"fern.tres")
	var bark := load("res://assets/materials/environment/forest_town/bark.tres").duplicate(true) as ShaderMaterial
	bark.resource_name = "Forest vegetation - warm broad bark"
	bark.set_shader_parameter("albedo_texture",load("res://assets/textures/environment/forest_village/character_style/bark.res"))
	bark.set_shader_parameter("normal_texture",load("res://assets/textures/environment/forest_village/character_style/bark_normal.res"))
	bark.set_shader_parameter("texture_detail",0.78)
	bark.set_shader_parameter("normal_scale",0.65)
	bark.set_shader_parameter("pixel_brush",true)
	assert(ResourceSaver.save(bark,MATS+"bark.tres") == OK)
	bark.take_over_path(MATS+"bark.tres")
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(META+"manifest.json"))
	var checks := []
	for entry: Dictionary in manifest.assets:
		var asset: String = entry.name
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		assert(doc.append_from_file(MODELS+asset+".glb",state) == OK)
		var source := doc.generate_scene(state)
		var meshes := source.find_children("*","MeshInstance3D",true,false)
		if source is MeshInstance3D: meshes.push_front(source)
		assert(meshes.size() == 1)
		var mesh := (meshes[0] as MeshInstance3D).mesh
		var triangles := 0
		var has_normals := true
		for surface in mesh.get_surface_count():
			var arrays := mesh.surface_get_arrays(surface)
			var count: int = arrays[Mesh.ARRAY_VERTEX].size()
			assert(arrays[Mesh.ARRAY_COLOR].size() == count)
			assert(arrays[Mesh.ARRAY_NORMAL].size() == count)
			assert(arrays[Mesh.ARRAY_TEX_UV].size() == count)
			for n: Vector3 in arrays[Mesh.ARRAY_NORMAL]:
				assert(n.is_finite() and n.length() > 0.98 and n.length() < 1.02)
			triangles += (arrays[Mesh.ARRAY_INDEX].size() if arrays[Mesh.ARRAY_INDEX].size() else count)/3
			var is_leaf := mesh.surface_get_material(surface).resource_name.begins_with("FV_Foliage")
			var leaf_material := fern_material if asset == "fern_fan" else foliage
			mesh.surface_set_material(surface,leaf_material if is_leaf else bark)
		assert(triangles == int(entry.triangles) and triangles <= 8000)
		var importer := ImporterMesh.from_mesh(mesh)
		importer.generate_lods(60.0,0.0,[])
		var lods := []
		for surface in importer.get_surface_count(): lods.append(importer.get_surface_lod_count(surface))
		var native := importer.get_mesh()
		assert(ResourceSaver.save(native,MODELS+asset+".mesh.res") == OK)
		native.take_over_path(MODELS+asset+".mesh.res")
		var prefab: Node3D
		# Preserve the current prefab's authored collision and placement metadata.
		if ResourceLoader.exists(SCENES+asset+".tscn"):
			prefab = load(SCENES+asset+".tscn").instantiate()
			prefab.get_node("Model").mesh = native
		else:
			prefab = Node3D.new()
			prefab.name = asset.to_pascal_case()
			var model := MeshInstance3D.new()
			model.name = "Model"
			model.mesh = native
			prefab.add_child(model)
			model.owner = prefab
		prefab.set_meta("source_model",MODELS+asset+".glb")
		prefab.set_meta("triangles",triangles)
		prefab.set_meta("source_polygons",triangles)
		prefab.set_meta("vegetation_revision",5)
		prefab.set_meta("asset_category",entry.category)
		save_scene(prefab,SCENES+asset+".tscn")
		prefab.free(); source.free()
		checks.append({"asset":asset,"triangles":triangles,"lod_counts":lods,"unit_normals":has_normals})
	var output := FileAccess.open(META+"godot_validation.json",FileAccess.WRITE)
	output.store_string(JSON.stringify({"assets":checks,"placements_changed":false},"\t"))
	print("FOREST_VEGETATION_IMPORTED ",JSON.stringify(checks))
	quit()
