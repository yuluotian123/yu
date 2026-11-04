extends SceneTree
## Native rendered model/context review, runtime surface binding and physical crossing.

const OUT := "res://.godot/forest-village-structures/"
const LIB := preload("res://scripts/tools/art/forest_village_structures/structure_library.gd")
var failures := 0
var checks := 0
var stage: Node3D
var camera: Camera3D
var results: Dictionary = {}

func _initialize() -> void:
	run.call_deferred()

func check(passed: bool, description: String) -> void:
	checks += 1
	if not passed: failures += 1
	print("STRUCTURE_PASS " if passed else "STRUCTURE_FAIL ",description)

func frames(count: int) -> void:
	for i: int in count: await process_frame

func shot(name_: String) -> Image:
	await frames(4)
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	check(not image.is_empty(),"native pixels "+name_)
	assert(image.save_png(OUT+name_+".png") == OK)
	return image

func image_difference(a: Image, b: Image) -> float:
	var difference := 0.0
	for y: int in range(0,a.get_height(),3):
		for x: int in range(0,a.get_width(),3):
			var c := a.get_pixel(x,y)
			var d := b.get_pixel(x,y)
			difference += absf(c.r-d.r)+absf(c.g-d.g)+absf(c.b-d.b)
	return difference

func setup() -> void:
	root.size = Vector2i(1200,800)
	root.content_scale_size = root.size
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
	sun.light_intensity_lux = 100000.0
	sun.shadow_enabled = true
	stage.add_child(sun)
	camera = Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.far = 200
	stage.add_child(camera)
	camera.current = true

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUT)
	Engine.max_fps = 90
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	setup()
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_village/structures_models.json"))
	for asset: Dictionary in manifest.assets:
		var mesh: ArrayMesh = load(LIB.MODELS+asset.name+".mesh.res")
		var triangles := 0
		for surface: int in mesh.get_surface_count():
			var data := mesh.surface_get_arrays(surface)
			var vertices: PackedVector3Array = data[Mesh.ARRAY_VERTEX]
			var indices: PackedInt32Array = data[Mesh.ARRAY_INDEX]
			check(data[Mesh.ARRAY_TANGENT].size() == vertices.size()*4,asset.name+" tangent frames "+str(surface))
			var tangents: PackedFloat32Array = data[Mesh.ARRAY_TANGENT]
			var valid := true
			for i: int in vertices.size():
				valid = valid and Vector3(tangents[i*4],tangents[i*4+1],tangents[i*4+2]).length()>.9
			check(valid,asset.name+" normalized nonzero tangents "+str(surface))
			check(data[Mesh.ARRAY_TEX_UV].size() == vertices.size() and data[Mesh.ARRAY_COLOR].size() == vertices.size(),asset.name+" authored UV and palette "+str(surface))
			var mat := mesh.surface_get_material(surface) as ShaderMaterial
			check(mat.get_shader_parameter("use_normal_texture") == true and mat.get_shader_parameter("normal_texture").get_width() == 2048,asset.name+" 2K normal map "+str(surface))
			check(mat.get_shader_parameter("use_roughness_texture") == true,asset.name+" roughness map "+str(surface))
			triangles += indices.size()/3 if indices.size() else vertices.size()/3
		check(triangles == int(asset.triangles),asset.name+" triangle count")
	var bridge: Node3D = load(LIB.SCENES+"creek_bridge.tscn").instantiate()
	stage.add_child(bridge)
	camera.size = 7.5
	camera.position = Vector3(7,4.4,8)
	camera.look_at(Vector3(0,0.18,0))
	var model_normal_on := await shot("bridge_model")
	var bridge_mesh: ArrayMesh = bridge.get_node("Model").mesh
	for surface: int in bridge_mesh.get_surface_count():
		bridge_mesh.surface_get_material(surface).set_shader_parameter("use_normal_texture",false)
	var model_normal_off := await shot("bridge_model_normal_off")
	for surface: int in bridge_mesh.get_surface_count():
		bridge_mesh.surface_get_material(surface).set_shader_parameter("use_normal_texture",true)
	var difference := image_difference(model_normal_on,model_normal_off)
	check(difference>.1,"isolated static model shows normal map lighting response")
	results["isolated_normal_ab_total_difference"] = difference
	camera.position = Vector3(7,-1.1,7)
	camera.look_at(Vector3(0,-.20,0))
	await shot("bridge_underside")
	stage.remove_child(bridge)
	bridge.free()
	var fence := LIB.new().fence("FenceReview",Vector3(-3,0,0),Vector3(3,0,0))
	stage.add_child(fence)
	camera.size = 4.8
	camera.position = Vector3(4,2.4,6)
	camera.look_at(Vector3(0,0.4,0))
	await shot("fence_model")
	stage.remove_child(fence)
	fence.free()
	stage.queue_free()
	await process_frame
	await context_review()
	results["checks"] = checks
	results["failures"] = failures
	results["all_passed"] = failures == 0
	var file := FileAccess.open("res://assets/resources/art/forest_village/structures_validation.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(results,"\t"))
	print("STRUCTURES_VALIDATED ",checks," checks; failures=",failures)
	quit(0 if failures == 0 else 1)

func context_review() -> void:
	var authored: Node3D = load("res://assets/scenes/levels/forest_town_level.tscn").instantiate()
	var scene := Node3D.new()
	var rig: Node3D = authored.get_node("EnvironmentRig")
	var village: Node3D = authored.get_node("Village")
	for node: Node3D in [rig,village]:
		node.owner = null
		authored.remove_child(node)
		scene.add_child(node)
	camera = authored.get_node("CameraRig/Camera3D").duplicate()
	scene.add_child(camera)
	authored.free()
	root.add_child(scene)
	current_scene = scene
	camera.current = true
	camera.projection = Camera3D.PROJECTION_PERSPECTIVE
	camera.fov = 43
	var day: Resource = rig.get_meta("time_of_day")
	var weather: Resource = rig.get_meta("weather")
	var surfaces: Resource = rig.get_meta("weather_surfaces")
	day.call("SetPaused",true)
	day.call("SetTimeOfDay",12.0)
	weather.call("SetAutomatic",false)
	weather.call("SetWeather",0,true)
	surfaces.call("SetSurfaceState",0.0,0.0)
	surfaces.call("SetSnowState",0.0)
	await frames(45)
	var all_meshes: Array = village.get_node("Bridge").find_children("*","MeshInstance3D",true,false)
	var fence_count := 0
	for node: Node in village.find_children("Fence_*","Node3D",true,false):
		check(node.get_meta("asset_category","") == "finished_timber_fence","finished fence "+str(village.get_path_to(node)))
		all_meshes.append_array(node.find_children("*","MeshInstance3D",true,false))
		fence_count += 1
	check(fence_count == 24,"24 persistent fence runs")
	var runtime_mats := {}
	for model: MeshInstance3D in all_meshes:
		check(model.mesh is ArrayMesh,"no visible primitive "+str(model.name))
		for surface: int in model.mesh.get_surface_count():
			var mat := model.get_active_material(surface) as ShaderMaterial
			check(mat != model.mesh.surface_get_material(surface),"runtime weather material copy "+str(model.name)+"/"+str(surface))
			runtime_mats[mat] = true
	check(runtime_mats.size() == 4,"four shared runtime structure materials")
	camera.position = Vector3(35.7,4.9,11.8)
	camera.look_at(Vector3(28.1,.20,2.34))
	await frames(60)
	await shot("bridge_in_forest")
	camera.position = Vector3(31.7,2.7,7.7)
	camera.look_at(Vector3(27.7,.30,2.34))
	await frames(35)
	await shot("bridge_normal_on")
	for mat: ShaderMaterial in runtime_mats: mat.set_shader_parameter("use_normal_texture",false)
	await shot("bridge_normal_off")
	for mat: ShaderMaterial in runtime_mats: mat.set_shader_parameter("use_normal_texture",true)
	for mode: int in [2,3]:
		weather.call("SetWeather",mode,true)
		surfaces.call("SetSurfaceState",1.0 if mode == 2 else 0.0,0.0)
		surfaces.call("SetSnowState",1.0 if mode == 3 else 0.0)
		await frames(18)
		for mat: ShaderMaterial in runtime_mats:
			check(is_equal_approx(float(mat.get_shader_parameter("weather_snow")),1.0 if mode == 3 else 0.0),"structure snow state "+str(mode))
			check(is_equal_approx(float(mat.get_shader_parameter("weather_wetness")),1.0 if mode == 2 else 0.0),"structure wetness state "+str(mode))
			check(mat.get_shader_parameter("use_normal_texture") == true,"normal remains bound in weather "+str(mode))
		await shot("bridge_rain" if mode == 2 else "bridge_snow")
	weather.call("SetWeather",0,true)
	surfaces.call("SetSurfaceState",0.0,0.0)
	surfaces.call("SetSnowState",0.0)
	camera.position = Vector3(21.0,1.85,-12.9)
	camera.look_at(Vector3(21.45,.45,-8.58))
	await frames(45)
	await shot("fence_in_forest")
	await crossing(scene)
	results["fence_runs"] = fence_count
	results["runtime_materials"] = runtime_mats.size()
	scene.queue_free()
	await process_frame

func crossing(scene: Node3D) -> void:
	var body := CharacterBody3D.new()
	var shape := CollisionShape3D.new()
	var capsule := CapsuleShape3D.new()
	capsule.radius = .25
	capsule.height = .936
	shape.shape = capsule
	body.add_child(shape)
	body.floor_snap_length = .12
	scene.add_child(body)
	for direction: float in [1.0,-1.0]:
		body.position = Vector3(28.08-3.55*direction,.52,2.34)
		var target := Vector3(28.08+3.55*direction,.52,2.34)
		var min_height := INF
		for i: int in 300:
			await physics_frame
			var dt := body.get_physics_process_delta_time()
			var remaining := target.x-body.position.x
			body.velocity = Vector3(signf(remaining)*minf(2.2,absf(remaining)/dt),body.velocity.y-16*dt,0)
			body.move_and_slide()
			min_height = minf(min_height,body.position.y-.468)
		check(absf(body.position.x-target.x)<.04 and body.is_on_floor(),"capsule crosses bridge "+str(direction)+" without jumping")
		check(min_height>-.1,"bridge has continuous physical support "+str(direction))
	body.queue_free()
