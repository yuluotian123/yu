extends SceneTree
## Native rendered turnarounds, mesh data, placement and rain/snow material checks.

const OUT := "res://.godot/forest-village-houses/"
const HOUSE_ROOT := "res://assets/scenes/environment/forest_village/houses/"
const ENV := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
var results: Array = []
var failures := 0
var stage: Node3D
var camera: Camera3D
var caption: Label

func _initialize() -> void:
	run.call_deferred()

func check(value: bool, message: String) -> void:
	if not value:
		failures += 1
		push_error(message)
	print("HOUSE_PASS " if value else "HOUSE_FAIL ",message)

func frames(count: int) -> void:
	for i in count: await process_frame

func shot(path: String) -> Image:
	await frames(3)
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	check(not image.is_empty(),"Rendered pixels: "+path)
	assert(image.save_png(OUT+path+".png") == OK)
	return image

func setup() -> void:
	root.size = Vector2i(800,600)
	root.content_scale_size = Vector2i(800,600)
	stage = Node3D.new()
	root.add_child(stage)
	var world := WorldEnvironment.new()
	world.environment = Environment.new()
	world.environment.background_mode = Environment.BG_COLOR
	world.environment.background_color = Color(0.22,0.27,0.26)
	world.environment.background_intensity = 22000.0
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	world.environment.ambient_light_color = Color(0.65,0.75,0.76)
	world.environment.ambient_light_energy = 0.9
	world.camera_attributes = CameraAttributesPhysical.new()
	world.camera_attributes.exposure_multiplier = 1.2
	(world.camera_attributes as CameraAttributesPhysical).exposure_shutter_speed = 81.5719
	stage.add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40,-35,0)
	sun.light_color = Color(1.0,0.91,0.78)
	sun.light_energy = 1.0
	sun.light_intensity_lux = 100000.0
	sun.shadow_enabled = true
	stage.add_child(sun)
	var floor_ := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(100,100)
	floor_.mesh = plane
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.25,0.29,0.25)
	mat.roughness = 1.0
	floor_.material_override = mat
	floor_.position.y = -0.04
	stage.add_child(floor_)
	var floor_body := StaticBody3D.new()
	var floor_shape := CollisionShape3D.new()
	var floor_box := BoxShape3D.new()
	floor_box.size = Vector3(100,0.1,100)
	floor_shape.shape = floor_box
	floor_body.add_child(floor_shape)
	floor_body.position.y = -0.05
	stage.add_child(floor_body)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.far = 200
	stage.add_child(camera)
	camera.current = true
	caption = Label.new()
	caption.position = Vector2(16,12)
	caption.add_theme_font_size_override("font_size",22)
	root.add_child(caption)

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	Engine.max_fps = 60
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	setup()
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_village/houses_models_v2.json"))
	var overview := Image.create_empty(2000,600,false,Image.FORMAT_RGBA8)
	for index: int in manifest.assets.size():
		var asset: Dictionary = manifest.assets[index]
		var house: Node3D = load(HOUSE_ROOT+asset.name+".tscn").instantiate()
		stage.add_child(house)
		var model: MeshInstance3D = house.get_node("Model")
		check(house.get_meta("complete_exterior",false),asset.name+" complete exterior")
		for side: String in ["front","back","left","right"]:
			check(asset.elevation_features[side].size() > 0,asset.name+" authored "+side)
		var mesh: ArrayMesh = model.mesh
		var triangles := 0
		for surface: int in mesh.get_surface_count():
			var arrays := mesh.surface_get_arrays(surface)
			var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
			var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
			triangles += indices.size()/3 if indices.size() else vertices.size()/3
			check(arrays[Mesh.ARRAY_TEX_UV].size() == vertices.size(),asset.name+" UV surface "+str(surface))
			check(arrays[Mesh.ARRAY_COLOR].size() == vertices.size(),asset.name+" palette surface "+str(surface))
			check(arrays[Mesh.ARRAY_TANGENT].size() == vertices.size()*4,asset.name+" tangent frame surface "+str(surface))
			var material := mesh.surface_get_material(surface) as ShaderMaterial
			check(material != null and material.shader.resource_path == "res://assets/shaders/town/town_toon.gdshader",asset.name+" weather/GI surface "+str(surface))
			check(material.get_shader_parameter("use_normal_texture") == true and (material.get_shader_parameter("normal_texture") as Texture2D).get_width() == 2048,asset.name+" normal map surface "+str(surface))
			check(material.get_shader_parameter("use_roughness_texture") == true and (material.get_shader_parameter("roughness_texture") as Texture2D).get_width() == 2048,asset.name+" roughness map surface "+str(surface))
		check(triangles == int(asset.triangles) and triangles >= 10000 and triangles <= int(manifest.budget_triangles),asset.name+" 10k-20k triangle budget")
		check(house.get_meta("exterior_only",false),asset.name+" exterior only")
		check(asset.entrance_audit.posts_clear and asset.entrance_audit.threshold_supported and asset.entrance_audit.marker_error_m < 0.01,asset.name+" entrance geometry")
		check(int(house.get_meta("detail_revision",0)) == 3,asset.name+" detailed exterior revision")
		var bounds := model.get_aabb()
		var center := bounds.get_center()
		camera.size = maxf(bounds.size.y,maxf(bounds.size.x*0.82,bounds.size.z*0.82))*1.45
		var contact := Image.create_empty(1600,1200,false,Image.FORMAT_RGBA8)
		var directions := [Vector3(0,0,1),Vector3(0,0,-1),Vector3(-1,0,0),Vector3(1,0,0)]
		var names_ := ["front","back","left","right"]
		for side: int in 4:
			caption.text = asset.name+" / "+names_[side]
			camera.position = center+directions[side]*20
			camera.look_at(center)
			var pixels := await shot(asset.name+"_"+names_[side])
			pixels.convert(Image.FORMAT_RGBA8)
			pixels.resize(800,600)
			contact.blit_rect(pixels,Rect2i(0,0,800,600),Vector2i((side%2)*800,(side/2)*600))
		assert(contact.save_png(OUT+asset.name+"_four_sides.png") == OK)
		camera.position = center+Vector3(1,0.64,1).normalized()*20
		camera.look_at(center)
		caption.text = asset.name
		await shot(asset.name+"_three_quarter")
		# Preserve proportions in the overview, including a common neutral backdrop.
		var original := Image.load_from_file(OUT+asset.name+"_three_quarter.png")
		original.convert(Image.FORMAT_RGBA8)
		original.resize(400,300,Image.INTERPOLATE_LANCZOS)
		overview.blit_rect(original,Rect2i(0,0,400,300),Vector2i((index%5)*400,(index/5)*300))
		var entrance_passed := await probe_entrance(house,asset)
		results.append({"asset":asset.name,"triangles":triangles,"four_views":true,"entrance_walk":entrance_passed,"normal_and_roughness_maps":true})
		house.queue_free()
		await process_frame
	assert(overview.save_png(OUT+"overview.png") == OK)
	stage.queue_free()
	await process_frame
	await check_level()
	var output := FileAccess.open("res://assets/resources/art/forest_village/houses_runtime_validation.json",FileAccess.WRITE)
	output.store_string(JSON.stringify({"assets":results,"all_passed":failures == 0,"failures":failures},"\t"))
	print("FOREST_HOUSES_VALIDATED: ten meshes, forty elevations, native rendering and material weather states; failures=",failures)
	quit(0 if failures == 0 else 1)

func ancestor_in_group(node: Node, group: String) -> Node:
	while node != null:
		if node.is_in_group(group): return node
		node = node.get_parent()
	return null

func probe_entrance(house: Node3D, asset: Dictionary) -> bool:
	var body := CharacterBody3D.new()
	var shape := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = 0.25
	capsule.height = 0.936
	shape.shape = capsule
	body.add_child(shape)
	body.floor_snap_length = 0.12
	stage.add_child(body)
	var approach: Vector3 = house.get_node("ApproachAnchor").global_position
	var door: Vector3 = house.get_node("DoorAnchor").global_position
	var target := door+house.global_basis*Vector3(0,0,0.35)
	body.global_position = Vector3(approach.x,0.52,approach.z)
	for frame in 180:
		await physics_frame
		var delta := body.get_physics_process_delta_time()
		var remaining := Vector2(target.x-body.global_position.x,target.z-body.global_position.z)
		var move := remaining.normalized()*minf(1.5,remaining.length()/delta)
		body.velocity = Vector3(move.x,body.velocity.y-16.0*delta,move.y)
		body.move_and_slide()
	var distance := Vector2(target.x-body.global_position.x,target.z-body.global_position.z).length()
	print("ENTRANCE_PHYSICS ",asset.name," distance=",distance," floor=",body.is_on_floor()," bottom=",body.global_position.y-0.468," threshold=",door.y)
	var passed := distance<0.03 and body.is_on_floor() and body.global_position.y-0.468>=door.y-0.15
	check(passed,asset.name+" stairs to closed door walk without jumping")
	body.queue_free()
	await physics_frame
	return passed

func check_level() -> void:
	caption.text = "Forest village / native level"
	var authored: Node3D = load("res://assets/scenes/levels/forest_town_level.tscn").instantiate()
	var scene := Node3D.new()
	var rig: Node3D = authored.get_node("EnvironmentRig")
	var village: Node3D = authored.get_node("Village")
	for child: Node3D in [rig,village]:
		child.owner = null
		authored.remove_child(child)
		scene.add_child(child)
	var game_camera := authored.get_node("CameraRig/Camera3D").duplicate() as Camera3D
	scene.add_child(game_camera)
	authored.free()
	root.add_child(scene)
	game_camera.current = true
	await frames(5)
	await physics_frame
	var overlaps: Array = []
	var space := scene.get_world_3d().direct_space_state
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_village/houses_models_v2.json"))
	check(village.get_node("Households").get_child_count() == manifest.assets.size(),"Ten persisted household replacements")
	for asset: Dictionary in manifest.assets:
		var building: Node3D = village.get_node("Households/"+asset.household+"/Building")
		check(building.scene_file_path == HOUSE_ROOT+asset.name+".tscn",asset.household+" uses its corresponding model")
		check(building.transform.is_equal_approx(Transform3D.IDENTITY),asset.household+" retains original pivot and direction")
	for home: Node3D in village.get_node("Households").get_children():
		var building := home.get_node("Building") as Node3D
		check(building.get_meta("complete_exterior",false),str(home.name)+" uses new exterior")
		var body := building.get_node("Collision1") as StaticBody3D
		for shape: CollisionShape3D in body.get_children():
			var query := PhysicsShapeQueryParameters3D.new()
			query.shape = shape.shape
			query.transform = shape.global_transform
			query.exclude = [body.get_rid()]
			query.collision_mask = 1
			for hit: Dictionary in space.intersect_shape(query,32):
				var tree := ancestor_in_group(hit.collider,"forest_tree")
				if tree != null:
					var issue := str(home.name)+" / "+str(tree.get_path())
					if not issue in overlaps: overlaps.append(issue)
	var placement_report := FileAccess.open(OUT+"tree_overlaps.json",FileAccess.WRITE)
	placement_report.store_string(JSON.stringify(overlaps,"\t"))
	print("HOUSE_TREE_OVERLAPS ",JSON.stringify(overlaps))
	check(overlaps.is_empty(),"House colliders clear existing tree trunks")
	var day: Resource = rig.get_meta("time_of_day")
	var weather: Resource = rig.get_meta("weather")
	var surfaces: Resource = rig.get_meta("weather_surfaces")
	day.call("SetPaused",true)
	day.call("SetTimeOfDay",12.0)
	weather.call("SetAutomatic",false)
	weather.call("SetWeather",0,true)
	surfaces.call("SetSurfaceState",0.0,0.0)
	surfaces.call("SetSnowState",0.0)
	await frames(60)
	await shot("level_clear")
	var original_transform := game_camera.transform
	var original_size := game_camera.size
	root.size = Vector2i(1440,1080)
	root.content_scale_size = Vector2i(1440,1080)
	for household: String in ["Chief","Carpenter","FarmerWest"]:
		var home: Node3D = village.get_node("Households/"+household)
		var center := home.global_position+Vector3(0,1.35,0)
		game_camera.size = 6.2
		game_camera.global_position = center+home.global_basis*Vector3(6.5,3.3,10.0)
		game_camera.look_at(center)
		caption.text = ""
		await frames(30)
		await shot("level_detail_"+household.to_snake_case())
	root.size = Vector2i(800,600)
	root.content_scale_size = Vector2i(800,600)
	game_camera.transform = original_transform
	game_camera.size = original_size
	for mode: int in [2,3]:
		weather.call("SetWeather",mode,true)
		surfaces.call("SetSurfaceState",1.0 if mode == 2 else 0.0,1.0 if mode == 2 else 0.0)
		surfaces.call("SetSnowState",1.0 if mode == 3 else 0.0)
		await frames(12)
		for home: Node3D in village.get_node("Households").get_children():
			var model: MeshInstance3D = home.get_node("Building/Model")
			for surface: int in model.mesh.get_surface_count():
				var mat := model.get_active_material(surface) as ShaderMaterial
				check(is_equal_approx(float(mat.get_shader_parameter("weather_snow")),1.0 if mode == 3 else 0.0),str(home.name)+" snow state "+str(mode))
		await shot("level_rain" if mode == 2 else "level_snow")
	scene.queue_free()
	await process_frame
