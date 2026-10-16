extends SceneTree
## GPU smoke check: run with the NVIDIA editor, Vulkan and a windowed display.
## Captures are written under .godot/pathtracing-validation/.

const OUTPUT := "res://.godot/pathtracing-validation/"
var rig: Node3D
var world: WorldEnvironment
var materials: Array[ShaderMaterial] = []
var started_at: int

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	started_at = int(Time.get_unix_time_from_system())
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	ProjectSettings.set_setting("rendering/pathtracing/async_shader_compilation", false)
	root.size = Vector2i(960, 540)
	root.msaa_3d = Viewport.MSAA_DISABLED
	rig = Node3D.new()
	root.add_child(rig)
	world = WorldEnvironment.new()
	world.environment = load("res://assets/resources/environment/relink_environment.tres").duplicate(true)
	world.camera_attributes = load("res://assets/resources/environment/day_night_camera_attributes.tres").duplicate(true)
	rig.add_child(world)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-60, -25, 0)
	sun.light_intensity_lux = 120000
	sun.shadow_enabled = true
	rig.add_child(sun)
	var town: Node3D = load("res://assets/scenes/town/riverside_town.tscn").instantiate()
	rig.add_child(town)
	collect_materials(town)
	var camera := Camera3D.new()
	camera.position = Vector3(12, 9, 18)
	rig.add_child(camera)
	camera.look_at(Vector3(0, 1.5, 0))
	camera.current = true
	add_varying_probe()
	world.environment.set("pathtracing_enabled", false)
	await capture("01_raster_dry")
	world.environment.set("pathtracing_enabled", true)
	world.environment.set("pathtracing_denoiser", 0)
	await capture("02_pathtracing_dry")
	set_weather(0.9, 0.7, 0.8, 0.0)
	await capture("03_pathtracing_rain")
	set_weather(0.0, 0.0, 0.0, 0.85)
	await capture("04_pathtracing_snow")
	set_weather(0.0, 0.0, 0.0, 0.0)
	world.compositor = load("res://assets/resources/environment/relink_compositor.tres").duplicate(true)
	root.msaa_3d = Viewport.MSAA_4X
	await capture("05_pathtracing_relink_msaa")
	root.msaa_3d = Viewport.MSAA_DISABLED
	root.scaling_3d_mode = Viewport.SCALING_3D_MODE_DLSS
	world.environment.set("pathtracing_denoiser", 1)
	await capture("06_pathtracing_dlss_rr")
	world.environment.set("pathtracing_enabled", false)
	root.scaling_3d_mode = Viewport.SCALING_3D_MODE_BILINEAR
	await capture("07_raster_restored")
	for effect in world.compositor.compositor_effects:
		if effect.has_method("ReleaseGpuResources"):
			effect.call("ReleaseGpuResources")
	world.compositor = null
	rig.queue_free()
	for frame in 4:
		await process_frame
	RenderingServer.call_on_render_thread(check_failed_shaders)

func check_failed_shaders() -> void:
	# A private TEMP directory keeps simultaneous editor instances' dumps apart.
	var temporary := OS.get_environment("TEMP")
	var failures := 0
	var rd := RenderingServer.get_rendering_device()
	for filename in DirAccess.get_files_at(temporary):
		if not filename.begins_with("rt_shader_") or not filename.ends_with(".glsl"):
			continue
		var path := temporary.path_join(filename)
		if FileAccess.get_modified_time(path) < started_at:
			continue
		failures += 1
		var source := RDShaderSource.new()
		source.source_closest_hit = FileAccess.get_file_as_string(path)
		var spirv := rd.shader_compile_spirv_from_source(source, false)
		push_error(filename + ": " + spirv.get_stage_compile_error(RenderingDevice.SHADER_STAGE_CLOSEST_HIT))
	finish.call_deferred(failures)

func finish(failures: int) -> void:
	print("PATHTRACING_VALIDATION_COMPLETE materials=", materials.size(), " failed_shaders=", failures)
	quit(0 if failures == 0 else 1)

func collect_materials(node: Node) -> void:
	if node is MeshInstance3D:
		for surface in node.mesh.get_surface_count():
			var material = node.get_active_material(surface)
			if material is ShaderMaterial and not materials.has(material):
				materials.append(material)
	for child in node.get_children():
		collect_materials(child)

func set_weather(wetness: float, puddles: float, rain: float, snow: float) -> void:
	for material in materials:
		material.set_shader_parameter("weather_wetness", wetness)
		material.set_shader_parameter("weather_puddles", puddles)
		material.set_shader_parameter("weather_puddles_enabled", puddles > 0)
		material.set_shader_parameter("weather_rain", rain)
		material.set_shader_parameter("weather_snow", snow)
		material.set_shader_parameter("weather_time", 12.0)

func add_varying_probe() -> void:
	# Both scalar/vector and array fragment-to-light varyings must compile in RT.
	var shader := Shader.new()
	shader.code = """shader_type spatial;
varying vec3 tint;
varying float weights[2];
void fragment() {
    tint = vec3(0.85, 0.15, 0.05);
    weights[0] = 0.25;
    weights[1] = 0.75;
    ALBEDO = tint * (weights[0] + weights[1]);
    ROUGHNESS = 0.5;
}
void light() {
    DIFFUSE_LIGHT += tint * (weights[0] + weights[1]) * LIGHT_COLOR
        * ATTENUATION * max(dot(NORMAL, LIGHT), 0.0) / PI;
}
"""
	var material := ShaderMaterial.new()
	material.shader = shader
	var snow_overlay := ShaderMaterial.new()
	snow_overlay.shader = load("res://assets/shaders/weather/snow_overlay.gdshader")
	snow_overlay.set_shader_parameter("weather_snow", 0.65)
	material.next_pass = snow_overlay
	var mesh := MeshInstance3D.new()
	mesh.mesh = BoxMesh.new()
	mesh.material_override = material
	mesh.position = Vector3(0, 1, 4)
	rig.add_child(mesh)

func capture(label: String) -> void:
	for frame in 48:
		await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	image.save_png(OUTPUT + label + ".png")
	print("PATHTRACING_CAPTURE ", label)
