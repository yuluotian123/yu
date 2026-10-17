extends SceneTree
## GPU regression: tangent-projection singularities must not draw distant white dots.

const SKY_SHADER := "res://assets/shaders/environment/day_night_sky.gdshader"
const OUTPUT := "res://.godot/sky-disc-validation/"
const LIGHT_DIRECTION := Vector3(-0.109381, 0.965926, 0.23457)
var views: Array[SubViewport] = []
var failures: Array[String] = []

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if RenderingServer.get_rendering_device() == null:
		push_error("SKY_DISCS requires a GPU renderer")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	for body in ["sun", "moon"]:
		for framing in ["away", "center", "ortho"]:
			var viewport := SubViewport.new()
			viewport.name = body + "_" + framing
			viewport.size = Vector2i(1410, 959) if framing == "away" else Vector2i(512, 512)
			viewport.own_world_3d = true
			viewport.msaa_3d = Viewport.MSAA_4X
			viewport.render_target_update_mode = SubViewport.UPDATE_ALWAYS
			root.add_child(viewport)
			views.append(viewport)
			var material := ShaderMaterial.new()
			material.shader = load(SKY_SHADER)
			# Black atmosphere isolates light discs and their bounded halos.
			for color in ["day_zenith", "day_horizon", "day_ground", "sunset_horizon", "night_zenith", "night_horizon"]:
				material.set_shader_parameter(color, Vector3.ZERO)
			material.set_shader_parameter("horizon_haze", 0.0)
			material.set_shader_parameter("mie_strength", 0.0)
			material.set_shader_parameter("daylight", 1.0 if body == "sun" else 0.0)
			material.set_shader_parameter("sun_direction", LIGHT_DIRECTION if body == "sun" else -LIGHT_DIRECTION)
			material.set_shader_parameter("moon_direction", LIGHT_DIRECTION)
			material.set_shader_parameter("moon_visibility", 1.0 if body == "moon" else 0.0)
			var sky := Sky.new()
			sky.sky_material = material
			var environment := Environment.new()
			environment.background_mode = Environment.BG_SKY
			environment.background_intensity = 1.0
			environment.sky = sky
			var world := WorldEnvironment.new()
			world.environment = environment
			viewport.add_child(world)
			var camera := Camera3D.new()
			viewport.add_child(camera)
			camera.current = true
			camera.fov = 70.01
			if framing == "away":
				camera.basis = Basis(Vector3(0.45630291, 0.0, 0.88982451), Vector3(0.02946922, 0.99945146, -0.01511185), Vector3(-0.88933641, 0.03311802, 0.45605260))
			else:
				camera.look_at(LIGHT_DIRECTION, Vector3.UP)
				if framing == "ortho":
					camera.projection = Camera3D.PROJECTION_ORTHOGONAL
					environment.sky_custom_fov = 65.0
	for frame in 12:
		await process_frame
	await RenderingServer.frame_post_draw
	for viewport in views:
		var image := viewport.get_texture().get_image()
		image.save_png(OUTPUT + str(viewport.name) + ".png")
		var bright := 0
		var minimum := Vector2i(image.get_width(), image.get_height())
		var maximum := Vector2i.ZERO
		for y in image.get_height():
			for x in image.get_width():
				if image.get_pixel(x, y).r > 0.95:
					bright += 1
					minimum = minimum.min(Vector2i(x, y))
					maximum = maximum.max(Vector2i(x, y))
		var size := maximum - minimum + Vector2i.ONE if bright > 0 else Vector2i.ZERO
		var passed := bright == 0 if str(viewport.name).ends_with("away") else bright > 4 and bright < 2000 and absi(size.x - size.y) <= 2
		if not passed:
			failures.append(str(viewport.name))
		print("SKY_DISCS ", viewport.name, " bright_pixels=", bright, " bounds=", size, " passed=", passed)
	for viewport in views:
		viewport.queue_free()
	for frame in 3:
		await process_frame
	print("SKY_DISCS_COMPLETE failures=", failures.size())
	quit(0 if failures.is_empty() else 1)
