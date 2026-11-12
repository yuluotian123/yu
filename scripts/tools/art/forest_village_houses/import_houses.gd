extends SceneTree
## Import Blender-authored exteriors with the native forest lighting/weather protocol.

const MODEL_ROOT := "res://assets/models/environment/forest_village/houses/"
const SCENE_ROOT := "res://assets/scenes/environment/forest_village/houses/"
const MATERIAL_ROOT := "res://assets/materials/environment/forest_village/houses/"
const MANIFEST := "res://assets/resources/art/forest_village/houses_models_v2.json"
const PALETTE := preload("res://scripts/tools/art/forest_town/material_palette.gd")
var materials: Dictionary = {}
var checks: Array = []

func _initialize() -> void:
	build.call_deferred()

func vec(values: Array) -> Vector3:
	return Vector3(float(values[0]),float(values[1]),float(values[2]))

func attach(parent: Node, child: Node, owner_root: Node) -> void:
	parent.add_child(child)
	child.owner = owner_root

func save_scene(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	PALETTE.new().save_resource(packed,path)

func prepare_materials() -> void:
	var source_root := "res://assets/materials/environment/forest_town/"
	# Load source pixels directly so the art pipeline also works before editor reimport.
	var pixels := Image.load_from_file("res://assets/textures/environment/forest_village/houses/painterly_house_atlas_v3.png")
	pixels.generate_mipmaps()
	var texture := ImageTexture.create_from_image(pixels)
	PALETTE.new().save_resource(texture,"res://assets/textures/environment/forest_village/houses/painterly_house_atlas_v3.res")
	var data_maps: Dictionary = {}
	for suffix: String in ["normal","roughness"]:
		var path := "res://assets/textures/environment/forest_village/houses/painterly_house_atlas_v3_"+suffix
		var data := Image.load_from_file(path+".png")
		assert(data.get_width() == 2048 and data.get_height() == 2048)
		data.generate_mipmaps()
		var map := ImageTexture.create_from_image(data)
		PALETTE.new().save_resource(map,path+".res")
		data_maps[suffix] = map
	var mapping := {"Plaster":"architecture","Wood":"timber","WoodLight":"timber","Roof":"roof","Stone":"masonry","Glass":"glass","Plant":"architecture","Thatch":"roof","Turf":"roof","Iron":"masonry"}
	for kind: String in mapping:
		var mat: ShaderMaterial = (load(source_root+mapping[kind]+".tres") as ShaderMaterial).duplicate()
		mat.resource_name = "Forest village houses · "+kind
		mat.set_shader_parameter("albedo_texture",texture)
		mat.set_shader_parameter("painterly_atlas",texture)
		mat.set_shader_parameter("base_color",Color(1.0,1.0,1.0))
		mat.set_shader_parameter("color_saturation",1.02)
		mat.set_shader_parameter("texture_detail",0.18 if kind == "Glass" else (0.80 if kind in ["Wood","WoodLight","Thatch","Turf"] else 0.70))
		mat.set_shader_parameter("painterly_light_wrap",0.55)
		mat.set_shader_parameter("painterly_shadow_lift",0.40)
		mat.set_shader_parameter("use_vertex_color",true)
		mat.set_shader_parameter("use_texture",true)
		mat.set_shader_parameter("use_normal_texture",false)
		mat.set_shader_parameter("use_ground_contact",false)
		mat.set_shader_parameter("use_vertex_coverage",false)
		mat.set_shader_parameter("weather_puddles_enabled",false)
		mat.set_shader_parameter("uv_scale",Vector2.ONE)
		mat.set_shader_parameter("uv_offset",Vector2.ZERO)
		PALETTE.new().apply_surface_response(mat,"stone" if kind == "Stone" else ("plant" if kind in ["Plant","Turf"] else "bark"))
		mat.set_shader_parameter("use_normal_texture",true)
		mat.set_shader_parameter("normal_texture",data_maps.normal)
		mat.set_shader_parameter("normal_scale",{"Plaster":0.20,"Stone":0.65,"Glass":0.10,"Iron":0.28}.get(kind,0.48))
		mat.set_shader_parameter("use_roughness_texture",true)
		mat.set_shader_parameter("roughness_texture",data_maps.roughness)
		mat.set_shader_parameter("use_orm_texture",false)
		mat.set_shader_parameter("surface_roughness",1.0)
		mat.set_shader_parameter("surface_metallic",0.55 if kind == "Iron" else 0.0)
		mat.set_shader_parameter("specular_strength",0.35 if kind in ["Iron","Glass"] else 0.08)
		mat.set_shader_parameter("wet_darkening_strength",0.75 if kind in ["Plaster","Stone"] else 0.9)
		mat.set_shader_parameter("wet_film_strength",0.35 if kind == "Stone" else 0.13)
		PALETTE.new().save_resource(mat,MATERIAL_ROOT+kind.to_snake_case()+".tres")
		materials[kind] = mat

func build() -> void:
	for path: String in [SCENE_ROOT,MATERIAL_ROOT]:
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path))
	prepare_materials()
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
	assert(manifest.assets.size() == 10)
	for asset: Dictionary in manifest.assets:
		var name_: String = asset.name
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		assert(doc.append_from_file(MODEL_ROOT+asset.file,state) == OK,"GLB import: "+name_)
		var imported := doc.generate_scene(state)
		var meshes := imported.find_children("*","MeshInstance3D",true,false)
		if imported is MeshInstance3D: meshes.push_front(imported)
		assert(meshes.size() == 1,"One mesh per independent building")
		var source: MeshInstance3D = meshes[0]
		var mesh: ArrayMesh = source.mesh
		var triangles := 0
		for surface: int in mesh.get_surface_count():
			var arrays := mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
			var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
			assert(colors.size() == vertices.size() and uv.size() == vertices.size(),"Palette and UVs: "+name_)
			assert(not colors[0].is_equal_approx(Color.WHITE),"Exported authored vertex colors: "+name_)
			triangles += indices.size()/3 if not indices.is_empty() else vertices.size()/3
			var material: Material = mesh.surface_get_material(surface)
			var kind := material.resource_name.trim_prefix("FV2_")
			assert(materials.has(kind),"Known material: "+material.resource_name)
			mesh.surface_set_material(surface,materials[kind])
		assert(triangles == int(asset.triangles) and triangles <= int(manifest.budget_triangles),"Triangle budget: "+name_)
		var importer := ImporterMesh.from_mesh(mesh)
		importer.generate_lods(60.0,0.0,[])
		mesh = importer.get_mesh()
		PALETTE.new().save_resource(mesh,MODEL_ROOT+name_+".mesh.res")
		var prefab := Node3D.new()
		prefab.name = name_.substr(3).to_pascal_case()
		prefab.set_meta("asset_category","building")
		prefab.set_meta("source_model",MODEL_ROOT+asset.file)
		prefab.set_meta("concept", "res://"+asset.concept)
		prefab.set_meta("triangles",triangles)
		prefab.set_meta("complete_exterior",true)
		prefab.set_meta("detail_revision",asset.detail_revision)
		prefab.set_meta("detail_features",asset.detail_features)
		prefab.set_meta("exterior_only",asset.exterior_only)
		prefab.set_meta("entrance_audit",asset.entrance_audit)
		prefab.set_meta("elevation_features",asset.elevation_features)
		var model := MeshInstance3D.new()
		model.name = "Model"
		model.mesh = mesh
		model.gi_mode = GeometryInstance3D.GI_MODE_STATIC
		attach(prefab,model,prefab)
		var body := StaticBody3D.new()
		body.name = "Collision1"
		body.collision_mask = 0
		attach(prefab,body,prefab)
		for index: int in asset.collision.size():
			var data: Dictionary = asset.collision[index]
			var shape := CollisionShape3D.new()
			shape.name = "Shape%02d" % index
			if data.get("shape","") == "convex":
				var convex := ConvexPolygonShape3D.new()
				var points := PackedVector3Array()
				for point: Array in data.points: points.append(vec(point))
				convex.points = points
				shape.shape = convex
			else:
				var box := BoxShape3D.new()
				box.size = vec(data.size)
				shape.position = vec(data.position)
				shape.shape = box
			attach(body,shape,prefab)
		preload("res://scripts/tools/art/forest_village_interiors/entrance_slopes.gd").new().apply(prefab,asset)
		for anchor: String in ["DoorAnchor","ApproachAnchor"]:
			var marker := Marker3D.new()
			marker.name = anchor
			marker.position = vec(asset.door if anchor == "DoorAnchor" else asset.approach)
			attach(prefab,marker,prefab)
		save_scene(prefab,SCENE_ROOT+name_+".tscn")
		var lods: Array = []
		for surface: int in importer.get_surface_count(): lods.append(importer.get_surface_lod_count(surface))
		checks.append({"asset":name_,"triangles":triangles,"surfaces":mesh.get_surface_count(),"lods":lods,"collision_volumes":asset.collision.size(),"four_elevations":asset.elevation_features})
		prefab.free()
		imported.free()
	build_gallery(manifest)
	var report := FileAccess.open("res://assets/resources/art/forest_village/houses_import_validation.json",FileAccess.WRITE)
	report.store_string(JSON.stringify({"assets":checks,"all_passed":true},"\t"))
	print("FOREST_HOUSES_IMPORTED: 10 complete exteriors, shared weather materials, native LODs and simple collisions")
	quit()

func build_gallery(manifest: Dictionary) -> void:
	var gallery := Node3D.new()
	gallery.name = "ForestVillageHousesV2"
	gallery.editor_description = "第二版十户完整外观模型。可在编辑器中绕行查看正面、背面与两侧；所有模型具有简化碰撞和原生 LOD。"
	for index: int in manifest.assets.size():
		var asset: Dictionary = manifest.assets[index]
		var prefab: Node3D = load(SCENE_ROOT+asset.name+".tscn").instantiate()
		prefab.name = asset.household
		prefab.position = Vector3((index%5-2)*9,0,(index/5)*10)
		attach(gallery,prefab,gallery)
		var label := Label3D.new()
		label.name = "Label%02d" % index
		label.text = asset.name+"\n"+str(asset.triangles)+" tris"
		label.font_size = 40
		label.pixel_size = 0.01
		label.position = prefab.position+Vector3(0,0.05,4.3)
		label.rotation_degrees.x = -90
		attach(gallery,label,gallery)
	var environment := WorldEnvironment.new()
	environment.name = "WorldEnvironment"
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color(0.23,0.28,0.27)
	environment.environment.background_intensity = 22000.0
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color(0.66,0.76,0.74)
	environment.environment.ambient_light_energy = 0.75
	environment.camera_attributes = CameraAttributesPhysical.new()
	environment.camera_attributes.exposure_multiplier = 1.2
	(environment.camera_attributes as CameraAttributesPhysical).exposure_shutter_speed = 81.5719
	attach(gallery,environment,gallery)
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-48,-32,0)
	sun.light_color = Color(1.0,0.91,0.76)
	sun.light_energy = 1.6
	sun.light_intensity_lux = 100000.0
	sun.shadow_enabled = true
	attach(gallery,sun,gallery)
	var camera := Camera3D.new()
	camera.name = "Camera3D"
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 33
	camera.position = Vector3(14,25,34)
	camera.basis = Basis.looking_at(Vector3(0,1,5)-camera.position)
	attach(gallery,camera,gallery)
	save_scene(gallery,"res://assets/scenes/environment/forest_village/houses_gallery.tscn")
	gallery.free()
