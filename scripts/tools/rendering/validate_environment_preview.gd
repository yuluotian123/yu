extends SceneTree
## Headless check for stable Inspector previews and path-traced indirect light.

var environment_notifications := 0
var camera_notifications := 0

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var profile = load("res://scripts/gamelogic/timeofday/TimeOfDayProfile.cs").new()
	profile.set("DriveFogFromProfile", true)
	var environment := Environment.new()
	var sky := ShaderMaterial.new()
	sky.shader = load("res://assets/shaders/environment/day_night_sky.gdshader")
	var sun := DirectionalLight3D.new()
	var moon := DirectionalLight3D.new()
	var exposure := CameraAttributesPhysical.new()
	var compositor = load("res://assets/resources/environment/relink_compositor.tres")
	profile.call("ApplyAtmosphere", environment, sky, 12.0)
	profile.call("ApplyLighting", environment, sun, moon, exposure, 12.0, 0.5)
	profile.call("ApplyRelinkPostProcessing", compositor, environment, 12.0)
	await process_frame
	environment.property_list_changed.connect(func(): environment_notifications += 1)
	exposure.property_list_changed.connect(func(): camera_notifications += 1)
	for frame in 10:
		profile.call("ApplyAtmosphere", environment, sky, 12.0)
		profile.call("ApplyLighting", environment, sun, moon, exposure, 12.0, 0.5)
		profile.call("ApplyRelinkPostProcessing", compositor, environment, 12.0)
		await process_frame
	var stable := environment_notifications == 0 and camera_notifications == 0
	print("ENVIRONMENT_PREVIEW stable=", stable, " environment_notifications=",
		environment_notifications, " camera_notifications=", camera_notifications)
	environment.set("pathtracing_enabled", true)
	profile.call("ApplyLighting", environment, sun, moon, exposure, 12.0, 0.5)
	var indirect_enabled := sun.light_indirect_energy == 1 and moon.light_indirect_energy == 1
	environment.set("pathtracing_enabled", false)
	profile.call("ApplyLighting", environment, sun, moon, exposure, 12.0, 0.5)
	var raster_restored := sun.light_indirect_energy == 0 and moon.light_indirect_energy == 0
	print("ENVIRONMENT_PREVIEW indirect_enabled=", indirect_enabled,
		" raster_restored=", raster_restored)
	# Real mode changes must still update the Inspector's available options.
	environment.fog_mode = Environment.FOG_MODE_EXPONENTIAL
	await process_frame
	environment_notifications = 0
	profile.call("ApplyAtmosphere", environment, sky, 12.0)
	await process_frame
	var mode_switch_refreshes := environment_notifications > 0
	print("ENVIRONMENT_PREVIEW mode_switch_refreshes=", mode_switch_refreshes)
	sun.free()
	moon.free()
	quit(0 if stable and indirect_enabled and raster_restored and mode_switch_refreshes else 1)
