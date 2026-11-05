extends RefCounted
## Offline surface palette shared by the asset importer and village material pass.

const ROOT := "res://assets/materials/environment/forest_town/"
const ATLAS := "res://assets/textures/environment/forest_town/painterly_atlas_v4.res"
const GRASS_TEXTURE := "res://assets/textures/environment/forest_village/groundcover_v4.res"
const BARK_TEXTURE := "res://assets/textures/environment/forest_town/bark_painted_v4.res"
const BARK_NORMAL := "res://assets/textures/environment/forest_town/bark_painted_v4_normal.res"
const GRASS := "res://assets/materials/environment/forest_village/grass.tres"
const GRASS_SCALE := 0.85
const SURFACES := {
	"architecture": [Color(1.06,1.01,0.91),1.06,0.72],
	"architecture_shaded": [Color(1.06,1.01,0.91),1.06,0.72],
	"timber": [Color(1.10,0.98,0.83),1.08,0.64],
	"roof": [Color(1.12,1.02,0.86),1.12,0.64],
	"masonry": [Color(1.06,1.02,0.93),0.90,0.62],
	"glass": [Color(0.94,1.0,1.02),1.0,0.18],
	"bark": [Color(1.08,1.02,0.93),0.88,0.78],
	"bark_hero": [Color(1.08,1.02,0.93),0.88,0.78],
	"ground": [Color(1.04,1.0,0.92),1.08,0.64]
}

func save_resource(resource: Resource, path: String) -> void:
	var previous := FileAccess.get_file_as_string(path) if FileAccess.file_exists(path) and (path.ends_with(".tres") or path.ends_with(".tscn")) else ""
	var uid := ResourceLoader.get_resource_uid(path) if FileAccess.file_exists(path) else -1
	var header := RegEx.new()
	header.compile('uid="([^"]+)"')
	var identity := header.search(previous.get_slice("\n",0))
	if identity != null: uid = ResourceUID.text_to_id(identity.get_string(1))
	if uid < 0: uid = ResourceUID.create_id()
	var dependencies := {}
	var pattern := RegEx.new()
	pattern.compile('uid="([^"]+)" path="([^"]+)"')
	for found in pattern.search_all(previous): dependencies[found.get_string(2)] = found.get_string(1)
	assert(ResourceSaver.save(resource,path) == OK)
	assert(ResourceSaver.set_uid(path,uid) == OK)
	resource.take_over_path(path)
	if previous.is_empty(): return
	var lines := FileAccess.get_file_as_string(path).split("\n")
	var path_pattern := RegEx.new()
	path_pattern.compile('path="([^"]+)"')
	for i in range(lines.size()):
		if not lines[i].begins_with("[ext_resource ") or lines[i].contains(" uid="): continue
		var found := path_pattern.search(lines[i])
		if found != null and dependencies.has(found.get_string(1)):
			lines[i] = lines[i].replace(" path=",' uid="%s" path=' % dependencies[found.get_string(1)])
	var file := FileAccess.open(path,FileAccess.WRITE)
	file.store_string("\n".join(lines))

func prepare_textures() -> void:
	var image := Image.load_from_file("res://assets/textures/environment/forest_town/painterly_atlas_v4.png")
	assert(image != null and not image.is_empty())
	# Equal cells preserve all existing UVs. Native resources remain CPU-readable for GI.
	image.resize(1024,1024,Image.INTERPOLATE_LANCZOS)
	image.convert(Image.FORMAT_RGBA8)
	var grass_image := image.get_region(Rect2i(0,768,256,256))
	image.generate_mipmaps()
	grass_image.generate_mipmaps()
	var atlas := ImageTexture.create_from_image(image)
	atlas.resource_name = "Forest village · broad painted surfaces v4"
	save_resource(atlas,ATLAS)
	var grass := ImageTexture.create_from_image(grass_image)
	grass.resource_name = "Forest floor · groundcover brush clusters v4"
	save_resource(grass,GRASS_TEXTURE)
	var bark_image := Image.load_from_file("res://assets/textures/environment/forest_town/bark_painted_v4.png")
	assert(bark_image != null and not bark_image.is_empty())
	bark_image.resize(1024,1024,Image.INTERPOLATE_LANCZOS)
	bark_image.generate_mipmaps()
	var bark := ImageTexture.create_from_image(bark_image)
	bark.resource_name = "Ancient oak · open bark plates and lichen v4"
	save_resource(bark,BARK_TEXTURE)
	load("res://scripts/tools/art/forest_town/bark_relief.gd").new().bake()

func apply_surface_response(material: ShaderMaterial, kind: String) -> void:
	var response := Vector3(1.0,1.0,0.0)
	match kind:
		"bark":
			response = Vector3(0.8,0.08,0.82)
			material.set_shader_parameter("use_normal_texture",true)
			material.set_shader_parameter("normal_texture",load(BARK_NORMAL))
			material.set_shader_parameter("normal_scale",0.65)
		"stone": response = Vector3(1.0,0.48,0.35)
		"soil": response = Vector3(0.85,0.18,0.75)
		"grass": response = Vector3(0.85,0.38,0.65)
		"foliage": response = Vector3(1.0,0.35,0.60)
		"plant": response = Vector3(1.25,0.30,0.70)
	material.set_shader_parameter("wet_darkening_strength",response.x)
	material.set_shader_parameter("wet_film_strength",response.y)
	material.set_shader_parameter("wet_roughness_min",response.z)
	material.set_shader_parameter("wet_specular_scale",0.60 if kind == "stone" else 1.0)
	material.set_shader_parameter("wet_puddle_coverage_scale",0.60 if kind == "stone" else 1.0)
	if kind in ["bark","soil","grass","foliage","plant"]:
		material.set_shader_parameter("weather_puddles_enabled",false)

func apply() -> void:
	var texture: Texture2D = load(ATLAS)
	for key: String in SURFACES:
		var material: ShaderMaterial = load(ROOT+key+".tres")
		var settings: Array = SURFACES[key]
		material.set_shader_parameter("albedo_texture",texture)
		material.set_shader_parameter("painterly_atlas",texture)
		material.set_shader_parameter("base_color",settings[0])
		material.set_shader_parameter("color_saturation",settings[1])
		material.set_shader_parameter("texture_detail",settings[2])
		material.set_shader_parameter("use_vertex_coverage",false)
		material.set_shader_parameter("vertex_coverage_cutoff",0.32)
		material.set_shader_parameter("use_ground_contact",false)
		material.set_shader_parameter("ground_contact_rect",Vector4(0,0,1,1))
		material.set_shader_parameter("ground_contact_from_vertex",false)
		material.set_shader_parameter("ground_contact_height",0.38)
		material.set_shader_parameter("ground_contact_strength",1.0)
		if key.begins_with("bark"):
			material.set_shader_parameter("albedo_texture",load(BARK_TEXTURE))
			material.set_shader_parameter("uv_scale",Vector2.ONE/0.244)
			material.set_shader_parameter("uv_offset",-Vector2(0.253,0.253)/0.244)
			apply_surface_response(material,"bark")
		elif key == "ground": apply_surface_response(material,"stone")
		save_resource(material,ROOT+key+".tres")
	var foliage: ShaderMaterial = load(ROOT+"foliage.tres")
	foliage.set_shader_parameter("lower_color",Vector3(0.90,1.04,0.85))
	foliage.set_shader_parameter("upper_color",Vector3(1.14,1.17,0.67))
	apply_surface_response(foliage,"foliage")
	# Keep the individually authored crown normals, silhouettes and vertex color variation.
	save_resource(foliage,ROOT+"foliage.tres")
	var grass: ShaderMaterial = (load(ROOT+"ground.tres") as ShaderMaterial).duplicate()
	grass.resource_name = "Forest village · yellow green groundcover"
	grass.set_shader_parameter("albedo_texture",load(GRASS_TEXTURE))
	grass.set_shader_parameter("base_color",Color(1.03,1.08,0.90))
	grass.set_shader_parameter("color_saturation",1.08)
	grass.set_shader_parameter("texture_detail",0.52)
	apply_surface_response(grass,"soil")
	grass.set_shader_parameter("weather_puddles_enabled",true)
	grass.set_shader_parameter("wet_specular_scale",0.48)
	grass.set_shader_parameter("wet_puddle_coverage_scale",0.48)
	DirAccess.make_dir_recursive_absolute(GRASS.get_base_dir())
	save_resource(grass,GRASS)

func apply_terrain(village: Node3D) -> void:
	var material: ShaderMaterial = load(GRASS)
	for name_ in ["VillageGround","EastBank"]:
		var ground: MeshInstance3D = village.get_node("Terrain/"+name_)
		var arrays := ground.mesh.surface_get_arrays(0)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
		for i in range(vertices.size()):
			uv[i] = Vector2(vertices[i].x,vertices[i].z)*GRASS_SCALE
		arrays[Mesh.ARRAY_TEX_UV] = uv
		var mesh := ArrayMesh.new()
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
		mesh.surface_set_material(0,material)
		ground.mesh = mesh
		ground.material_override = material

