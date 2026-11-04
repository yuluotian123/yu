extends SceneTree
## Native render comparison: identical sun trajectory, authored orthographic camera,
## old world-up/High/2 against the production orbital basis/High/6.
const OUTPUT := "res://.godot/forest-shadow-stability/"
# Stay inside rigid surfaces: including adjacent wind-blown grass overstates shadow flicker.
const REGIONS := [Rect2i(1140,210,160,130),Rect2i(950,300,70,270),Rect2i(350,660,110,50)]
const REGION_NAMES := ["house","trunk","path"]
var profile: Resource
var sun: DirectionalLight3D
var moon: DirectionalLight3D
var samples: Array[Dictionary] = []

func _initialize() -> void:
	run.call_deferred()

func frames(count: int) -> void:
	for i in count: await process_frame

func apply_direction(hour: float, baseline: bool) -> void:
	profile.call("ApplyLightDirections",sun,moon,hour,.5)
	if baseline:
		for light: DirectionalLight3D in [sun,moon]:
			light.basis = Basis.looking_at(-light.basis.z,Vector3.UP)

func set_filter(baseline: bool) -> void:
	RenderingServer.directional_soft_shadow_filter_set_quality(RenderingServer.SHADOW_QUALITY_SOFT_HIGH)
	for light: DirectionalLight3D in [sun,moon]: light.shadow_blur = 2.0 if baseline else 6.0

func check_orbit() -> void:
	var tilt := deg_to_rad(clampf(float(profile.get("NoonElevation")),10,89))
	var azimuth := deg_to_rad(float(profile.get("Azimuth")))
	var up := Vector3(0,cos(tilt),-sin(tilt)).rotated(Vector3.UP,azimuth)
	for hour: float in [0.0,5.99,6.0,6.01,9.0,11.99,12.0,12.01,15.0,18.0,23.99]:
		apply_direction(hour,false)
		for index in 2:
			var light: DirectionalLight3D = [sun,moon][index]
			var angle := ((hour-6.0)/24.0-index*.5)*TAU
			var expected := Vector3(cos(angle),sin(angle)*sin(tilt),sin(angle)*cos(tilt)).rotated(Vector3.UP,azimuth)
			assert(light.basis.z.distance_to(expected) < .00001,"Celestial lighting direction changed")
			assert(light.basis.y.distance_to(up) < .00001,"Shadow basis must not roll around the sun direction")
	print("FOREST_SHADOW_ORBIT_VERIFIED")

func measure(baseline: bool, label: String) -> void:
	set_filter(baseline)
	var images: Array = []
	# Deterministic 4x day motion at 60 Hz: expose sampling jumps without changing gameplay time.
	for frame in 126:
		apply_direction(12.0+frame/3000.0,baseline)
		await process_frame
		if frame < 30 or (frame-30)%4 != 3: continue
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		var data: Array = []
		for region: Rect2i in REGIONS:
			var crop := image.get_region(region)
			crop.resize(64,32,Image.INTERPOLATE_LANCZOS)
			var values := PackedFloat32Array()
			for y in 32:
				for x in 64:
					var color := crop.get_pixel(x,y)
					values.append(Vector3(color.r,color.g,color.b).dot(Vector3(.2126,.7152,.0722)))
			data.append(values)
		images.append(data)
		if images.size() == 1: assert(image.save_png(OUTPUT+label+".png") == OK)
	var metrics := {}
	for region in REGIONS.size():
		var temporal := 0.0
		for pixel in 2048:
			for i in range(1,images.size()-1):
				temporal += pow(images[i-1][region][pixel]-2.0*images[i][region][pixel]+images[i+1][region][pixel],2)/float((images.size()-2)*2048)
		metrics[REGION_NAMES[region]] = sqrt(temporal)*255.0
	apply_direction(12.0,false)
	await frames(30)
	var gpu := PackedFloat32Array()
	for i in 120:
		await process_frame
		gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(root.get_viewport_rid()))
	var total := 0.0
	for value in gpu: total += value
	gpu.sort()
	var sample := {"phase":label,"temporal_curvature_255":metrics,"gpu_mean_ms":total/gpu.size(),"gpu_median_ms":gpu[60],"gpu_p95_ms":gpu[114]}
	samples.append(sample)
	print("FOREST_SHADOW_SAMPLE ",JSON.stringify(sample))

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	Engine.max_fps = 60
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	root.size = Vector2i(1401,788)
	var authored: Node3D = load("res://assets/scenes/levels/forest_town_level.tscn").instantiate()
	var scene := Node3D.new()
	var rig: Node3D = authored.get_node("EnvironmentRig")
	for child_name in ["EnvironmentRig","Village"]:
		var child := authored.get_node(child_name)
		child.owner = null
		authored.remove_child(child)
		scene.add_child(child)
	var camera := authored.get_node("CameraRig/Camera3D").duplicate() as Camera3D
	assert(camera.projection == Camera3D.PROJECTION_ORTHOGONAL,"Update image regions when changing camera projection")
	scene.add_child(camera)
	# Retain the diagnostic framing/ROIs while accommodating the authored near-plane clearance.
	camera.size = 7.0
	camera.position = Vector3(4.9739,4.15,32.12)+camera.basis.z*4.0
	camera.current = true
	authored.free()
	root.add_child(scene)
	current_scene = scene
	var day: Resource = rig.get_meta("time_of_day")
	profile = day.get("Profile")
	sun = rig.get_node("Sun")
	moon = rig.get_node("Moon")
	day.call("SetPaused",true)
	day.call("SetTimeOfDay",12.0)
	rig.get_meta("weather").call("SetAutomatic",false)
	rig.get_meta("weather").call("SetWeather",0,true)
	rig.get_meta("weather_surfaces").call("SetSurfaceState",0.0,0.0)
	rig.get_meta("weather_surfaces").call("SetSnowState",0.0)
	day.call("RefreshProfile")
	assert(is_equal_approx(sun.shadow_blur,6.0) and is_equal_approx(moon.shadow_blur,6.0))
	assert(is_equal_approx(sun.light_volumetric_fog_energy,6.0))
	assert(ProjectSettings.get_setting("rendering/lights_and_shadows/directional_shadow/soft_shadow_filter_quality") == 4)
	check_orbit()
	apply_direction(12.0,false)
	await frames(300)
	camera.make_current()
	var world: WorldEnvironment = rig.get_node("WorldEnvironment")
	var gi: Resource
	for effect in world.compositor.compositor_effects:
		if effect.has_method("GetStatus") and effect.call("GetStatus").has("native_receiver"): gi = effect
	assert(gi != null and gi.call("GetStatus").get("native_receiver",false))
	gi.set("FreezeUpdates",true)
	rig.process_mode = Node.PROCESS_MODE_DISABLED
	# Isolate direct shadow sampling from moving clouds, fog history and GI updates.
	world.environment.volumetric_fog_enabled = false
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	await measure(true,"baseline-a")
	await measure(false,"fixed-a")
	await measure(true,"baseline-b")
	await measure(false,"fixed-b")
	var delta := (float(samples[1].gpu_mean_ms)+float(samples[3].gpu_mean_ms)-float(samples[0].gpu_mean_ms)-float(samples[2].gpu_mean_ms))*.5
	sun.shadow_enabled = false
	await measure(false,"shadowless-control")
	var file := FileAccess.open(OUTPUT+"validation.json",FileAccess.WRITE)
	file.store_string(JSON.stringify({"viewport":str(root.size),"samples":samples,"paired_gpu_delta_ms":delta,"note":"ROI temporal second difference includes normal shadow motion. GI frozen, fog disabled, static camera; not full gameplay FPS or a guarantee of zero flicker."},"\t"))
	file.close()
	print("FOREST_SHADOW_VERIFIED paired_gpu_delta_ms=",delta)
	scene.queue_free()
	await frames(3)
	var driver := root.get_node_or_null("StandaloneModules")
	if driver != null: driver.queue_free()
	await frames(2)
	quit()
