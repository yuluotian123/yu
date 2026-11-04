extends SceneTree
## Import the authored room library, build playable levels and refresh world directories.

const MODEL := "res://assets/models/environment/forest_village/interiors/"
const ROOMS := "res://assets/scenes/environment/forest_village/interiors/"
const LEVELS := "res://assets/scenes/levels/forest_village_interiors/"
const MANIFEST := "res://assets/resources/art/forest_village/interiors_models.json"
const FOREST := "res://assets/scenes/levels/forest_town_level.tscn"
const LEGACY := "res://assets/scenes/levels/world_workshop.tscn"
const LIB := preload("res://scripts/tools/art/forest_village_interiors/entrance_library.gd")
var saver := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()
var library := LIB.new()
var mats: Dictionary = {}

func _initialize() -> void:
	run.call_deferred()

func vec(data: Array) -> Vector3:
	return Vector3(data[0],data[1],data[2])

func save_scene(node: Node, path: String) -> void:
	library.own_children(node,node)
	var packed := PackedScene.new()
	assert(packed.pack(node) == OK)
	saver.save_resource(packed,path)

func behavior(name_: String) -> Resource:
	return load("res://scripts/gamelogic/"+name_+".cs").new()

func host(name_: String, components: Array = []) -> Node3D:
	var result: Node3D = load(LIB.HOST).new()
	result.name = name_
	result.set("Components",components)
	return result

func definitions(asset: Dictionary) -> Resource:
	if asset.household == "Host":
		var existing: Resource = load("res://assets/resources/world/levels/world_workshop.tres")
		existing.set("DisplayName","杂货与待客户 · 室内仓储")
		var place: Resource = existing.get("Location")
		place.set("Parent",load("res://assets/resources/world/locations/forest_town_level_location.tres"))
		place.set("Tags",PackedStringArray(["indoor","forest_village","household","storage"]))
		saver.save_resource(place,place.resource_path)
		saver.save_resource(existing,existing.resource_path)
		return existing
	var path: String = "res://assets/resources/world/levels/forest_house_"+asset.name+".tres"
	var record: Resource
	if FileAccess.file_exists(path): record = load(path)
	else:
		record = behavior("world/definitions/WorldLevelDefinition")
		record.set("Id","forest_house_"+asset.household.to_snake_case())
	var place_path: String = "res://assets/resources/world/locations/forest_house_"+asset.name+".tres"
	var place: Resource
	if FileAccess.file_exists(place_path): place = load(place_path)
	else:
		place = behavior("world/definitions/WorldLocationDefinition")
		place.set("Id","forest_house_location_"+asset.household.to_snake_case())
	place.set("Parent",load("res://assets/resources/world/locations/forest_town_level_location.tres"))
	place.set("DisplayName",asset.display_name+" · 室内")
	place.set("Tags",PackedStringArray(["indoor","forest_village","household"]))
	saver.save_resource(place,place_path)
	record.set("ScenePath",LEVELS+asset.name+".tscn")
	record.set("Location",place)
	record.set("DisplayName",asset.display_name+" · 室内")
	record.set("ExportPortalRoutes",true)
	saver.save_resource(record,path)
	return record

func import_models(manifest: Dictionary) -> void:
	for kind: String in ["Wood","WoodLight","Plaster","Stone","Iron","Glass","Plant"]:
		var mat: ShaderMaterial = load("res://assets/materials/environment/forest_village/houses/"+kind.to_snake_case()+".tres").duplicate()
		mat.resource_name = "Forest village interiors · "+kind
		mat.set_shader_parameter("weather_puddles_enabled",false)
		mat.set_shader_parameter("normal_scale",.42 if kind in ["Wood","WoodLight"] else .28)
		saver.save_resource(mat,"res://assets/materials/environment/forest_village/interiors/"+kind.to_snake_case()+".tres")
		mats[kind] = mat
	var doc := GLTFDocument.new()
	var state := GLTFState.new()
	state.handle_binary_image_mode = GLTFState.HANDLE_BINARY_IMAGE_MODE_EMBED_AS_UNCOMPRESSED
	assert(doc.append_from_file(MODEL+"forest_village_interiors.glb",state) == OK)
	var source := doc.generate_scene(state)
	var meshes := source.find_children("*","MeshInstance3D",true,false)
	assert(meshes.size() == 10)
	for asset: Dictionary in manifest.assets:
		var found: MeshInstance3D
		for node: MeshInstance3D in meshes:
			if String(node.name) == asset.name+"_interior": found = node
		assert(found != null,"Room mesh missing: "+asset.name)
		var mesh: ArrayMesh = found.mesh
		var triangles := 0
		for surface: int in mesh.get_surface_count():
			var data := mesh.surface_get_arrays(surface)
			assert(data[Mesh.ARRAY_TANGENT].size() == data[Mesh.ARRAY_VERTEX].size()*4,"Interior tangents")
			var kind := mesh.surface_get_material(surface).resource_name.trim_prefix("FV2_")
			mesh.surface_set_material(surface,mats[kind])
			triangles += data[Mesh.ARRAY_INDEX].size()/3
		assert(triangles == int(asset.triangles))
		var importer := ImporterMesh.from_mesh(mesh)
		importer.generate_lods(60.0,0.0,[])
		saver.save_resource(importer.get_mesh(),MODEL+asset.name+".mesh.res")
		var prefab := Node3D.new()
		prefab.name = String(asset.household)+"Interior"
		prefab.set_meta("household",asset.household)
		prefab.set_meta("theme",asset.theme)
		prefab.set_meta("cutaway_interior",true)
		var visual := MeshInstance3D.new()
		visual.name = "Model"
		visual.mesh = load(MODEL+asset.name+".mesh.res")
		prefab.add_child(visual)
		var body := StaticBody3D.new()
		body.name = "Collision"
		body.collision_mask = 0
		prefab.add_child(body)
		for index: int in asset.collision.size():
			var data: Dictionary = asset.collision[index]
			var shape := CollisionShape3D.new()
			shape.name = "Shape%03d" % index
			var box := BoxShape3D.new()
			box.size = vec(data.size)
			shape.shape = box
			shape.position = vec(data.position)
			body.add_child(shape)
		save_scene(prefab,ROOMS+asset.name+".tscn")
		prefab.free()
		print("INTERIOR_IMPORTED ",asset.name," ",triangles," triangles")
	source.free()

func lighting(level: Node3D, asset: Dictionary) -> void:
	var old := level.get_node_or_null("EnvironmentRig")
	if old != null: old.free()
	var rig := Node3D.new()
	rig.name = "EnvironmentRig"
	level.add_child(rig)
	var world := WorldEnvironment.new()
	world.name = "WorldEnvironment"
	world.environment = Environment.new()
	world.environment.background_mode = Environment.BG_COLOR
	world.environment.background_color = Color(.13,.17,.17)
	world.environment.background_intensity = 1800.0
	world.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	world.environment.ambient_light_color = Color(.66,.73,.73)
	world.environment.ambient_light_energy = .75
	world.environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	world.camera_attributes = CameraAttributesPhysical.new()
	(world.camera_attributes as CameraAttributesPhysical).exposure_shutter_speed = 30.0
	(world.camera_attributes as CameraAttributesPhysical).exposure_aperture = 8.0
	rig.add_child(world)
	var daylight := DirectionalLight3D.new()
	daylight.name = "WindowDaylight"
	daylight.rotation_degrees = Vector3(-52,-28,0)
	daylight.light_intensity_lux = 11000
	daylight.light_color = Color(.83,.91,1.0)
	daylight.shadow_enabled = true
	daylight.directional_shadow_max_distance = 18
	rig.add_child(daylight)
	var fire := OmniLight3D.new()
	fire.name = "HearthGlow"
	fire.position = Vector3(-float(asset.width)/2+.78,.85,-float(asset.depth)/2+.70)
	fire.light_color = Color(1,.59,.26)
	fire.light_intensity_lumens = 1800
	fire.omni_range = 4.5
	fire.shadow_enabled = true
	rig.add_child(fire)
	var fill := OmniLight3D.new()
	fill.name = "WindowBounce"
	fill.position = Vector3(0,1.8,-float(asset.depth)/2+.7)
	fill.light_color = Color(.72,.86,1)
	fill.light_intensity_lumens = 1500
	fill.omni_range = 6
	rig.add_child(fill)

func scene_for(asset: Dictionary, definition: Resource) -> Node3D:
	var legacy: bool = asset.household == "Host"
	var level: Node3D
	if legacy:
		level = load(LEGACY).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
		for name_: String in ["Floor","Interior"]:
			var old := level.get_node_or_null(name_)
			if old != null: old.free()
		for child: Node in level.get_children():
			if child is Label3D and child.text == "出门": child.free()
	else:
		var context := behavior("world/components/WorldLevelContextComponent3D")
		context.set("Level",definition)
		context.set("RequireNavigation",true)
		level = host(String(asset.household)+"HouseInterior",[context])
		var world := host("World",[behavior("ai/runtime/AiWorldComponent3D")])
		level.add_child(world)
		var nav := NavigationRegion3D.new()
		nav.name = "NavigationRegion3D"
		world.add_child(nav)
		var spawn := behavior("world/components/WorldSpawnPointComponent3D")
		var entry := host("EntryDefault",[spawn])
		level.add_child(entry)
		var marker := Marker3D.new()
		marker.name = "Arrival"
		entry.add_child(marker)
		var frame := Marker3D.new()
		frame.name = "CameraFramingTarget"
		level.add_child(frame)
		var cam_controller := behavior("camera/SceneCameraComponent3D")
		var rig := host("CameraRig",[cam_controller])
		level.add_child(rig)
		var camera := Camera3D.new()
		camera.name = "Camera3D"
		rig.add_child(camera)
		var portal := host("TownPortal")
		portal.set_meta("_world_placement",("forest_interior/"+asset.name+"/exit").md5_text())
		level.add_child(portal)
		level.add_child(load("res://assets/scenes/ui/runtime_gm.tscn").instantiate())
	var interior: Node3D = load(ROOMS+asset.name+".tscn").instantiate()
	interior.name = "Interior"
	level.add_child(interior)
	var spawn := vec(asset.default_spawn)
	level.get_node("EntryDefault/Arrival").position = spawn
	level.get_node("CameraFramingTarget").position = Vector3(0,.52,0)
	var at := Transform3D(Basis.IDENTITY,vec(asset.exit_position))
	library.configure(level.get_node("TownPortal"),load("res://assets/resources/world/levels/forest_town_level.tres"),"from_workshop" if legacy else "from_house_"+String(asset.household).to_snake_case(),"from_town",at,Transform3D(Basis.IDENTITY,spawn))
	var exit_label := Label3D.new()
	exit_label.name = "ExitSign"
	exit_label.position = Vector3(.70,1.12,float(asset.depth)/2-.12)
	exit_label.text = "出门"
	exit_label.font_size = 32
	exit_label.pixel_size = .003
	exit_label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	level.add_child(exit_label)
	var camera: Camera3D = level.get_node("CameraRig/Camera3D")
	camera.position = Vector3(0,5.72,7.2)
	camera.rotation_degrees = Vector3(-36,0,0)
	camera.current = true
	camera.fov = 47
	camera.near = .03
	camera.far = 40
	var controller := library.component(level.get_node("CameraRig"),"SceneCameraComponent3D")
	controller.set("Profile",load("res://assets/resources/camera/hd2d_free_3d.tres"))
	controller.set("FramingOriginPath",NodePath("../CameraFramingTarget"))
	controller.set("UseComponentFraming",true)
	controller.set("CameraOffset",Vector3(0,5.2,7.2))
	controller.set("CameraRotationDegrees",Vector3(-36,0,0))
	level.get_node("CameraRig").process_priority = 100
	lighting(level,asset)
	if legacy:
		level.get_node("Storage").position = Vector3(1.75,0,1.76)
		level.get_node("StorageCopy").position = Vector3(1.83,0,-1.20)
		for name_: String in ["Storage","StorageCopy"]:
			var storage: Node3D = level.get_node(name_)
			# These are level-local presentations; flatten the template so child edits save.
			storage.scene_file_path = ""
			storage.get_node("Body").scale = Vector3(.44,.44,.44)
			storage.get_node("Body").position.y = .22
			var crate_visual: MeshInstance3D = storage.get_node("Body/Mesh")
			crate_visual.mesh = load("res://assets/models/environment/forest_town/crate.mesh.res")
			var bounds := crate_visual.mesh.get_aabb()
			crate_visual.scale = Vector3.ONE/bounds.size
			crate_visual.position = -bounds.get_center()*crate_visual.scale
			crate_visual.visible = true
			crate_visual.material_override = mats.Wood
			storage.get_node("UsePosition").position = Vector3(-.65,0,.12)
		level.get_node("Bottle").position = Vector3(0,0,-2.0)
		level.get_node("Bottle").scene_file_path = ""
		var bottle_mesh: MeshInstance3D = level.get_node("Bottle/Body/Mesh")
		var pottery: ShaderMaterial = mats.Plaster.duplicate()
		pottery.set_shader_parameter("use_vertex_color",false)
		pottery.set_shader_parameter("base_color",Color("bda77f").srgb_to_linear())
		pottery.set_shader_parameter("uv_scale",Vector2(.23,.23))
		pottery.set_shader_parameter("uv_offset",Vector2(.01,.76))
		bottle_mesh.material_override = pottery
	level.set_meta("household",asset.household)
	level.editor_description = asset.display_name+"室内："+asset.theme+"。正门往返，后侧封闭；屋顶和前墙按游戏镜头剖开。"
	return level

func navigation(level: Node3D, asset: Dictionary) -> NavigationMesh:
	var source := Node3D.new()
	var room: Node3D = load(ROOMS+asset.name+".tscn").instantiate()
	source.add_child(room)
	if asset.household == "Host":
		for name_: String in ["Storage","StorageCopy","Bottle"]:
			var item: Node3D = level.get_node(name_)
			var body: Node3D = item.get_node("Body").duplicate()
			body.transform = item.transform*body.transform
			source.add_child(body)
	root.add_child(source)
	var nav := NavigationMesh.new()
	nav.cell_size = .12
	nav.cell_height = .05
	nav.agent_height = 1.0
	nav.agent_radius = .25
	nav.agent_max_climb = .22
	nav.geometry_parsed_geometry_type = NavigationMesh.PARSED_GEOMETRY_STATIC_COLLIDERS
	nav.geometry_collision_mask = 1
	nav.filter_baking_aabb = AABB(Vector3(-5,-.4,-5),Vector3(10,1.7,10))
	nav.region_min_size = 1.0
	var geometry := NavigationMeshSourceGeometryData3D.new()
	NavigationServer3D.parse_source_geometry_data(nav,geometry,source)
	NavigationServer3D.bake_from_source_geometry_data(nav,geometry)
	assert(nav.get_polygon_count()>0)
	source.free()
	return nav

func run() -> void:
	for path: String in [ROOMS,LEVELS,"res://assets/materials/environment/forest_village/interiors/","res://assets/resources/world/navigation/forest_village_interiors/"]:
		DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(path))
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST))
	import_models(manifest)
	for asset: Dictionary in manifest.assets:
		var record := definitions(asset)
		var level := scene_for(asset,record)
		var nav := navigation(level,asset)
		var nav_path: String = "res://assets/resources/world/navigation/forest_village_interiors/"+asset.name+".tres"
		saver.save_resource(nav,nav_path)
		level.get_node("World/NavigationRegion3D").navigation_mesh = nav
		save_scene(level,LEGACY if asset.household == "Host" else LEVELS+asset.name+".tscn")
		level.free()
	var forest: Node3D = load(FOREST).instantiate(PackedScene.GEN_EDIT_STATE_INSTANCE)
	library.apply(forest)
	save_scene(forest,FOREST)
	forest.free()
	var compiler: RefCounted = load("res://scripts/tools/art/forest_village_interiors/ForestInteriorAuthoring.cs").new()
	compiler.call("CompileWorld")
	print("FOREST_INTERIORS_BUILT: ten furnished rooms, sealed rear walls, ten precise front doors; legacy storage retained")
	quit()
