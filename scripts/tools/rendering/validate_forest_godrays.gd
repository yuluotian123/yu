extends SceneTree
## Native forward+ check of authored forest shafts, weather restoration and paired GPU cost.
const OUTPUT := "res://.godot/forest-godrays/"
var samples: Array[Dictionary] = []

func _initialize() -> void:
	run.call_deferred()

func frames(count: int) -> void:
	for i in count: await process_frame

func capture(label: String) -> void:
	await RenderingServer.frame_post_draw
	assert(root.get_texture().get_image().save_png(OUTPUT+label+".png") == OK)
	print("FOREST_GODRAYS_CAPTURE ",label)

func check_energy(sun: DirectionalLight3D, moon: DirectionalLight3D, expected: float) -> void:
	assert(is_equal_approx(sun.light_volumetric_fog_energy,expected),"Unexpected sunlight scattering after weather/day update")
	assert(is_zero_approx(moon.light_volumetric_fog_energy),"Moon scattering must remain disabled")

func measure(environment: Environment, enabled: bool, label: String) -> void:
	environment.volumetric_fog_enabled = enabled
	await frames(20)
	var gpu: Array[float] = []
	var cpu := 0.0
	var viewport := root.get_viewport_rid()
	for i in 90:
		await process_frame
		gpu.append(RenderingServer.viewport_get_measured_render_time_gpu(viewport))
		cpu += RenderingServer.viewport_get_measured_render_time_cpu(viewport)
	var total := 0.0
	for value in gpu: total += value
	gpu.sort()
	var sample := {"phase":label,"fog_enabled":enabled,"frames":gpu.size(),"gpu_mean_ms":total/gpu.size(),"gpu_median_ms":gpu[45],"gpu_p95_ms":gpu[85],"render_cpu_ms":cpu/gpu.size()}
	samples.append(sample)
	print("FOREST_GODRAYS_GPU ",JSON.stringify(sample))

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	Engine.max_fps = 60
	DisplayServer.window_set_vsync_mode(DisplayServer.VSYNC_DISABLED)
	var authored: Node3D = load("res://assets/scenes/levels/forest_town_level.tscn").instantiate()
	var scene := Node3D.new()
	scene.name = "ForestGodraysReview"
	var rig: Node3D = authored.get_node("EnvironmentRig")
	for child_name in ["EnvironmentRig","Village"]:
		var child := authored.get_node(child_name)
		child.owner = null
		authored.remove_child(child)
		scene.add_child(child)
	var camera := authored.get_node("CameraRig/Camera3D").duplicate() as Camera3D
	scene.add_child(camera)
	camera.current = true
	var actor_position: Vector3 = authored.get_node("CameraFramingTarget").position
	authored.free()
	var player: Node3D = load("res://assets/scenes/player.tscn").instantiate()
	var actor := Node3D.new()
	actor.name = "StaticPlayerVisual"
	actor.position = actor_position
	for child_name in ["VisualRoot","AreaLight3D"]:
		var child := player.get_node_or_null(child_name)
		if child == null: continue
		child.owner = null
		player.remove_child(child)
		actor.add_child(child)
	scene.add_child(actor)
	player.free()
	assert(scene.find_children("*","FogVolume",true,false).size() == 5,"Expected five saved local forest mist volumes")
	root.add_child(scene)
	current_scene = scene
	var world: WorldEnvironment = rig.get_node("WorldEnvironment")
	var environment := world.environment
	var sun: DirectionalLight3D = rig.get_node("Sun")
	var moon: DirectionalLight3D = rig.get_node("Moon")
	var day: Resource = rig.get_meta("time_of_day")
	var weather: Resource = rig.get_meta("weather")
	var surfaces: Resource = rig.get_meta("weather_surfaces")
	assert(environment.volumetric_fog_enabled and is_zero_approx(environment.volumetric_fog_density))
	assert(is_equal_approx(environment.volumetric_fog_length,60.0))
	assert(weather.get("DriveVolumetricLightFromWeather") == true)
	day.call("SetPaused",true)
	day.call("SetTimeOfDay",12.0)
	weather.call("SetAutomatic",false)
	weather.call("SetWeather",0,true)
	surfaces.call("SetSurfaceState",0.0,0.0)
	surfaces.call("SetSnowState",0.0)
	check_energy(sun,moon,6.0)
	await frames(300)
	await capture("01-clear")
	weather.call("SetWeather",2,true)
	surfaces.call("SetSurfaceState",1.0,1.0)
	for i in 12:
		day.call("RefreshProfile")
		check_energy(sun,moon,.06)
	for hour in [9.0,15.0,18.0,12.0]:
		day.call("SetTimeOfDay",hour)
		check_energy(sun,moon,.06)
	assert(environment.volumetric_fog_enabled and is_zero_approx(environment.volumetric_fog_density))
	await frames(180)
	await capture("02-rain")
	weather.call("SetWeather",0,true)
	surfaces.call("SetSurfaceState",0.0,0.0)
	day.call("SetLunarPhase",.5)
	day.call("SetTimeOfDay",0.0)
	check_energy(sun,moon,6.0)
	assert(not sun.visible and moon.visible,"Night must use the moon without solar shafts")
	await frames(180)
	await capture("03-night")
	day.call("SetTimeOfDay",12.0)
	weather.set("DriveVolumetricLightFromWeather",false)
	weather.call("SetWeather",2,true)
	day.call("RefreshProfile")
	check_energy(sun,moon,6.0)
	weather.set("DriveVolumetricLightFromWeather",true)
	weather.call("SetWeather",0,true)
	day.call("RefreshProfile")
	check_energy(sun,moon,6.0)
	await frames(180)
	await capture("04-clear-restored")
	var gi: Resource
	for effect in world.compositor.compositor_effects:
		if effect.has_method("GetStatus") and effect.call("GetStatus").has("native_receiver"): gi = effect
	assert(gi != null and gi.call("GetStatus").get("native_receiver",false))
	gi.set("FreezeUpdates",true)
	RenderingServer.viewport_set_measure_render_time(root.get_viewport_rid(),true)
	await measure(environment,false,"off-a")
	await capture("05-fog-off")
	await measure(environment,true,"on-a")
	await measure(environment,false,"off-b")
	await measure(environment,true,"on-b")
	await capture("06-fog-on")
	var gpu_delta := (float(samples[1].gpu_mean_ms)+float(samples[3].gpu_mean_ms)-float(samples[0].gpu_mean_ms)-float(samples[2].gpu_mean_ms))*.5
	var report := {"viewport":str(root.size),"samples":samples,"paired_gpu_delta_ms":gpu_delta,"ddgi_updates_frozen":true,"note":"Paired native GPU timings; competing GPU workloads can affect results."}
	var file := FileAccess.open(OUTPUT+"validation.json",FileAccess.WRITE)
	file.store_string(JSON.stringify(report,"\t"))
	file.close()
	print("FOREST_GODRAYS_PAIRED_GPU_DELTA_MS ",gpu_delta)
	gi.set("FreezeUpdates",false)
	var start := camera.position
	for i in 30:
		camera.position = start+Vector3(1.5,0,-3.0)*float(i+1)/30.0
		await process_frame
		if i == 9: await capture("07-camera-move-10")
		if i == 19: await capture("07-camera-move-20")
	await capture("07-camera-move-end")
	print("FOREST_GODRAYS_VERIFIED: five local fog volumes, clear/rain/night, noncumulative weather, opt-out, restoration and paired GPU samples")
	scene.queue_free()
	await frames(3)
	var driver := root.get_node_or_null("StandaloneModules")
	if driver != null: driver.queue_free()
	await frames(2)
	quit()
