extends SceneTree
## Import author meshes with the same painted PBR/weather protocol as the houses.

const LIB := preload("res://scripts/tools/art/forest_village_structures/structure_library.gd")
const MANIFEST := "res://assets/resources/art/forest_village/structures_models.json"
const MAT_ROOT := "res://assets/materials/environment/forest_village/structures/"
var saver := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()
var library := LIB.new()
var materials: Dictionary = {}

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	for path: String in [LIB.SCENES,MAT_ROOT]:
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path))
	for kind: String in ["Wood","WoodLight","Iron","Stone"]:
		var mat: ShaderMaterial = load("res://assets/materials/environment/forest_village/houses/"+kind.to_snake_case()+".tres").duplicate()
		mat.resource_name = "Forest village structures · "+kind
		mat.set_shader_parameter("normal_scale",0.55 if kind != "Iron" else 0.28)
		mat.set_shader_parameter("weather_puddles_enabled",false)
		assert(mat.get_shader_parameter("use_normal_texture") == true)
		assert(mat.get_shader_parameter("use_roughness_texture") == true)
		saver.save_resource(mat,MAT_ROOT+kind.to_snake_case()+".tres")
		materials[kind] = mat
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
	for asset: Dictionary in manifest.assets:
		var doc := GLTFDocument.new()
		var state := GLTFState.new()
		assert(doc.append_from_file(LIB.MODELS+asset.file,state) == OK)
		var source := doc.generate_scene(state)
		var meshes := source.find_children("*","MeshInstance3D",true,false)
		if source is MeshInstance3D: meshes.push_front(source)
		assert(meshes.size() == 1)
		var mesh: ArrayMesh = meshes[0].mesh
		var triangles := 0
		for surface: int in mesh.get_surface_count():
			var data := mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array = data[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = data[Mesh.ARRAY_INDEX]
			assert(data[Mesh.ARRAY_NORMAL].size() == vertices.size())
			assert(data[Mesh.ARRAY_TEX_UV].size() == vertices.size())
			assert(data[Mesh.ARRAY_COLOR].size() == vertices.size())
			assert(data[Mesh.ARRAY_TANGENT].size() == vertices.size()*4)
			var kind := mesh.surface_get_material(surface).resource_name.trim_prefix("FVS_")
			assert(materials.has(kind))
			mesh.surface_set_material(surface,materials[kind])
			triangles += indices.size()/3 if indices.size() else vertices.size()/3
		assert(triangles == int(asset.triangles))
		var importer := ImporterMesh.from_mesh(mesh)
		importer.generate_lods(60.0,0.0,[])
		mesh = importer.get_mesh()
		saver.save_resource(mesh,LIB.MODELS+asset.name+".mesh.res")
		var prefab := Node3D.new()
		prefab.name = String(asset.name).to_pascal_case()
		prefab.set_meta("asset_category","finished_timber_structure")
		prefab.set_meta("source_model",LIB.MODELS+asset.file)
		prefab.set_meta("triangles",triangles)
		prefab.set_meta("features",asset.features)
		library.visual(prefab,"Model",asset.name,Vector3.ZERO)
		if asset.name == "creek_bridge":
			var body := StaticBody3D.new()
			body.name = "Collision"
			body.collision_mask = 0
			prefab.add_child(body)
			library.collider(body,"Deck",Vector3(0,-0.11,0),Vector3(5.85,0.25,3.276))
			for side: int in [-1,1]:
				for i: int in 2:
					library.collider(body,"Rail%d_%d" % [side,i],Vector3(0,0.38+i*0.35,side*1.716),Vector3(5.772,0.12,0.09))
		library.own_children(prefab,prefab)
		var packed := PackedScene.new()
		assert(packed.pack(prefab) == OK)
		saver.save_resource(packed,LIB.SCENES+asset.name+".tscn")
		print("STRUCTURE_IMPORTED ",asset.name," ",triangles," tris; tangent normals; native LODs")
		prefab.free()
		source.free()
	quit()
