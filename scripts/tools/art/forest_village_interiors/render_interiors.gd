extends SceneTree
## Render the authored rooms with their saved lights, without starting ten sessions.
const OUT := "res://.godot/forest-village-interiors/"
func _initialize() -> void:
	run.call_deferred()
func run() -> void:
	root.size = Vector2i(1200,800)
	root.content_scale_size = root.size
	Engine.max_fps = 60
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string("res://assets/resources/art/forest_village/interiors_models.json"))
	var sheet := Image.create_empty(2000,600,false,Image.FORMAT_RGB8)
	for index: int in manifest.assets.size():
		var asset: Dictionary = manifest.assets[index]
		var path: String = "res://assets/scenes/levels/world_workshop.tscn" if asset.household == "Host" else "res://assets/scenes/levels/forest_village_interiors/"+asset.name+".tscn"
		var authored: Node3D = load(path).instantiate()
		var stage := Node3D.new()
		for name_: String in ["Interior","EnvironmentRig"]:
			var child := authored.get_node(name_)
			child.owner = null
			authored.remove_child(child)
			stage.add_child(child)
		if asset.household == "Host":
			for name_: String in ["Storage","StorageCopy","Bottle"]:
				var item: Node3D = authored.get_node(name_)
				var body: Node3D = item.get_node("Body").duplicate()
				body.transform = item.transform*body.transform
				stage.add_child(body)
		var camera: Camera3D = authored.get_node("CameraRig/Camera3D").duplicate()
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.size = maxf(float(asset.width)*.85,float(asset.depth))+.8
		camera.position = Vector3(6,6.8,8)
		camera.basis = Basis.looking_at(Vector3(0,.3,0)-camera.position)
		stage.add_child(camera)
		authored.free()
		root.add_child(stage)
		camera.current = true
		var label := Label.new()
		label.text = asset.name+" / "+asset.display_name
		label.position = Vector2(24,18)
		label.add_theme_font_size_override("font_size",24)
		root.add_child(label)
		for i: int in 12: await process_frame
		await RenderingServer.frame_post_draw
		var image := root.get_texture().get_image()
		assert(image.save_png(OUT+asset.name+"_overview.png") == OK)
		image.resize(400,300,Image.INTERPOLATE_LANCZOS)
		sheet.blit_rect(image,Rect2i(0,0,400,300),Vector2i((index%5)*400,(index/5)*300))
		label.free()
		stage.free()
	assert(sheet.save_png(OUT+"rooms_overview.png") == OK)
	print("FOREST_INTERIORS_RENDERED ten saved rooms")
	quit()
