extends SceneTree
## Verify independent model budgets, baseline rendering and actual scene collision.
var checks: Array = []

func _initialize() -> void:
	call_deferred("validate")

func check(condition: bool, label: String) -> void:
	checks.append({"check": label, "passed": condition})
	if not condition:
		push_error("Forest town validation: " + label)

func validate() -> void:
	var level: Node3D = load("res://assets/scenes/spacelevel.tscn").instantiate()
	var baseline: Node3D = load("res://assets/scenes/levels/whitebox_test.tscn").instantiate()
	var camera: Camera3D = level.get_node("CameraRig/Camera3D")
	var base_camera: Camera3D = baseline.get_node("CameraRig/Camera3D")
	check(camera.transform.is_equal_approx(base_camera.transform), "Camera transform unchanged")
	for property in ["fov", "far", "near", "size", "projection", "keep_aspect"]:
		check(camera.get(property) == base_camera.get(property), "Camera " + property + " unchanged")
	var components: Array = level.get_node("EnvironmentRig").get("Components")
	var base_components: Array = baseline.get_node("EnvironmentRig").get("Components")
	for i in range(components.size()):
		check(components[i] == base_components[i], "Environment component " + str(i) + " unchanged")
	var settings: Environment = level.get_node("EnvironmentRig/WorldEnvironment").environment
	var expected := {"background_intensity":26000.0,"ambient_light_energy":0.35000002,"fog_depth_begin":35.0,"fog_depth_end":240.0,"fog_density":0.38,"glow_intensity":0.03999999,"adjustment_contrast":1.04,"adjustment_saturation":1.08}
	for key in expected:
		check(is_equal_approx(float(settings.get(key)),float(expected[key])), "Environment " + key + " matches task baseline")
	check(level.get_node("RPGTown/Buildings").get_child_count() == 8, "Eight independent complete building instances")
	check(level.get_node("RPGTown/Props/LaneSteps/WalkableStairRamp/RampShape").shape is ConvexPolygonShape3D, "Stone stair run has a native walkable ramp")
	var river: MeshInstance3D = level.get_node("RPGTown/Water/ForegroundRiver/Model")
	check(river.mesh.surface_get_material(0).shader.resource_path == "res://assets/shaders/town/forest_river.gdshader", "River uses its dedicated painted flow shader")
	check(river.gi_mode == GeometryInstance3D.GI_MODE_DISABLED and river.cast_shadow == GeometryInstance3D.SHADOW_CASTING_SETTING_OFF, "Animated water does not become a solid DDGI occluder")
	var water_arrays: Array = river.mesh.surface_get_arrays(0)
	var water_vertices: PackedVector3Array = water_arrays[Mesh.ARRAY_VERTEX]
	var water_uvs: PackedVector2Array = water_arrays[Mesh.ARRAY_TEX_UV]
	var bank_z := 0.0
	var outer_z := 0.0
	var bank_count := 0
	var outer_count := 0
	for i in water_uvs.size():
		if is_zero_approx(water_uvs[i].y):
			bank_z += water_vertices[i].z
			bank_count += 1
		if is_equal_approx(water_uvs[i].y, 1.0):
			outer_z += water_vertices[i].z
			outer_count += 1
	check(bank_count > 0 and outer_count > 0 and outer_z / max(outer_count, 1) - bank_z / max(bank_count, 1) > 4.7, "Exported water UV distance starts at the town bank")
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_town_v1/models.json"))
	var tree_assets: Array = manifest.assets.filter(func(asset): return asset.category == "tree")
	check(tree_assets.size() == 3, "Three independently authored detailed tree variants")
	check(level.get_node("RPGTown/Trees").get_child_count() == int(manifest.tree_upgrade.trees_after), "Denser woodland instance count matches manifest")
	for asset: Dictionary in manifest.assets:
		check(int(asset.polygons) <= 8000 and int(asset.triangles) <= 8000, asset.name + " polygon and triangle budget")
		var prefab: Node3D = load("res://assets/scenes/environment/forest_town/models/" + asset.name + ".tscn").instantiate()
		var model: MeshInstance3D = prefab.get_node("Model")
		var materials_valid := model.material_override == null
		var cutout_count := 0
		for surface in model.mesh.get_surface_count():
			var material := model.mesh.surface_get_material(surface)
			if material is ShaderMaterial and material.shader.resource_path == "res://assets/shaders/town/leaf_article.gdshader":
				materials_valid = materials_valid and material.get_shader_parameter("leaf_texture") != null and material.get_shader_parameter("normal_roundness") == 0.0 and material.get_shader_parameter("wind_strength") == 0.0
				cutout_count += 1
			else:
				materials_valid = materials_valid and material is ShaderMaterial
		materials_valid = materials_valid and cutout_count == (1 if int(asset.get("cutout_sprigs", 0)) > 0 else 0)
		check(materials_valid, asset.name + " painted bark and cutout leaf material bindings")
		if asset.category == "tree":
			check(int(asset.get("leaf_sprays",0)) >= 900 and int(asset.revision) == 2, asset.name + " detailed layered canopy revision")
			check(model.cast_shadow != GeometryInstance3D.SHADOW_CASTING_SETTING_OFF, asset.name + " preserves native canopy shadows")
		prefab.free()
	baseline.free()
	root.add_child(level)
	current_scene = level
	for frame in range(30):
		await physics_frame
	var space := level.get_world_3d().direct_space_state
	var floor_query := PhysicsRayQueryParameters3D.create(Vector3(3.5,2,3),Vector3(3.5,-2,3),1)
	var floor_hit := space.intersect_ray(floor_query)
	check(not floor_hit.is_empty() and floor_hit.collider.get_parent().get_meta("asset_category", "") == "ground", "Walkable ground collision")
	var dense_grounded := true
	for tree: Node3D in level.get_node("RPGTown/Trees").get_children():
		if not str(tree.name).begins_with("DenseWoodland_"): continue
		var query := PhysicsRayQueryParameters3D.create(tree.global_position+Vector3.UP,tree.global_position-Vector3.UP,1)
		query.exclude = [tree.get_node("Collision1").get_rid()]
		var hit := space.intersect_ray(query)
		dense_grounded = dense_grounded and not hit.is_empty() and hit.collider.get_parent().get_meta("asset_category","") == "ground"
	check(dense_grounded,"All added woodland trunks stand on existing land")
	var wall_query := PhysicsRayQueryParameters3D.create(Vector3(.9,1.0,3),Vector3(.9,1.0,-7),1)
	var wall_hit := space.intersect_ray(wall_query)
	check(not wall_hit.is_empty() and wall_hit.collider.get_parent().get_meta("asset_category", "") == "building", "Inn blocks movement through walls")
	check(level.get_node("Player").position.y > .3, "Player remains above ground")
	var bank_hit := space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(.25,2,3.3),Vector3(.25,-2,3.3),1))
	check(not bank_hit.is_empty() and bank_hit.collider.get_parent().get_meta("asset_category", "") == "ground", "Land behind the river still supports characters")
	var water_hit := space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(.25,2,6),Vector3(.25,-2,6),1))
	check(water_hit.is_empty(), "River has no remaining invisible ground collider")
	var stairs: Node3D = level.get_node("RPGTown/Props/LaneSteps")
	var ramp_query := PhysicsRayQueryParameters3D.create(stairs.to_global(Vector3(0,3,-1.5)),stairs.to_global(Vector3(0,-1,-1.5)),1)
	var ramp_hit := space.intersect_ray(ramp_query)
	check(not ramp_hit.is_empty() and ramp_hit.collider.name == "WalkableStairRamp" and ramp_hit.normal.y > 0.9, "Character can stand on the gentle stair ramp")
	var landing_hit := space.intersect_ray(PhysicsRayQueryParameters3D.create(Vector3(5.4,3,-14),Vector3(5.4,0,-14),1))
	check(not landing_hit.is_empty() and landing_hit.position.y > 1.3, "Elevated lane continues beyond the stairs")
	var passed := true
	for item: Dictionary in checks:
		passed = passed and bool(item.passed)
	var file := FileAccess.open("res://assets/resources/art/forest_town_v1/scene_validation.json", FileAccess.WRITE)
	file.store_string(JSON.stringify({"passed":passed,"count":checks.size(),"checks":checks},"\t"))
	file.close()
	print("FOREST_TOWN_VALIDATION: ", checks.size(), " checks; passed=", passed)
	level.queue_free()
	await process_frame
	quit(0 if passed else 1)
