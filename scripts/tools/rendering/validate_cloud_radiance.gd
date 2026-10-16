extends SceneTree
## GPU-only regression: temporary cloud texture bindings must never be saved.

func _initialize() -> void:
	call_deferred("run")

func run() -> void:
	var host := Node3D.new()
	root.add_child(host)
	var clouds: Node3D = load("res://assets/scenes/environment/reference_clouds.tscn").instantiate()
	host.add_child(clouds)
	var environment: Environment = load("res://assets/resources/environment/relink_environment.tres").duplicate(true)
	var sky: ShaderMaterial = environment.sky.sky_material
	# Simulate an old asset saved with unusable editor-generated GPU resources.
	sky.set_shader_parameter("cloud_radiance_enabled", true)
	sky.set_shader_parameter("cloud_radiance_0", Texture2DRD.new())
	var validator = load("res://scripts/tools/rendering/CloudRadianceValidation.cs").new()
	validator.call("Bind", host, sky, clouds.get_node("BaseClouds"))
	for frame in 80:
		validator.call("Refresh")
		await process_frame
	var bound := bool(RenderingServer.material_get_param(sky.get_rid(), "cloud_radiance_enabled"))
	var texture: RID = RenderingServer.material_get_param(sky.get_rid(), "cloud_radiance_0")
	var valid_texture := texture.is_valid() and RenderingServer.texture_get_rd_texture(texture).is_valid()
	var output := "res://.godot/pathtracing-validation/cloud-material.tres"
	var save_error := ResourceSaver.save(sky, output)
	var saved := FileAccess.get_file_as_string(output)
	var clean := save_error == OK and not saved.contains("Texture2DRD") \
		and not saved.contains("cloud_radiance_enabled = true")
	validator.call("Release")
	var released := not bool(RenderingServer.material_get_param(sky.get_rid(), "cloud_radiance_enabled"))
	host.queue_free()
	for frame in 4:
		await process_frame
	print("CLOUD_RADIANCE bound=", bound, " valid_texture=", valid_texture,
		" serialized_clean=", clean, " released=", released)
	quit(0 if bound and valid_texture and clean and released else 1)
