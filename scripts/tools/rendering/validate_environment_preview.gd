extends SceneTree
## Native Environment settings must survive time changes and Relink toggles.

const ENVIRONMENT_PROPERTIES := [
	"ssao_enabled", "ssil_enabled", "sdfgi_enabled", "ssr_enabled", "glow_enabled",
	"adjustment_enabled", "tonemap_mode", "tonemap_exposure", "fog_enabled", "fog_mode",
	"fog_density", "fog_depth_begin", "fog_depth_end", "fog_depth_curve", "fog_height",
	"fog_height_density", "fog_aerial_perspective", "fog_sky_affect", "fog_sun_scatter",
	"background_intensity", "sky_custom_fov", "sky_rotation",
	"adjustment_saturation", "adjustment_contrast", "glow_intensity", "glow_hdr_threshold", "glow_bloom",
]
const EFFECT_SWITCHES := [
	"ssao_enabled", "ssil_enabled", "sdfgi_enabled", "ssr_enabled", "glow_enabled",
	"adjustment_enabled", "fog_enabled", "fog_mode", "tonemap_mode", "tonemap_exposure",
	"fog_aerial_perspective", "fog_sky_affect", "fog_sun_scatter",
]
const CAMERA_PROPERTIES := [
	"auto_exposure_enabled", "exposure_sensitivity", "exposure_aperture",
	"exposure_multiplier", "exposure_shutter_speed",
]
const LIGHT_PROPERTIES := [
	"visible", "light_energy", "light_indirect_energy", "light_intensity_lux",
	"light_temperature", "light_color", "light_angular_distance", "shadow_blur",
	"shadow_bias", "shadow_normal_bias", "directional_shadow_max_distance",
	"directional_shadow_mode", "directional_shadow_blend_splits",
	"directional_shadow_split_1", "directional_shadow_split_2", "directional_shadow_split_3",
]

var environment_notifications := 0
var camera_notifications := 0

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var profile = load("res://scripts/gamelogic/timeofday/TimeOfDayProfile.cs").new()
	var environment := Environment.new()
	environment.tonemap_mode = Environment.TONE_MAPPER_AGX
	environment.tonemap_exposure = 1.75
	environment.fog_mode = Environment.FOG_MODE_EXPONENTIAL
	environment.fog_density = 0.13
	environment.fog_aerial_perspective = 0.37
	environment.fog_sky_affect = 0.42
	environment.fog_sun_scatter = 0.27
	environment.background_intensity = 321.0
	environment.sky_custom_fov = 73.0
	environment.sky_rotation = Vector3(0.1, 0.2, 0.3)
	var sky := ShaderMaterial.new()
	sky.shader = load("res://assets/shaders/environment/day_night_sky.gdshader")
	var sun := DirectionalLight3D.new()
	var moon := DirectionalLight3D.new()
	sun.light_energy = 2.5
	sun.light_indirect_energy = 1.7
	sun.light_intensity_lux = 54321.0
	sun.shadow_bias = 0.23
	moon.light_energy = 0.75
	moon.light_indirect_energy = 0.6
	moon.light_intensity_lux = 0.8
	var exposure := CameraAttributesPhysical.new()
	exposure.auto_exposure_enabled = true
	exposure.exposure_sensitivity = 320.0
	exposure.exposure_aperture = 11.0
	exposure.exposure_shutter_speed = 83.0
	exposure.exposure_multiplier = 1.3
	var compositor = load("res://assets/resources/environment/relink_compositor.tres").duplicate(true)
	await process_frame
	environment.property_list_changed.connect(func(): environment_notifications += 1)
	exposure.property_list_changed.connect(func(): camera_notifications += 1)
	var preserved := true
	var stable := true
	for enabled in [true, false, true]:
		set_switches(environment, enabled)
		set_relink_active(compositor, enabled)
		await process_frame
		environment_notifications = 0
		camera_notifications = 0
		var native := snapshot(environment, ENVIRONMENT_PROPERTIES)
		var camera := snapshot(exposure, CAMERA_PROPERTIES)
		var sun_settings := snapshot(sun, LIGHT_PROPERTIES)
		var moon_settings := snapshot(moon, LIGHT_PROPERTIES)
		for hour in [0.0, 6.0, 12.0, 18.0, 23.0]:
			apply_profile(profile, environment, sky, sun, moon, exposure, compositor, hour)
			await process_frame
		preserved = unchanged(environment, native) and preserved
		preserved = unchanged(exposure, camera) and preserved
		preserved = unchanged(sun, sun_settings) and preserved
		preserved = unchanged(moon, moon_settings) and preserved
		stable = environment_notifications == 0 and camera_notifications == 0 and stable
	print("ENVIRONMENT_PREVIEW stable=", stable, " environment_notifications=",
		environment_notifications, " camera_notifications=", camera_notifications)
	profile.set("DriveFogFromProfile", true)
	profile.set("DrivePostEffectsFromProfile", true)
	profile.set("DriveRelinkBloomFromProfile", true)
	profile.set("DriveExposureFromProfile", true)
	profile.set("DriveLightSettingsFromProfile", true)
	profile.set("BlendNightPostEffects", true)
	var independent := true
	for enabled in [false, true]:
		set_switches(environment, enabled)
		profile.set("GlowEnabled", not enabled)
		set_relink_active(compositor, enabled)
		var switches := snapshot(environment, EFFECT_SWITCHES)
		apply_profile(profile, environment, sky, sun, moon, exposure, compositor, 0.0)
		independent = unchanged(environment, switches) and independent
		independent = exposure.auto_exposure_enabled and independent
		independent = is_equal_approx(sun.light_energy, 2.5) and is_equal_approx(sun.light_indirect_energy, 1.7) and independent
		independent = is_equal_approx(moon.light_energy, 0.75) and is_equal_approx(moon.light_indirect_energy, 0.6) and independent
	var previous_direction := sun.basis
	profile.call("ApplyLightDirections", sun, moon, 12.0, 0.5)
	var directions_move := not sun.basis.is_equal_approx(previous_direction)
	# Physical camera attributes are optional when the scene uses manual settings.
	profile.call("ApplyLighting", environment, sun, moon, null, 12.0, 0.5)
	print("ENVIRONMENT_PREVIEW manual_settings_preserved=", preserved,
		" switches_independent=", independent, " celestial_motion=", directions_move)
	sun.free()
	moon.free()
	quit(0 if stable and preserved and independent and directions_move else 1)

func set_switches(environment: Environment, enabled: bool) -> void:
	for property in ["ssao_enabled", "ssil_enabled", "sdfgi_enabled", "ssr_enabled", "glow_enabled", "adjustment_enabled", "fog_enabled"]:
		environment.set(property, enabled)

func set_relink_active(compositor: Compositor, enabled: bool) -> void:
	for effect in compositor.compositor_effects:
		effect.get("Profile").set("Active", enabled)

func apply_profile(profile: Resource, environment: Environment, sky: ShaderMaterial,
		sun: DirectionalLight3D, moon: DirectionalLight3D, exposure: CameraAttributesPhysical,
		compositor: Compositor, hour: float) -> void:
	profile.call("ApplyAtmosphere", environment, sky, hour)
	profile.call("ApplyLighting", environment, sun, moon, exposure, hour, 0.5)
	profile.call("ApplyRelinkPostProcessing", compositor, hour)

func snapshot(object: Object, properties: Array) -> Dictionary:
	var values := {}
	for property in properties:
		values[property] = object.get(property)
	return values

func unchanged(object: Object, expected: Dictionary) -> bool:
	var result := true
	for property in expected:
		if object.get(property) != expected[property]:
			push_error("Manual setting changed: " + property)
			result = false
	return result
