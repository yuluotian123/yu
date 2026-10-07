@tool
extends EditorPlugin
## 编辑器预览适配；游戏行为仍由 EC 组件驱动。
var _elapsed := 0.0
const SKY_PROPERTIES := {"DayZenith": "day_zenith", "DayHorizon": "day_horizon", "DayGround": "day_ground"}

func _process(delta: float) -> void:
	_elapsed += delta
	if _elapsed < 0.15:
		return
	_elapsed = 0.0
	var scene := get_editor_interface().get_edited_scene_root()
	if scene == null:
		return
	var cameras := scene.find_children("*", "Camera3D", true, false)
	if cameras.is_empty():
		return
	var camera: Camera3D = cameras[0]
	for candidate: Camera3D in cameras:
		if candidate.current:
			camera = candidate
			break
	for world: WorldEnvironment in scene.find_children("*", "WorldEnvironment", true, false):
		var env := world.environment
		if env == null or env.sky == null or not env.sky.sky_material is ShaderMaterial:
			continue
		var material := env.sky.sky_material as ShaderMaterial
		if material.shader == null or material.shader.resource_path != "res://assets/environment/day_night_sky.gdshader":
			continue
		var components = world.get_parent().get("Components")
		if components == null:
			continue
		for component: Resource in components:
			if component == null or component.get_script().resource_path != "res://scripts/gamelogic/timeofday/DayNightEnvironmentComponent3D.cs":
				continue
			var profile: Resource = component.get("Profile")
			if profile == null:
				continue
			profile.call("ApplyAtmosphere", env, material, profile.get("StartHour"))
			var ortho := camera.projection == Camera3D.PROJECTION_ORTHOGONAL
			var sky_fov: float = clampf(profile.get("OrthographicSkyFov"), 30.0, 100.0) if ortho else 0.0
			var sky_rotation := Vector3(deg_to_rad(profile.get("OrthographicSkyPitchDegrees")), 0, 0) if ortho else Vector3.ZERO
			if not is_equal_approx(env.sky_custom_fov, sky_fov):
				env.sky_custom_fov = sky_fov
			if not env.sky_rotation.is_equal_approx(sky_rotation):
				env.sky_rotation = sky_rotation
			set_if_changed(material, "sun_radius", deg_to_rad(profile.get("SunDiameterDegrees") * 0.5))
			set_if_changed(material, "moon_radius", deg_to_rad(profile.get("MoonDiameterDegrees") * 0.5))
			for key: String in SKY_PROPERTIES:
				set_if_changed(material, SKY_PROPERTIES[key], profile.get(key))

			var host: Node3D = world.get_parent()
			var sun := host.get_node_or_null(component.get("SunPath")) as DirectionalLight3D
			var moon := host.get_node_or_null(component.get("MoonPath")) as DirectionalLight3D
			if sun != null and moon != null and world.camera_attributes is CameraAttributesPhysical:
				profile.call("ApplyLighting", env, sun, moon, world.camera_attributes, profile.get("StartHour"), profile.get("StartLunarPhase"))
			# Editing the calendar previews exactly that instant; the editor has no independent cloud clock.
			var hours: float = profile.get("StartDay") * 24.0 + profile.get("StartHour")
			var cloud_time: Vector4 = profile.call("SampleCloudTime", hours)
			var rotation_phase: float = cloud_time.x
			var phase: float = cloud_time.y
			var day_phase: float = cloud_time.w
			var rotation_timeline: Resource = profile.get("CloudRotationTimeline")
			var pivot := host.get_node_or_null(component.get("CloudRootPath")) as Node3D
			if pivot != null and rotation_timeline != null:
				pivot.rotation = Vector3(0, deg_to_rad(-rotation_timeline.call("Sample", "GroupYaw", rotation_phase, 0.0)), 0)
			for pair in [["CloudsPath", "CloudTimeline"], ["HighCloudsPath", "HighCloudTimeline"], ["SecondaryCloudsPath", "SecondaryCloudTimeline"]]:
				var mesh := host.get_node_or_null(component.get(pair[0])) as MeshInstance3D
				var timeline: Resource = profile.get(pair[1])
				if mesh == null or timeline == null:
					continue
				mesh.visible = profile.get("CloudsEnabled")
				var clouds := mesh.material_override as ShaderMaterial
				if clouds == null:
					continue
				set_if_changed(clouds, "horizon_fade", profile.get("CloudHorizonFade"))
				set_if_changed(clouds, "sky_custom_fov", sky_fov)
				set_if_changed(clouds, "sky_pitch", sky_rotation.x)
				set_if_changed(clouds, "noise_time", cloud_time.z)
				set_if_changed(clouds, "cloud_sdf", timeline.call("Sample", "_Cloud_SDF_TSb", phase, 0.003))
				set_if_changed(clouds, "sun_moon", timeline.call("Sample", "_SunMoon", day_phase, 0.0))
				for colors in [["cloud_color_a", "_CloudColorA"], ["cloud_color_b", "_CloudColorB"], ["cloud_color_c", "_CloudColorC"], ["cloud_color_d", "_CloudColorD"], ["cloud_edge_color", "_Cloud_edgeColor"]]:
					set_if_changed(clouds, colors[0], timeline.call("SampleColor", colors[1], day_phase))


func set_if_changed(material: ShaderMaterial, parameter: String, value: Variant) -> void:
	var current: Variant = material.get_shader_parameter(parameter)
	# Plain vec4 uniforms return Vector4; C# SampleColor returns Color.
	# Normalize before comparison without adding source_color (which would change source colors).
	if current is Vector4 and value is Color:
		value = Vector4(value.r, value.g, value.b, value.a)
	elif current is Color and value is Vector4:
		value = Color(value.x, value.y, value.z, value.w)
	if typeof(current) != typeof(value) or current != value:
		material.set_shader_parameter(parameter, value)
