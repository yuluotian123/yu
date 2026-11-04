extends SceneTree
## Static art review; no gameplay loop or changes to the production camera/environment.
const SCENES := "res://assets/scenes/environment/forest_vegetation/"
const OUT := "res://.godot/vegetation_v5/"
var stage: Node3D
var camera: Camera3D

func _initialize() -> void:
	run.call_deferred()

func own_all(node: Node, owner_node: Node) -> void:
	for child in node.get_children():
		child.owner = owner_node
		if child.scene_file_path.is_empty(): own_all(child,owner_node)

func capture(label: String) -> void:
	for i in 12: await process_frame
	await RenderingServer.frame_post_draw
	var image := root.get_texture().get_image()
	assert(image.save_png(OUT+label+".png") == OK)
	print("VEGETATION_CAPTURE ",label)

func add_asset(asset: String, position: Vector3, scale_amount := 1.0) -> Node3D:
	var node: Node3D = load(SCENES+asset+".tscn").instantiate()
	node.name = asset
	stage.add_child(node)
	node.position = position
	node.scale = Vector3.ONE*scale_amount
	return node

func run() -> void:
	root.size = Vector2i(1600,1000)
	root.scaling_3d_scale = 1.0
	root.msaa_3d = Viewport.MSAA_4X
	stage = Node3D.new()
	stage.name = "ForestVegetationReview"
	var environment := WorldEnvironment.new()
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color("d9d4be")
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color("a2b5b5")
	environment.environment.ambient_light_energy = 0.8
	environment.environment.tonemap_mode = Environment.TONE_MAPPER_LINEAR
	stage.add_child(environment)
	var sun := DirectionalLight3D.new()
	sun.name = "ReviewSun"
	sun.rotation_degrees = Vector3(-48,-32,0)
	sun.light_color = Color("fff1d7")
	sun.light_energy = 1.6
	sun.light_intensity_lux = 2.5
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 70
	stage.add_child(sun)
	var ground := MeshInstance3D.new()
	ground.mesh = PlaneMesh.new()
	ground.mesh.size = Vector2(100,100)
	var ground_mat := StandardMaterial3D.new()
	ground_mat.albedo_color = Color("b7b08a")
	ground_mat.roughness = 1.0
	ground.material_override = ground_mat
	ground.position.y = -0.02
	stage.add_child(ground)
	add_asset("oak_broad",Vector3(-10,0,-4))
	add_asset("hornbeam_tall",Vector3(0,0,-6))
	add_asset("oak_sweeping",Vector3(10,0,-3))
	add_asset("hazel_round",Vector3(-7,0,5))
	add_asset("hazel_tall",Vector3(-2.5,0,5))
	add_asset("woodland_low",Vector3(2.3,0,5))
	add_asset("oak_sapling",Vector3(7,0,4))
	add_asset("fern_fan",Vector3(0,0,7))
	camera = Camera3D.new()
	camera.name = "ReviewCamera"
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = 26
	camera.position = Vector3(27,29,44)
	stage.add_child(camera)
	root.add_child(stage)
	current_scene = stage
	camera.look_at(Vector3(0,8,-2))
	camera.current = true
	own_all(stage,stage)
	var packed := PackedScene.new()
	assert(packed.pack(stage) == OK)
	assert(ResourceSaver.save(packed,SCENES+"vegetation_gallery.tscn") == OK)
	await capture("kit_godot")
	# Close review at the actual character height used by the project.
	for asset in ["oak_broad","hornbeam_tall","oak_sweeping"]: stage.get_node(asset).visible = false
	stage.get_node("hazel_round").position = Vector3(-1.9,0,-.2)
	stage.get_node("hazel_round").scale = Vector3.ONE*.65
	stage.get_node("hazel_tall").position = Vector3(1.8,0,-1.0)
	stage.get_node("hazel_tall").scale = Vector3.ONE*.65
	stage.get_node("woodland_low").position = Vector3(.6,0,1.0)
	stage.get_node("woodland_low").scale = Vector3.ONE*.55
	stage.get_node("oak_sapling").position = Vector3(-3.8,0,-1.3)
	stage.get_node("fern_fan").position = Vector3(-.7,0,.4)
	var actor := Sprite3D.new()
	actor.name = "CharacterStyleReference"
	actor.texture = load("res://assets/textures/sprites/amber_fox_style_v2/greatsword_v4/sources/guard-keyframe-256.png")
	actor.texture_filter = BaseMaterial3D.TEXTURE_FILTER_NEAREST
	actor.pixel_size = .936/174.0
	actor.offset = Vector2(0,219-128)
	actor.billboard = BaseMaterial3D.BILLBOARD_FIXED_Y
	actor.shaded = false
	actor.alpha_cut = SpriteBase3D.ALPHA_CUT_DISCARD
	actor.position = Vector3(-.6,0,1.3)
	stage.add_child(actor)
	camera.size = 5.5
	camera.position = Vector3(4,3.8,10)
	camera.look_at(Vector3(-.7,1.15,0))
	own_all(stage,stage)
	var style_scene := PackedScene.new()
	assert(style_scene.pack(stage) == OK)
	assert(ResourceSaver.save(style_scene,SCENES+"character_style_review.tscn") == OK)
	await capture("character_style_godot")
	quit()
