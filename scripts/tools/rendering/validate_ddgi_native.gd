extends SceneTree
## Native Forward+ reception. StandardMaterial3D must work without material replacement.
const Effect := preload("res://scripts/gamelogic/rendering/ddgi/DdgiEffect.cs")
const OUTPUT := "res://.godot/ddgi-native-validation/"
var failures: Array[String] = []
var measurements := {}
var rig: Node3D
var world: WorldEnvironment
var camera: Camera3D
var effect: CompositorEffect
var receiver: MeshInstance3D
var material: StandardMaterial3D
var _additional_effects: Array[CompositorEffect] = []
var settings := {"enabled": true, "origin": Vector3(-1, -1, -1), "counts": Vector3i(3, 3, 3), "spacing": 1.0, "rays": 128, "budget": 27, "history": .9, "max_distance": 30.0, "sky": Vector3(.125, .25, .5), "energy": 1.0, "normal_bias": .02, "view_bias": 0.0}

func _initialize() -> void:
	call_deferred("run")

func _finalize() -> void:
	if is_instance_valid(effect):
		effect.ReleaseResources()
	for item in _additional_effects:
		item.ReleaseResources()

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error("DDGI_NATIVE_VALIDATION " + message)

func settle(frames := 6) -> void:
	for i in frames:
		effect.CaptureScene(rig, settings)
		await process_frame
		await RenderingServer.frame_post_draw

func capture(name: String) -> Vector3:
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	image.save_png(OUTPUT + name + ".png")
	var color := image.get_pixel(root.size.x / 2, root.size.y / 2).srgb_to_linear()
	var value := Vector3(color.r, color.g, color.b)
	measurements[name] = value
	return value

func expect(value: Vector3, reference: Vector3, message: String, tolerance := .015) -> void:
	check(value.distance_to(reference) < tolerance, message + ": got " + str(value) + " expected " + str(reference))

func read_gpu() -> Vector3:
	var serial: int = effect.GetStatus().get("debug_serial", 0)
	effect.DebugReadback(PackedVector3Array([Vector3.ZERO]), PackedVector3Array([Vector3.BACK]))
	for i in 120:
		await process_frame
		var status: Dictionary = effect.GetStatus()
		if int(status.get("debug_serial", 0)) > serial:
			var values: PackedFloat32Array = status.debug_values
			return Vector3(values[0], values[1], values[2])
	check(false, "GPU readback timed out: " + str(effect.GetStatus()))
	return Vector3.ZERO

func make_receiver(parent: Node3D, source: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	var mesh := QuadMesh.new()
	mesh.size = Vector2(2, 2)
	mesh.material = source
	node.mesh = mesh
	# A receiver-only test isolates the analytic constant-sky reference.
	node.set_meta(&"ddgi_contribute", false)
	parent.add_child(node)
	return node

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	if not ClassDB.class_has_method(&"RenderSceneBuffersRD", &"set_ddgi"):
		check(false, "Native DDGI API missing in " + OS.get_executable_path())
		finish()
		return
	root.size = Vector2i(640, 360)
	root.use_debanding = false
	root.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	rig = Node3D.new()
	root.add_child(rig)
	world = WorldEnvironment.new()
	world.environment = Environment.new()
	world.environment.background_mode = Environment.BG_COLOR
	world.environment.background_color = Color.BLACK
	world.environment.background_intensity = 1.0
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_DISABLED
	world.environment.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	world.environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	rig.add_child(world)
	camera = Camera3D.new()
	camera.position = Vector3(0, 0, 4)
	camera.current = true
	rig.add_child(camera)
	material = StandardMaterial3D.new()
	material.albedo_color = Color.WHITE
	material.metallic_specular = 0.0
	material.roughness = 1.0
	receiver = make_receiver(rig, material)
	effect = Effect.new()
	var compositor := Compositor.new()
	compositor.compositor_effects = [effect]
	world.compositor = compositor
	await settle()
	var initial_status: Dictionary = effect.GetStatus()
	check(initial_status.get("state", "") == "running" and initial_status.get("native_receiver", false), "Native DDGI must actually be selected: " + str(initial_status))
	check(receiver.get_active_material(0) == material and receiver.get_surface_override_material(0) == null, "Native reception must leave StandardMaterial3D unchanged")
	expect(await capture("01_standard"), settings.sky, "StandardMaterial3D diffuse reception")
	expect(await read_gpu(), settings.sky, "Persistent cache remains in world radiance units")

	# Native reception must work for a ShaderMaterial with no DDGI source code.
	var arbitrary := ShaderMaterial.new()
	var arbitrary_shader := Shader.new()
	arbitrary_shader.code = "shader_type spatial; void fragment() { ALBEDO = vec3(1.0); ROUGHNESS = 1.0; SPECULAR = 0.0; }"
	arbitrary.shader = arbitrary_shader
	receiver.material_override = arbitrary
	await settle()
	expect(await capture("02_shader_without_ddgi_code"), settings.sky, "Ordinary ShaderMaterial reception")
	receiver.material_override = null
	material.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	await settle()
	expect(await capture("02b_unshaded"), Vector3.ONE, "Unshaded materials bypass native DDGI")
	material.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL
	var custom := ShaderMaterial.new()
	var custom_shader := Shader.new()
	custom_shader.code = "shader_type spatial; void fragment() { ALBEDO = vec3(1.0); SPECULAR = 0.0; ROUGHNESS = 1.0; IRRADIANCE = vec4(0.2, 0.05, 0.1, 0.5); }"
	custom.shader = custom_shader
	receiver.material_override = custom
	await settle()
	expect(await capture("02c_custom_irradiance"), settings.sky.lerp(Vector3(.2, .05, .1), .5), "Explicit IRRADIANCE keeps its blend priority")
	receiver.material_override = null
	var reflection_probe := ReflectionProbe.new()
	reflection_probe.size = Vector3(6, 6, 6)
	reflection_probe.max_distance = 10
	rig.add_child(reflection_probe)
	await settle(10)
	expect(await capture("02d_reflection_probe"), settings.sky, "Reflection-probe passes use legal default DDGI resources")
	reflection_probe.queue_free()
	await process_frame

	# The receiver should multiply its base color once, and respect metallic.
	material.albedo_color = Color(.5, .5, .5)
	await settle()
	expect(await capture("03_albedo"), settings.sky * .21404114, "Native albedo energy handling", .01)
	material.albedo_color = Color.WHITE
	material.metallic = 1.0
	await settle()
	expect(await capture("04_metallic"), Vector3.ZERO, "Metal must not receive diffuse DDGI", .005)
	material.metallic = 0.0
	material.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	material.albedo_color.a = .5
	await settle()
	expect(await capture("05_transparent"), settings.sky * .5, "Native transparent reception")
	root.msaa_3d = Viewport.MSAA_4X
	await settle()
	expect(await capture("06_transparent_msaa4"), settings.sky * .5, "Native transparent MSAA reception")
	material.transparency = BaseMaterial3D.TRANSPARENCY_DISABLED
	material.albedo_color = Color.WHITE

	# Choose ISO so Practical's normalization is exactly one in physical mode.
	var attributes := CameraAttributesPractical.new()
	attributes.exposure_sensitivity = 3072007.0
	attributes.exposure_multiplier = .5
	camera.attributes = attributes
	await settle()
	expect(await capture("07_camera_half"), settings.sky * .5, "Per-view exposure normalization")
	var generation: int = effect.GetStatus().generation
	attributes.exposure_multiplier = 1.5
	await settle()
	expect(await capture("08_camera_one_and_half"), settings.sky * 1.5, "Camera exposure changes without re-baking")
	check(effect.GetStatus().generation == generation, "Camera exposure must not invalidate the world cache")
	expect(await read_gpu(), settings.sky, "Camera exposure must never enter the persistent cache")
	camera.attributes = null

	# A disabled or detached producer must stop overriding the view immediately.
	effect.enabled = false
	await settle()
	expect(await capture("09_effect_disabled"), Vector3.ZERO, "Disabled compositor publication expires", .005)
	effect.enabled = true
	await settle()
	expect(await capture("10_reenabled"), settings.sky, "Effect re-enable")
	settings.enabled = false
	await settle()
	expect(await capture("11_profile_disabled"), Vector3.ZERO, "Profile switch expires native publication", .005)
	settings.enabled = true
	await settle()
	world.compositor = null
	await settle()
	expect(await capture("12_detached"), Vector3.ZERO, "Detached compositor cannot leave stale native GI", .005)
	world.compositor = compositor
	await settle()
	expect(await capture("13_reattached"), settings.sky, "Reattached producer")

	# Outside-volume / no-producer views preserve their own ambient fallback.
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	world.environment.ambient_light_color = Color(.6, .4, .2)
	world.environment.ambient_light_energy = .3
	effect.enabled = false
	await settle()
	var ambient_reference := await capture("14a_ambient_reference")
	check(ambient_reference.length() > .02, "Ambient fallback reference must be nonzero")
	effect.enabled = true
	await settle()
	expect(await capture("14b_ambient_overridden"), settings.sky, "DDGI replaces diffuse ambient inside its volume")
	settings.origin = Vector3(10, 10, 10)
	await settle()
	expect(await capture("14_outside_volume"), ambient_reference, "Outside-volume fallback", .005)
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_DISABLED
	settings.origin = Vector3(-1, -1, -1)
	root.size = Vector2i(800, 450)
	await settle()
	expect(await capture("15_viewport_resized"), settings.sky, "Viewport resize keeps the world cache usable")
	measurements.status = effect.GetStatus()

	await check_shared_world_views()
	await check_independent_worlds()
	world.compositor = null
	effect.ReleaseResources()
	rig.queue_free()
	await process_frame
	await process_frame
	check(effect.GetStatus().state == "released", "Native producer teardown")
	finish()

func check_shared_world_views() -> void:
	# Two cameras share one world/cache, but have different exposure and buffers.
	var viewport := SubViewport.new()
	viewport.size = Vector2i(128, 128)
	viewport.world_3d = rig.get_world_3d()
	viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
	root.add_child(viewport)
	var other_camera := Camera3D.new()
	other_camera.position = camera.position
	other_camera.current = true
	var attributes := CameraAttributesPractical.new()
	attributes.exposure_sensitivity = 3072007.0
	attributes.exposure_multiplier = .25
	other_camera.attributes = attributes
	viewport.add_child(other_camera)
	var updates: int = effect.GetStatus().updates
	await settle(8)
	var update_count: int = effect.GetStatus().updates - updates
	var image := viewport.get_texture().get_image()
	image.save_png(OUTPUT + "16_shared_world_camera.png")
	var pixel := image.get_pixel(64, 64).srgb_to_linear()
	expect(Vector3(pixel.r, pixel.g, pixel.b), settings.sky * .25, "Shared world camera uses its own exposure")
	check(update_count >= 8 and update_count <= 9, "Multiple cameras must update a shared cache only once per frame: " + str(update_count))
	measurements.shared_world_updates = update_count
	viewport.queue_free()
	await process_frame

func check_independent_worlds() -> void:
	var viewports: Array[SubViewport] = []
	var roots: Array[Node3D] = []
	var worlds: Array[WorldEnvironment] = []
	var configs: Array[Dictionary] = []
	var effects: Array[CompositorEffect] = []
	var receivers: Array[MeshInstance3D] = []
	var shared_environment := Environment.new()
	shared_environment.background_mode = Environment.BG_COLOR
	shared_environment.background_color = Color.BLACK
	shared_environment.background_intensity = 1.0
	shared_environment.ambient_light_source = Environment.AMBIENT_SOURCE_DISABLED
	shared_environment.reflected_light_source = Environment.REFLECTION_SOURCE_DISABLED
	var shared_material := StandardMaterial3D.new()
	shared_material.metallic_specular = 0.0
	for i in 2:
		var viewport := SubViewport.new()
		viewport.size = Vector2i(128, 128)
		viewport.own_world_3d = true
		viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
		root.add_child(viewport)
		viewports.append(viewport)
		var local_root := Node3D.new()
		viewport.add_child(local_root)
		roots.append(local_root)
		var local_world := WorldEnvironment.new()
		local_world.environment = shared_environment
		var local_effect := Effect.new()
		effects.append(local_effect)
		_additional_effects.append(local_effect)
		var compositor := Compositor.new()
		compositor.compositor_effects = [local_effect]
		local_world.compositor = compositor
		local_root.add_child(local_world)
		worlds.append(local_world)
		var local_camera := Camera3D.new()
		local_camera.position.z = 4
		local_camera.current = true
		local_root.add_child(local_camera)
		receivers.append(make_receiver(local_root, shared_material))
		var config := settings.duplicate()
		config.sky = Vector3(.5, 0, 0) if i == 0 else Vector3(0, 0, .5)
		configs.append(config)
	for frame in 8:
		for i in 2: effects[i].CaptureScene(roots[i], configs[i])
		await process_frame
		await RenderingServer.frame_post_draw
	for i in 2:
		var image := viewports[i].get_texture().get_image()
		image.save_png(OUTPUT + "17_world_%d.png" % i)
		var pixel := image.get_pixel(64, 64).srgb_to_linear()
		expect(Vector3(pixel.r, pixel.g, pixel.b), configs[i].sky, "Native world light must be isolated")
		check(receivers[i].get_active_material(0) == shared_material and receivers[i].get_surface_override_material(0) == null, "Independent worlds must share an unchanged StandardMaterial3D")
	worlds[0].compositor = null
	effects[0].ReleaseResources()
	for frame in 4:
		effects[1].CaptureScene(roots[1], configs[1])
		await process_frame
		await RenderingServer.frame_post_draw
	var disabled_pixel := viewports[0].get_texture().get_image().get_pixel(64, 64).srgb_to_linear()
	var surviving_pixel := viewports[1].get_texture().get_image().get_pixel(64, 64).srgb_to_linear()
	expect(Vector3(disabled_pixel.r, disabled_pixel.g, disabled_pixel.b), Vector3.ZERO, "Released world stops receiving native DDGI", .005)
	expect(Vector3(surviving_pixel.r, surviving_pixel.g, surviving_pixel.b), configs[1].sky, "Releasing one world's cache must preserve the other")
	measurements.world_isolation = true
	for i in 2:
		worlds[i].compositor = null
		effects[i].ReleaseResources()
		viewports[i].queue_free()
	await process_frame

func finish() -> void:
	var report := {"failures": failures, "measurements": measurements, "engine_executable": OS.get_executable_path()}
	var file := FileAccess.open(OUTPUT + "results.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t"))
	file.close()
	print("DDGI_NATIVE_VALIDATION_COMPLETE failures=", failures.size(), " measurements=", measurements)
	quit(0 if failures.is_empty() else 1)
