extends SceneTree
## Forward+ / Vulkan GPU regression. Run without --headless; output stays in .godot/.

const OUTPUT := "res://.godot/relink-validation/"
const OPAQUE := Color(0.18, 0.36, 0.54)
const TRANSPARENT := Color(0.4, 0.1, 0.2, 0.5)
var world: WorldEnvironment
var rig: Node3D
var profile: Resource
var effects: Array[CompositorEffect] = []
var failures: Array[String] = []
var measurements := {}

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	if RenderingServer.get_current_rendering_method() != "forward_plus" or RenderingServer.get_rendering_device() == null:
		push_error("RELINK_VALIDATION requires Forward+ and a real RenderingDevice")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	root.size = Vector2i(640, 360)
	root.msaa_3d = Viewport.MSAA_DISABLED
	root.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	root.use_debanding = false
	check_scene_configuration()
	rig = Node3D.new()
	root.add_child(rig)
	world = WorldEnvironment.new()
	world.environment = Environment.new()
	world.environment.background_mode = Environment.BG_COLOR
	world.environment.background_color = Color.BLACK
	world.environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	rig.add_child(world)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 6.0
	camera.current = true
	rig.add_child(camera)
	add_quad(Vector3(0, 0, -5), Vector2(12, 6), OPAQUE, false)
	add_quad(Vector3(3, 0, -4), Vector2(6, 6), TRANSPARENT, true)
	var canvas := CanvasLayer.new()
	rig.add_child(canvas)
	var ui := ColorRect.new()
	ui.position = Vector2(8, 8)
	ui.size = Vector2(48, 32)
	ui.color = Color(0.2, 0.6, 0.8)
	canvas.add_child(ui)
	var compositor: Compositor = load("res://assets/resources/environment/relink_compositor.tres").duplicate(true)
	# Keep the captured LUTs shared, but isolate mutable settings from the scene/editor.
	profile = compositor.compositor_effects[0].get("Profile").duplicate()
	for effect in compositor.compositor_effects:
		effect.set("Profile", profile)
		effects.append(effect)
	world.compositor = compositor
	profile.set("Active", false)
	var baseline := await capture("01_native_linear")
	check_color("native_linear", sample(baseline, 0.25, 0.5), OPAQUE.linear_to_srgb())
	var blend := Color(OPAQUE.r, OPAQUE.g, OPAQUE.b).lerp(Color(TRANSPARENT.r, TRANSPARENT.g, TRANSPARENT.b), TRANSPARENT.a)
	check_color("native_transparency", sample(baseline, 0.75, 0.5), blend.linear_to_srgb())
	profile.set("Active", true)
	profile.set("LutStrength", 0.0)
	profile.set("BloomEnabled", false)
	profile.set("Exposure", 3.0)
	var film := await capture("02_film_only")
	check_color("film_once", sample(film, 0.25, 0.5), film_reference(OPAQUE, 3.0))
	check_color("transparent_before_film", sample(film, 0.75, 0.5), film_reference(blend, 3.0))
	check_color("ui_after_film", sample(film, 0.05, 0.06), sample(baseline, 0.05, 0.06))
	profile.set("Exposure", 1.0)
	var exposure := await capture("03_exposure_one")
	check_color("exposure_once", sample(exposure, 0.25, 0.5), film_reference(OPAQUE, 1.0))
	profile.set("Exposure", 3.0)
	profile.set("LutStrength", 1.0)
	var graded := await capture("04_lut_no_msaa")
	check(distance(sample(graded, 0.25, 0.5), sample(film, 0.25, 0.5)) > 0.01, "LUT must affect opaque color")
	root.msaa_3d = Viewport.MSAA_4X
	var msaa := await capture("05_lut_msaa4")
	check_color("msaa_lut_writeback", sample(msaa, 0.25, 0.5), sample(graded, 0.25, 0.5))
	check_color("msaa_transparency", sample(msaa, 0.75, 0.5), sample(graded, 0.75, 0.5))
	check("MSAA" in str(effects[0].call("GetRenderStatus")), "LUT must write back to MSAA")
	profile.set("BloomEnabled", true)
	profile.set("BloomThreshold", 0.3)
	profile.set("BloomIntensity", 0.5)
	var bloom := await capture("06_bloom")
	check(distance(sample(bloom, 0.25, 0.5), sample(msaa, 0.25, 0.5)) > 0.01, "Relink Bloom must affect color")
	check_color("ui_after_bloom", sample(bloom, 0.05, 0.06), sample(baseline, 0.05, 0.06))
	root.size = Vector2i(800, 450)
	var resized := await capture("07_resized")
	check(resized.get_size() == Vector2i(800, 450), "resize must rebuild render buffers")
	profile.set("Active", false)
	root.msaa_3d = Viewport.MSAA_DISABLED
	var bypass := await capture("08_bypass")
	check_color("bypass_restores_linear", sample(bypass, 0.25, 0.5), sample(baseline, 0.25, 0.5))
	# Explicit release must allow effects to initialize again on the same viewport.
	for effect in effects:
		effect.call("ReleaseGpuResources")
	for frame in 4:
		await process_frame
	profile.set("Active", true)
	profile.set("LutStrength", 0.0)
	profile.set("BloomEnabled", false)
	var recreated := await capture("09_recreated")
	check_color("recreated_film", sample(recreated, 0.25, 0.5), film_reference(OPAQUE, 3.0))
	for effect in effects:
		check(not effect.get("HasShaderError"), "Relink shaders must compile")
		effect.call("ReleaseGpuResources")
	world.compositor = null
	effects.clear()
	rig.queue_free()
	for frame in 6:
		await process_frame
	var report := {"renderer": RenderingServer.get_current_rendering_method(), "failures": failures, "measurements": measurements}
	var file := FileAccess.open(OUTPUT + "results.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t"))
	file.close()
	print("RELINK_VALIDATION_COMPLETE failures=", failures.size(), " output=", OUTPUT)
	quit(0 if failures.is_empty() else 1)

func check_scene_configuration() -> void:
	var scene: PackedScene = load("res://assets/scenes/spacelevel.tscn")
	var state := scene.get_state()
	var environment: Environment
	var compositor: Compositor
	for node in state.get_node_count():
		if not str(state.get_node_path(node)).ends_with("EnvironmentRig/WorldEnvironment"):
			continue
		for property in state.get_node_property_count(node):
			if state.get_node_property_name(node, property) == &"environment":
				environment = state.get_node_property_value(node, property)
			if state.get_node_property_name(node, property) == &"compositor":
				compositor = state.get_node_property_value(node, property)
	check(environment != null and compositor != null, "level must wire Relink environment and compositor")
	if environment == null or compositor == null:
		return
	check(environment.tonemap_mode == Environment.TONE_MAPPER_LINEAR and is_equal_approx(environment.tonemap_exposure, 1.0), "level must use Linear / Exposure 1")
	check(not environment.glow_enabled and not environment.adjustment_enabled, "level must avoid native Glow / Adjustment stacking")
	check(compositor.compositor_effects.size() == 2, "level must have exactly two Relink stages")
	var previous: Resource
	for effect in compositor.compositor_effects:
		check(effect.enabled and effect.access_resolved_color, "each stage must read resolved color")
		var is_grade: bool = effect.get("Pass") == 0
		check(effect.access_resolved_depth == is_grade, "only LUT stage needs resolved depth")
		check(effect.effect_callback_type == (CompositorEffect.EFFECT_CALLBACK_TYPE_PRE_TRANSPARENT if is_grade else CompositorEffect.EFFECT_CALLBACK_TYPE_POST_TRANSPARENT), "stage ordering must preserve linear transparency")
		var shared: Resource = effect.get("Profile")
		check(previous == null or previous == shared, "stages must share one Profile")
		previous = shared

func add_quad(position: Vector3, size: Vector2, color: Color, transparent: bool) -> void:
	var shader := Shader.new()
	shader.code = "shader_type spatial; render_mode unshaded, fog_disabled; uniform vec4 linear_color; void fragment() { ALBEDO = linear_color.rgb; %s }" % ("ALPHA = linear_color.a;" if transparent else "")
	var material := ShaderMaterial.new()
	material.shader = shader
	# Color variants are decoded from sRGB by the material API; Vector4 supplies raw linear values.
	material.set_shader_parameter("linear_color", Vector4(color.r, color.g, color.b, color.a))
	var mesh := QuadMesh.new()
	mesh.size = size
	var instance := MeshInstance3D.new()
	instance.mesh = mesh
	instance.material_override = material
	instance.position = position
	rig.add_child(instance)

func capture(name: String) -> Image:
	for frame in 10:
		await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	check(image.save_png(OUTPUT + name + ".png") == OK, "capture " + name)
	return image

func sample(image: Image, x: float, y: float) -> Color:
	return image.get_pixel(int(image.get_width() * x), int(image.get_height() * y))

func film_reference(color: Color, exposure: float) -> Color:
	var result := Color.BLACK
	for channel in 3:
		var x: float = color[channel] * exposure
		result[channel] = clampf(((x * (0.15 * x + 0.05) + 0.004) / (x * (0.15 * x + 0.5) + 0.06) - 0.066667) * 1.379064, 0, 1)
	return result.linear_to_srgb()

func distance(a: Color, b: Color) -> float:
	return maxf(absf(a.r - b.r), maxf(absf(a.g - b.g), absf(a.b - b.b)))

func check_color(name: String, actual: Color, expected: Color) -> void:
	var error := distance(actual, expected)
	measurements[name] = {"actual": [actual.r, actual.g, actual.b], "expected": [expected.r, expected.g, expected.b], "max_error": error}
	check(error < 0.012, name + " max_error=" + str(error))

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error("RELINK_VALIDATION " + message)
