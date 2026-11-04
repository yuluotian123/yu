extends RefCounted
## Authoring only. Runtime scattering and shadowing are native FogVolumes.
const SCENE := "res://assets/scenes/environment/forest_village_light_shafts.tscn"

func apply(level: Node3D) -> void:
	var rig := level.get_node("EnvironmentRig")
	var environment: Environment = rig.get_node("WorldEnvironment").environment
	environment.volumetric_fog_enabled = true
	environment.volumetric_fog_density = 0.0
	environment.volumetric_fog_length = 60.0
	environment.volumetric_fog_detail_spread = 1.5
	environment.volumetric_fog_anisotropy = .2
	environment.volumetric_fog_ambient_inject = 0.0
	environment.volumetric_fog_gi_inject = 0.0
	environment.volumetric_fog_emission = Color.BLACK
	environment.volumetric_fog_sky_affect = 1.0
	# Fixed froxel samples keep the painted shadows stable in a stationary view.
	environment.volumetric_fog_temporal_reprojection_enabled = false
	rig.get_node("Sun").light_volumetric_fog_energy = 6.0
	rig.get_node("Moon").light_volumetric_fog_energy = 0.0
	for component: Resource in rig.get("Components"):
		if component.get_script().resource_path.ends_with("WeatherComponent3D.cs"):
			component.set("DriveVolumetricLightFromWeather",true)
	if rig.get_node_or_null("ForestLightShafts") == null:
		var shafts: Node3D = load(SCENE).instantiate()
		rig.add_child(shafts)
		shafts.owner = level
