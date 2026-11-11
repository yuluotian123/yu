extends SceneTree
## Native Vulkan validation of serialized tuft instances, weather copies and GI topology caching.
func _initialize() -> void:
	run.call_deferred()
func frames(count: int) -> void:
	for i in count: await process_frame
func capture(label: String) -> void:
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	assert(image.save_png("res://.godot/forest-performance/"+label+".png") == OK)
	assert(image.get_region(Rect2i(130,310,230,155)).save_png("res://.godot/forest-performance/"+label+"-detail.png") == OK)
	print("GI_AB_CAPTURE ",label)
func run() -> void:
	DirAccess.make_dir_recursive_absolute("res://.godot/forest-performance/")
	var authored: Node3D = load("res://assets/scenes/levels/forest_town_level.tscn").instantiate()
	var scene := Node3D.new()
	scene.name = "ForestConceptReview"
	var rig: Node3D = authored.get_node("EnvironmentRig")
	for child_name in ["EnvironmentRig", "Village"]:
		var child := authored.get_node(child_name)
		child.owner = null
		authored.remove_child(child)
		scene.add_child(child)
	var camera := authored.get_node("CameraRig/Camera3D").duplicate() as Camera3D
	scene.add_child(camera)
	camera.current = true
	camera.position = Vector3(1.56,4.15,32.12)
	var entry: Node3D = authored.get_node("EntryDefault")
	var actor_position: Vector3 = entry.transform * entry.get_node("Arrival").position
	authored.free()
	var player: Node3D = load("res://assets/scenes/player.tscn").instantiate()
	var visual: Node3D = player.get_node("VisualRoot")
	visual.owner = null
	player.remove_child(visual)
	var actor := Node3D.new()
	actor.name = "StaticPlayerVisual"
	actor.position = actor_position
	actor.add_child(visual)
	var player_light := player.get_node_or_null("AreaLight3D")
	if player_light != null:
		player_light.owner = null
		player.remove_child(player_light)
		actor.add_child(player_light)
	scene.add_child(actor)
	player.free()
	root.add_child(scene)
	current_scene = scene
	var weather: Resource = rig.get_meta("weather")
	var surfaces: Resource = rig.get_meta("weather_surfaces")
	weather.call("SetAutomatic", false)
	weather.call("SetWeather", 0, true)
	surfaces.call("SetSurfaceState", 0.0, 0.0)
	surfaces.call("SetSnowState", 0.0)
	var ddgi: Resource
	for component in rig.get("Components"):
		if component.get_script().resource_path.ends_with("DdgiComponent3D.cs"): ddgi = component
	assert(ddgi != null)
	var profile: Resource = ddgi.get("Profile")
	assert(is_equal_approx(float(profile.get("Energy")),2.5))
	var probe_counts: Vector3i = profile.get("ProbeCounts")
	var probe_bounds := AABB(profile.get("Origin"), Vector3(probe_counts - Vector3i.ONE) * float(profile.get("Spacing")))
	for instance: GeometryInstance3D in scene.get_node("Village").find_children("*", "GeometryInstance3D", true, false):
		if instance.is_visible_in_tree():
			assert(probe_bounds.encloses(instance.global_transform * instance.get_aabb()), "Outside forest DDGI: %s" % instance.get_path())
	var day: Resource = rig.get_meta("time_of_day")
	for light_name in ["Sun","Moon"]:
		var light: DirectionalLight3D = rig.get_node(light_name)
		assert(is_equal_approx(light.directional_shadow_max_distance,80.0))
		assert(light.directional_shadow_mode == DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS)
		assert(is_equal_approx(light.shadow_blur,6.0))
	assert(ProjectSettings.get_setting("rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality") == 4)
	assert(not root.use_taa)
	var bark_materials := {}
	var hero_tree: Node3D
	var nearest := INF
	var tree_count := 0
	for tree: Node3D in scene.get_node("Village/Forest").find_children("Tree_*","Node3D",true,false):
		var model: MeshInstance3D = tree.get_node("Model")
		var bark: ShaderMaterial = model.get_active_material(0)
		assert(bark.get_shader_parameter("use_normal_texture") == true)
		assert(bark.get_shader_parameter("normal_texture").resource_path == "res://assets/textures/environment/forest_village/character_style/bark_normal.res")
		assert(is_equal_approx(float(bark.get_shader_parameter("normal_scale")),0.65))
		assert(float(bark.get_shader_parameter("wet_film_strength")) < .1)
		var arrays := model.mesh.surface_get_arrays(0)
		assert(arrays[Mesh.ARRAY_TANGENT].size() == arrays[Mesh.ARRAY_VERTEX].size()*4)
		bark_materials[bark] = true
		tree_count += 1
		var distance := tree.global_position.distance_squared_to(Vector3(-1,0,23))
		if distance < nearest:
			nearest = distance
			hero_tree = tree
	assert(tree_count == 274)
	var verge_patches := 0
	for patch in scene.get_node("Village/GroundContact").get_children():
		if not String(patch.name).begins_with("Grass_"): continue
		var verge: ShaderMaterial = patch.get_active_material(0)
		assert(verge.shader.resource_path == "res://assets/shaders/town/plant_painterly.gdshader")
		assert(verge.get_shader_parameter("ground_contact_map") != null)
		assert(float(verge.get_shader_parameter("floor_color_mix")) > 0.0)
		verge_patches += 1
	assert(verge_patches > 0)
	var flora := scene.get_node("Village/GroundFlora")
	assert(flora.find_children("*","CollisionObject3D",true,false).is_empty())
	var counts: Dictionary = flora.get_meta("plant_counts")
	assert(counts.grass > 3000 and counts.grass < 10000 and counts.fern > 400 and counts.rosette > 700 and counts.flowers > 450)
	var plant_materials := {}
	var instances := 0
	for patch in flora.get_children():
		assert(patch.get_meta("ddgi_contribute") == false)
		var material: ShaderMaterial
		if patch is MultiMeshInstance3D:
			var data: PackedFloat32Array = patch.multimesh.buffer
			assert(data.size() == patch.multimesh.instance_count*16)
			for i in patch.multimesh.instance_count:
				assert(absf(patch.multimesh.get_instance_transform(i).basis.determinant()) > .01)
				assert(patch.multimesh.get_instance_custom_data(i).a > .8)
			instances += patch.multimesh.instance_count
			material = patch.material_override
			assert(material != null and material.shader.resource_path == "res://assets/shaders/town/grass_tuft.gdshader")
			assert(patch.multimesh.mesh.surface_get_arrays(0)[Mesh.ARRAY_INDEX].size() == 36)
		else:
			assert(patch is MeshInstance3D and patch.mesh.resource_path.begins_with("res://assets/models/environment/forest_village/ground_flora/"))
			material = patch.get_active_material(0)
			assert(material.shader.resource_path == "res://assets/shaders/town/plant_painterly.gdshader")
		assert(float(material.get_shader_parameter("wind_strength")) > 0.0)
		plant_materials[patch.get_meta("plant_kind")] = material
	assert(plant_materials.size() == 4)
	for kind in ["rosette","fern","flowers"]:
		assert(float(plant_materials[kind].get_shader_parameter("grass_normal_blend")) > 0.0)
	assert(instances == int(counts.grass))
	var atlas: Image = plant_materials.grass.get_shader_parameter("grass_atlas").get_image()
	assert(atlas.get_pixel(1,1).a == 0 and atlas.get_pixel(160,250).a > .5)
	await frames(400)
	var baseline_instances := 0
	for effect in rig.get_node("WorldEnvironment").compositor.compositor_effects:
		if effect.has_method("GetStatus"):
			var status: Dictionary = effect.call("GetStatus")
			if status.has("native_receiver"):
				assert(status.get("state","") == "running" and status.get("native_receiver",false))
				var protocols: Dictionary = status["material_counts"]
				var shrubs := get_nodes_in_group("forest_shrub").size()
				assert(int(protocols.get("leaf",0)) == tree_count + shrubs)
				assert(int(protocols.get("town",0)) >= tree_count + shrubs)
				baseline_instances = int(status["instances"])
				assert(baseline_instances >= 2*(tree_count+shrubs))
				print("FLORA_NATIVE_GI ",status)
	var active_gi: Resource
	for effect in rig.get_node("WorldEnvironment").compositor.compositor_effects:
		if effect.has_method("GetStatus") and effect.call("GetStatus").has("native_receiver"): active_gi = effect
	var box := MeshInstance3D.new()
	box.mesh = BoxMesh.new()
	box.position = Vector3(3,.5,20)
	scene.add_child(box)
	await frames(8)
	assert(active_gi.call("GetStatus").get("instances",0) == baseline_instances+1)
	var epoch := int(active_gi.call("GetStatus").get("local_invalidations",0))
	box.position.x += 1.0
	await frames(8)
	assert(int(active_gi.call("GetStatus").get("local_invalidations",0)) > epoch)
	box.free()
	await frames(8)
	assert(active_gi.call("GetStatus").get("instances",0) == baseline_instances)
	print("FOREST_GI_CACHE_VALIDATED: added, moved and removed geometry captured; native GI unchanged")
	await capture("01-overview")
	var saved_camera := camera.transform
	var village := scene.get_node("Village")
	var course := preload("res://scripts/tools/art/forest_village_whitebox/river_course.gd").new()
	assert(village.has_meta("river_course_revision"))
	var stones: MultiMeshInstance3D = village.get_node("Terrain/RiverStones")
	assert(stones.multimesh.instance_count == 40 and stones.multimesh.buffer.size() == 40*12)
	assert(stones.find_children("*","CollisionShape3D",true,false).is_empty())
	var litter: MeshInstance3D = village.get_node("GroundContact/RootLitter")
	var litter_arrays := litter.mesh.surface_get_arrays(0)
	var litter_vertices: PackedVector3Array = litter_arrays[Mesh.ARRAY_VERTEX]
	for vertex: Vector3 in litter_vertices:
		assert(course.dry(Vector2(vertex.x,vertex.z),.395),"Root litter must remain outside the river bank: %s" % vertex)
	var litter_indices: PackedInt32Array = litter_arrays[Mesh.ARRAY_INDEX]
	if litter_indices.is_empty():
		litter_indices.resize(litter_vertices.size())
		for i in litter_indices.size(): litter_indices[i] = i
	for i in range(0,litter_indices.size(),3):
		for edge in 3:
			var a := litter_vertices[litter_indices[i+edge]]
			var b := litter_vertices[litter_indices[i+(edge+1)%3]]
			if maxf(a.x,b.x) < 24.5 or minf(a.x,b.x) > 31.8: continue
			var steps := maxi(1,ceili(a.distance_to(b)/.05))
			for step in range(1,steps):
				var point := a.lerp(b,float(step)/steps)
				assert(course.dry(Vector2(point.x,point.z),.395),"Root litter edge must remain outside the curved bank: %s" % point)
	camera.position = Vector3(28.2,4.3,34.5)
	camera.look_at(Vector3(27.9,-.15,20.0),Vector3.UP)
	await frames(90)
	await capture("06-river-course")
	print("FOREST_RIVER_VERIFIED: root litter clipped to dry banks; 40 decorative stones; curved water and bank colliders")
	camera.position = Vector3(5.0,1.7,24.0)
	camera.look_at(Vector3(3.0,.18,20.0),Vector3.UP)
	await frames(60)
	await capture("02-closeup")
	# Render the actual tree normal path with a fixed environment for visual A/B.
	var rig_process := rig.process_mode
	rig.process_mode = Node.PROCESS_MODE_DISABLED
	active_gi.set("FreezeUpdates",true)
	camera.position = hero_tree.global_position+Vector3(2.3,1.5,3.6)
	camera.look_at(hero_tree.global_position+Vector3(0,1.15,0),Vector3.UP)
	await frames(12)
	await capture("03-bark-normal")
	for bark: ShaderMaterial in bark_materials: bark.set_shader_parameter("normal_scale",0.0)
	await frames(8)
	await capture("03-bark-flat")
	for bark: ShaderMaterial in bark_materials: bark.set_shader_parameter("normal_scale",0.65)
	active_gi.set("FreezeUpdates",false)
	rig.process_mode = rig_process
	print("FOREST_BARK_NORMAL_VERIFIED: ",tree_count," trees; valid tangents, runtime weather copies and aligned normal texture")
	camera.transform = saved_camera
	weather.call("SetWeather",2,true)
	surfaces.call("SetSurfaceState",1.0,1.0)
	surfaces.call("SetSnowState",0.0)
	await frames(180)
	for material: ShaderMaterial in plant_materials.values(): assert(float(material.get_shader_parameter("weather_wetness")) > .98)
	for bark: ShaderMaterial in bark_materials:
		assert(float(bark.get_shader_parameter("weather_wetness")) > .98)
		assert(float(bark.get_shader_parameter("wet_film_strength")) < .1)
	await capture("04-rain")
	weather.call("SetWeather",3,true)
	surfaces.call("SetSurfaceState",0.0,0.0)
	surfaces.call("SetSnowState",1.0)
	await frames(180)
	for material: ShaderMaterial in plant_materials.values(): assert(float(material.get_shader_parameter("weather_snow")) > .98)
	await capture("05-snow")
	print("FOREST_VEGETATION_VERIFIED ",counts,"; four weather-bound wind materials; native GI receives; zero colliders; saved native mesh assets")
	scene.queue_free()
	await frames(3)
	quit()
