extends SceneTree
## Real Vulkan GPU validation; requires the regular 4.8 Ray Query build, not --headless.

const Effect := preload("res://scripts/gamelogic/rendering/ddgi/DdgiEffect.cs")
const OUTPUT := "res://.godot/ddgi-validation/"
var failures: Array[String] = []
var measurements := {}
var rig: Node3D
var world: WorldEnvironment
var effect: CompositorEffect
var camera: Camera3D
var _tlas_result := PackedInt32Array()
var _tlas_mutex := Mutex.new()
var settings := {"enabled": true, "origin": Vector3(-1, -1, -1), "counts": Vector3i(3, 3, 3), "spacing": 1.0, "rays": 128, "budget": 27, "history": 0.9, "max_distance": 30.0, "sky": Vector3(0.125, 0.25, 0.5), "energy": 1.0, "normal_bias": 0.02, "view_bias": 0.0}

func _initialize() -> void:
	call_deferred("run")

func check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
		push_error("DDGI_VALIDATION " + message)

func settle(frames := 6) -> void:
	for i in frames:
		effect.CaptureScene(rig, settings)
		await process_frame
		await RenderingServer.frame_post_draw

func sample_at(position: Vector3, normal: Vector3) -> Vector3:
	var serial: int = effect.GetStatus().get("debug_serial", 0)
	effect.DebugReadback(PackedVector3Array([position]), PackedVector3Array([normal]))
	for i in 120:
		await process_frame
		var status: Dictionary = effect.GetStatus()
		if status.get("state", "") in ["failed", "unsupported"]:
			check(false, "GPU failure: " + str(status))
			return Vector3.ZERO
		if int(status.get("debug_serial", 0)) > serial:
			var values: PackedFloat32Array = status.debug_values
			return Vector3(values[0], values[1], values[2])
	check(false, "Timed out waiting for actual GPU readback: " + str(effect.GetStatus()))
	return Vector3.ZERO

func box(name: String, position: Vector3, size: Vector3, material: Material) -> MeshInstance3D:
	var node := MeshInstance3D.new()
	node.name = name
	var mesh := BoxMesh.new()
	mesh.size = size
	mesh.material = material
	node.mesh = mesh
	rig.add_child(node)
	node.position = position
	return node

func run() -> void:
	if RenderingServer.get_rendering_device() == null or RenderingServer.get_current_rendering_method() != "forward_plus":
		push_error("DDGI_VALIDATION requires a real Forward+ RenderingDevice")
		quit(1)
		return
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	root.size = Vector2i(640, 360)
	root.use_debanding = false
	root.screen_space_aa = Viewport.SCREEN_SPACE_AA_DISABLED
	RenderingServer.call_on_render_thread(check_tlas_rebuild_render)
	for i in 120:
		await process_frame
		_tlas_mutex.lock()
		var tlas_ready := not _tlas_result.is_empty()
		_tlas_mutex.unlock()
		if tlas_ready: break
	check(_tlas_result == PackedInt32Array([1, 0]), "Two TLAS builds in one frame must keep distinct instance uploads: " + str(_tlas_result))
	measurements.tlas_rebuild_same_frame = _tlas_result
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
	camera.position = Vector3(0, 0, 6)
	camera.current = true
	rig.add_child(camera)
	effect = Effect.new()
	var compositor := Compositor.new()
	compositor.compositor_effects = [effect]
	world.compositor = compositor
	await settle()
	var sky := await sample_at(Vector3.ZERO, Vector3.UP)
	measurements.sky = sky
	check(sky.distance_to(settings.sky) < 0.003, "Empty-scene irradiance / PI must equal constant sky: " + str(sky))
	check(effect.GetStatus().get("debug_hits", -1) == 0, "Empty TLAS must produce real ray misses")
	check(effect.GetStatus().get("debug_valid_probes", 0) == 27, "All empty-volume probes must classify valid")
	if effect.GetStatus().get("state", "") != "running":
		finish()
		return

	settings.sky = Vector3.ZERO
	var emitter := StandardMaterial3D.new()
	emitter.albedo_color = Color.BLACK
	emitter.emission_enabled = true
	emitter.emission = Color.RED
	emitter.emission_energy_multiplier = 2.0
	emitter.emission_intensity = 1.0
	var wall := box("OffscreenEmitter", Vector3(-2, 0, 0), Vector3(.2, 4, 6), emitter)
	await settle()
	var red := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.emission_red = red
	check(red.x > 0.15 and red.x > red.y * 10.0, "Ray hits must gather red emission: " + str(red))
	var reference := rectangle_emission(2.0, 3.0, 1.9, 2.0)
	measurements.emission_rectangle_reference = reference
	check(absf(red.x - reference) < reference * .015, "Area emitter must match an independent rectangle form-factor reference")
	check(effect.GetStatus().get("debug_hits", 0) > 0, "Scene must produce actual triangle hits")
	var blas_before: int = effect.GetStatus().get("blas", 0)
	wall.scale.x = -1.0
	await settle()
	var mirrored := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.mirrored_instance = mirrored
	check(mirrored.x > red.x * .8, "Negative scale must preserve transformed surface normals and emission")

	# The camera now points away; RT geometry collection must remain unchanged.
	camera.rotation.y = PI
	await settle()
	var offscreen := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.offscreen = offscreen
	check(offscreen.x > red.x * 0.8, "Offscreen geometry must continue contributing GI")

	var opaque := StandardMaterial3D.new()
	opaque.albedo_color = Color.BLACK
	var blocker := box("MovingBlocker", Vector3(0.8, 0, 0), Vector3(.12, 4, 6), opaque)
	await settle()
	blocker.position.x = -.7
	await settle(10)
	var blocked := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.blocked = blocked
	check(blocked.x < red.x * 0.5, "Moving rigid occluder must reduce incident emission: " + str(blocked))
	check(effect.GetStatus().get("blas", 0) == blas_before + 1, "Rigid motion must reuse the surface BLAS")

	var alpha_image := Image.create(2, 2, false, Image.FORMAT_RGBA8)
	alpha_image.fill(Color(1, 1, 1, 0))
	opaque.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA_SCISSOR
	opaque.albedo_texture = ImageTexture.create_from_image(alpha_image)
	await settle(10)
	var cutout := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.alpha_cutout = cutout
	check(cutout.x > red.x * 0.7, "Alpha-discarded triangles must not occlude rays: " + str(cutout))

	emitter.emission = Color.GREEN
	await settle(8)
	var green := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.emission_green = green
	check(green.y > 0.15 and green.y > green.x * 10.0, "Ordinary material uniform changes must invalidate probe history")
	var emitter_mesh := wall.mesh as BoxMesh
	var old_size := emitter_mesh.size
	emitter_mesh.size = Vector3(.1, .15, .15)
	await settle(12)
	var small_reference := rectangle_emission(.075, .075, 1.95, 2.0)
	var small_min := INF
	var small_max := 0.0
	for i in 16:
		await settle(1)
		var value := await sample_at(Vector3.ZERO, Vector3.LEFT)
		small_min = minf(small_min, value.y)
		small_max = maxf(small_max, value.y)
	measurements.small_emitter = {"min": small_min, "max": small_max, "reference": small_reference}
	check(absf(small_min - small_reference) < small_reference * .02 and absf(small_max - small_reference) < small_reference * .02,
		"A small emitter must remain nonzero, stable and match its analytic energy")
	emitter_mesh.size = old_size
	emitter.emission_energy_multiplier = 0.0
	await settle(8)
	var dark := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.dark = dark
	check(dark.length() < 0.002, "Removing illumination must leave no stale light")
	# Verify a diffuse bounce from directional light rather than emission alone.
	emitter.albedo_color = Color.RED
	var sunlight := DirectionalLight3D.new()
	sunlight.light_energy = 4.0
	sunlight.light_intensity_lux = 1.0
	sunlight.light_indirect_energy = 1.0
	sunlight.basis = Basis.looking_at(-Vector3(1, 1, 0).normalized(), Vector3.FORWARD)
	rig.add_child(sunlight)
	await settle(8)
	var diffuse := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.directional_bounce = diffuse
	check(diffuse.x > 0.05 and diffuse.x > diffuse.y * 10.0, "Directional-light hits must produce a red diffuse bounce: " + str(diffuse))
	sunlight.light_indirect_energy = 0.0
	await settle(8)
	var indirect_off := await sample_at(Vector3.ZERO, Vector3.LEFT)
	measurements.directional_indirect_off = indirect_off
	check(indirect_off.length() < 0.002, "DirectionalLight3D indirect energy zero must disable its bounce")
	sunlight.queue_free()

	blocker.queue_free()
	wall.queue_free()
	await process_frame
	settings.sky = Vector3(0.125, 0.25, 0.5)
	settings.budget = 5
	await settle(12)
	var budgeted := await sample_at(Vector3.ZERO, Vector3.UP)
	measurements.budgeted = budgeted
	check(budgeted.distance_to(settings.sky) < 0.003, "Partial update budget must eventually refresh all probes")
	check(effect.GetStatus().get("rays_per_update", 0) == 5 * 128, "Ray work must follow the configured probe budget")
	check(effect.GetStatus().get("blas", -1) == 0, "Removed surfaces must release their BLAS caches")
	var embedded := box("ProbeRelocation", Vector3.ZERO, Vector3(.4, .4, .4), StandardMaterial3D.new())
	await settle(18)
	await sample_at(Vector3.ZERO, Vector3.UP)
	measurements.relocation = effect.GetStatus().get("debug_relocated_probes", 0)
	check(measurements.relocation > 0, "A probe inside a solid must relocate or classify invalid")
	check(effect.GetStatus().get("debug_valid_probes", 0) >= 26, "Relocation must preserve the surrounding free probes")
	embedded.queue_free()
	await process_frame
	settings.counts = Vector3i(4, 3, 3)
	await settle(14)
	var resized := await sample_at(Vector3.ZERO, Vector3.UP)
	measurements.resized_volume = resized
	check(resized.distance_to(settings.sky) < .003, "Probe topology changes must replace and initialize atlas resources")

	world.compositor = null
	effect.ReleaseResources()
	await process_frame
	check(effect.GetStatus().state == "released", "Explicit GPU resource release must complete")
	rig.queue_free()
	await process_frame

	finish()

func rectangle_emission(half_width: float, half_height: float, distance: float, radiance: float) -> float:
	var x := half_width / distance
	var y := half_height / distance
	return radiance * 2.0 / PI * (x / sqrt(1.0 + x * x) * atan(y / sqrt(1.0 + x * x))
		+ y / sqrt(1.0 + y * y) * atan(x / sqrt(1.0 + y * y)))

func check_tlas_rebuild_render() -> void:
	var rd := RenderingServer.get_rendering_device()
	if not rd.has_feature(RenderingDevice.SUPPORTS_RAY_QUERY):
		_tlas_mutex.lock()
		_tlas_result = PackedInt32Array([-1])
		_tlas_mutex.unlock()
		return
	var data := PackedFloat32Array([-1, -1, -2, 1, -1, -2, 0, 1, -2]).to_byte_array()
	var vertices := rd.vertex_buffer_create(data.size(), data, RenderingDevice.BUFFER_CREATION_ACCELERATION_STRUCTURE_BUILD_INPUT_READ_ONLY_BIT)
	var geometry := RDAccelerationStructureGeometry.new()
	geometry.vertex_buffer = vertices
	geometry.vertex_count = 3
	geometry.vertex_stride = 12
	geometry.vertex_format = RenderingDevice.DATA_FORMAT_R32G32B32_SFLOAT
	geometry.flags = RenderingDevice.ACCELERATION_STRUCTURE_GEOMETRY_OPAQUE_BIT
	var blas := rd.blas_create([geometry], 0)
	rd.blas_build(blas)
	var tlas := rd.tlas_create(1, 0)
	var instance := RDAccelerationStructureInstance.new()
	instance.blas = blas
	var source := RDShaderSource.new()
	source.source_compute = """#version 460
#extension GL_EXT_ray_query : require
layout(local_size_x = 1) in;
layout(set=0, binding=0) uniform accelerationStructureEXT scene;
layout(set=0, binding=1, std430) writeonly buffer Result { uint hit; } result;
void main() {
    rayQueryEXT query;
    rayQueryInitializeEXT(query, scene, gl_RayFlagsOpaqueEXT, 255, vec3(0.0), 0.01, vec3(0, 0, -1), 10.0);
    while (rayQueryProceedEXT(query)) {}
    result.hit = rayQueryGetIntersectionTypeEXT(query, true) == gl_RayQueryCommittedIntersectionTriangleEXT ? 1u : 0u;
}
"""
	var shader := rd.shader_create_from_spirv(rd.shader_compile_spirv_from_source(source))
	var pipeline := rd.compute_pipeline_create(shader)
	var results: Array[RID] = []
	for iteration in 2:
		instance.transform = Transform3D(Basis.IDENTITY, Vector3(iteration * 100, 0, 0))
		rd.tlas_build(tlas, [instance])
		var result := rd.storage_buffer_create(4, PackedByteArray([0, 0, 0, 0]))
		results.append(result)
		var acceleration_uniform := RDUniform.new()
		acceleration_uniform.binding = 0
		acceleration_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_ACCELERATION_STRUCTURE
		acceleration_uniform.add_id(tlas)
		var result_uniform := RDUniform.new()
		result_uniform.binding = 1
		result_uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER
		result_uniform.add_id(result)
		var set := UniformSetCacheRD.get_cache(shader, 0, [acceleration_uniform, result_uniform])
		var list := rd.compute_list_begin()
		rd.compute_list_bind_compute_pipeline(list, pipeline)
		rd.compute_list_bind_uniform_set(list, set, 0)
		rd.compute_list_dispatch(list, 1, 1, 1)
		rd.compute_list_end()
	var values := PackedInt32Array([rd.buffer_get_data(results[0]).decode_u32(0), rd.buffer_get_data(results[1]).decode_u32(0)])
	rd.free_rid(tlas)
	rd.free_rid(blas)
	rd.free_rid(vertices)
	rd.free_rid(pipeline)
	rd.free_rid(shader)
	for result in results: rd.free_rid(result)
	_tlas_mutex.lock()
	_tlas_result = values
	_tlas_mutex.unlock()

func finish() -> void:
	# Let the test coroutine release its scene/effect references before shutdown.
	for i in 3: await process_frame
	var report := {"failures": failures, "measurements": measurements, "engine": Engine.get_version_info(), "renderer": RenderingServer.get_current_rendering_method()}
	var file := FileAccess.open(OUTPUT + "results.json", FileAccess.WRITE)
	file.store_string(JSON.stringify(report, "\t"))
	file.close()
	print("DDGI_VALIDATION_COMPLETE failures=", failures.size(), " measurements=", measurements)
	quit(0 if failures.is_empty() else 1)
