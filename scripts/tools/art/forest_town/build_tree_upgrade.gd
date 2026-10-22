extends SceneTree
## Import only the new trees and replace only woodland placements in the authored town.

const MODEL_ROOT := "res://assets/models/environment/forest_town/"
const SCENE_ROOT := "res://assets/scenes/environment/forest_town/"
const META_ROOT := "res://assets/resources/art/forest_town_v1/"

func _initialize() -> void:
	call_deferred("build")

func save_scene(node: Node, path: String) -> void:
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	assert(ResourceSaver.save(packed,path) == OK)

func build() -> void:
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(META_ROOT+"models.json"))
	var imports: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(META_ROOT+"godot_validation.json"))
	var tree_info := []
	for asset: Dictionary in manifest.assets:
		if asset.category != "tree": continue
		var document := GLTFDocument.new()
		var state := GLTFState.new()
		assert(document.append_from_file(MODEL_ROOT+asset.file,state) == OK)
		var source := document.generate_scene(state)
		var meshes := source.find_children("*","MeshInstance3D",true,false)
		if source is MeshInstance3D: meshes.push_front(source)
		assert(meshes.size() == 1)
		var model: MeshInstance3D = meshes[0]
		var triangles := 0
		for surface in model.mesh.get_surface_count():
			var arrays := model.mesh.surface_get_arrays(surface)
			triangles += arrays[Mesh.ARRAY_INDEX].size()/3
			var imported := model.mesh.surface_get_material(surface)
			var leaf := imported.resource_name.begins_with("FT_OakSprigs")
			var material_path := "foliage.tres" if leaf else ("bark_hero.tres" if asset.name == "ancient_oak" else "bark.tres")
			model.mesh.surface_set_material(surface,load("res://assets/materials/environment/forest_town/"+material_path))
		assert(triangles == int(asset.triangles) and triangles <= 8000)
		var importer := ImporterMesh.from_mesh(model.mesh)
		importer.generate_lods(60.0,0.0,[])
		var lod_counts := []
		for surface in importer.get_surface_count(): lod_counts.append(importer.get_surface_lod_count(surface))
		var tree_mesh := importer.get_mesh()
		assert(ResourceSaver.save(tree_mesh,MODEL_ROOT+asset.name+".mesh.res") == OK)
		tree_mesh.take_over_path(MODEL_ROOT+asset.name+".mesh.res")
		var prefab: Node3D = load(SCENE_ROOT+"models/"+asset.name+".tscn").instantiate()
		prefab.get_node("Model").mesh = tree_mesh
		prefab.set_meta("triangles",triangles)
		prefab.set_meta("tree_revision",2)
		save_scene(prefab,SCENE_ROOT+"models/"+asset.name+".tscn")
		prefab.free()
		source.free()
		for check: Dictionary in imports.checks:
			if check.asset == asset.name: check.lod_counts = lod_counts
		tree_info.append({"asset":asset.name,"triangles":triangles,"lod_counts":lod_counts})
	var town: Node3D = load(SCENE_ROOT+"forest_town.tscn").instantiate()
	var trees: Node3D = town.get_node("Trees")
	for child in trees.get_children(): child.free()
	var names := manifest.tree_upgrade.variants as Array
	for placement: Dictionary in manifest.instances:
		if not names.has(placement.asset): continue
		var packed: PackedScene = ResourceLoader.load(SCENE_ROOT+"models/"+placement.asset+".tscn","",ResourceLoader.CACHE_MODE_REPLACE)
		var instance: Node3D = packed.instantiate()
		instance.name = placement.name
		instance.position = Vector3(placement.position[0],placement.position[1],placement.position[2])
		instance.rotation.y = deg_to_rad(placement.rotation_y)
		instance.scale = Vector3.ONE*placement.scale
		trees.add_child(instance)
		instance.owner = town
	assert(trees.get_child_count() == int(manifest.tree_upgrade.trees_after))
	save_scene(town,SCENE_ROOT+"forest_town.tscn")
	town.free()
	var file := FileAccess.open(META_ROOT+"godot_validation.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(imports,"\t"))
	print("TREE_UPGRADE_IMPORTED: ",JSON.stringify(tree_info)," instances=",manifest.tree_upgrade.trees_after)
	quit()
