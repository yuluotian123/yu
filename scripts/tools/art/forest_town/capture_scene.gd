extends SceneTree
## Capture the actual level with its authored camera, lighting and post effects.

func _initialize() -> void:
	call_deferred("capture_level")

func capture_level() -> void:
	var level: Node3D = load("res://assets/scenes/levels/forest_town_level.tscn").instantiate()
	root.add_child(level)
	current_scene = level
	for frame in range(120):
		await process_frame
	await RenderingServer.frame_post_draw
	var actual_camera := root.get_viewport().get_camera_3d()
	var player := root.find_child("Player", true, false) as Node3D
	assert(player != null, "World session player must be loaded")
	var camera_info := {"position":actual_camera.global_position,"basis_x":actual_camera.global_basis.x,"basis_y":actual_camera.global_basis.y,"basis_z":actual_camera.global_basis.z,"fov":actual_camera.fov,"viewport":root.size,"player_position":player.global_position}
	var camera_file := FileAccess.open("res://.godot/forest-town/camera-state.json", FileAccess.WRITE)
	camera_file.store_string(JSON.stringify(camera_info,"\t"))
	camera_file.close()
	var compositor: Compositor = level.get_node("EnvironmentRig/WorldEnvironment").compositor
	for effect: CompositorEffect in compositor.compositor_effects if compositor != null else []:
		if effect.has_method("GetStatus"):
			var status: Dictionary = effect.call("GetStatus")
			var status_file := FileAccess.open("res://.godot/forest-town/ddgi-status.json", FileAccess.WRITE)
			status_file.store_string(JSON.stringify(status, "\t"))
			status_file.close()
	var image := root.get_texture().get_image()
	var destination := "res://.godot/forest-town/godot-composition.png"
	assert(image.save_png(destination) == OK)
	print("FOREST_TOWN_CAPTURE: ", destination)
	level.queue_free()
	await process_frame
	quit()
