extends SceneTree
## Native regression against the actual ten houses and three forest tree meshes.
const PARAM := "camera_solid_hide"
var effect: Resource
var camera: Camera3D

func _initialize() -> void:
	run.call_deferred()

func frames(count: int) -> void:
	for i in count: await process_frame

func hidden(mesh: MeshInstance3D) -> float:
	var value: Variant = mesh.get_instance_shader_parameter(PARAM)
	return 0.0 if value == null else float(value)

func sample(position: Vector3) -> void:
	camera.global_position = position
	effect.call("OnUpdate",1.0/60.0)

func run() -> void:
	create_timer(75).timeout.connect(func(): push_error("Camera interior validation timed out"); quit(1))
	var scene := Node3D.new()
	var village: Node3D = load("res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn").instantiate()
	village.name = "Village"
	scene.add_child(village)
	var rig: Node3D = load("res://scripts/gamelogic/gameobject/GameObject3D.cs").new()
	rig.name = "CameraRig"
	camera = Camera3D.new()
	camera.name = "Camera3D"
	camera.near = .001
	rig.add_child(camera)
	var framing: Resource = load("res://scripts/gamelogic/camera/SceneCameraComponent3D.cs").new()
	framing.set("Profile",load("res://assets/resources/camera/hd2d_free_3d.tres"))
	framing.set("UseComponentFraming",true)
	framing.set("CameraOffset",Vector3(0,0,20))
	framing.set("CameraRotationDegrees",Vector3.ZERO)
	rig.set("Components",[framing])
	scene.add_child(rig)
	var player := Node3D.new()
	player.name = "Player"
	scene.add_child(player)
	root.add_child(scene)
	current_scene = scene
	await frames(8)
	assert(camera.is_current())
	rig.process_mode = Node.PROCESS_MODE_DISABLED
	effect = load("res://scripts/gamelogic/camera/CameraOcclusionComponent3D.cs").new()
	effect.set("Owner",rig)
	effect.set("OccluderRootPath",NodePath("../Village"))
	effect.set("OccludedTransparency",0.0)
	effect.set("RestoreDelay",0.0)
	effect.set("RestoreSeconds",0.0)
	effect.call("OnInit")
	var houses := get_nodes_in_group("village_house")
	assert(houses.size() == 10)
	for house: Node3D in houses:
		var building: Node3D = house.get_node("Building")
		var model: MeshInstance3D = building.get_node("Model")
		var collider: CollisionShape3D = building.get_node("Collision1").get_child(0)
		var box: BoxShape3D = collider.shape
		var shadow_mode := model.cast_shadow
		var gi_mode := model.gi_mode
		sample(collider.global_position)
		assert(hidden(model) > .99,"Camera inside wall body remains visible: "+house.name)
		for other: Node3D in houses:
			if other != house: assert(hidden(other.get_node("Building/Model")) < .01,"Adjacent house was hidden")
		assert(model.visible and model.transparency == 0 and model.cast_shadow == shadow_mode and model.gi_mode == gi_mode)
		# The attic lies above the walk collider, below the actual pitched roof.
		sample(collider.global_transform*Vector3(0,box.size.y*.5+.1,0))
		assert(hidden(model) > .99,"Attic remains visible: "+house.name)
		# This point is in the model AABB but above the sloped roof near its side.
		var bounds := model.get_aabb()
		sample(building.global_transform*Vector3(box.size.x*.49,bounds.end.y-.01,0))
		assert(hidden(model) < .01,"Empty roof AABB corner hid a house: "+house.name)
		# A near plane crossing the wall must also be protected while the camera center is outside.
		camera.global_basis = collider.global_basis.orthonormalized()
		camera.near = .5
		sample(collider.global_transform*Vector3(0,0,box.size.z*.5+.1))
		assert(hidden(model) > .99,"Near-plane wall intersection not hidden: "+house.name)
		camera.near = .001
		sample(Vector3(0,1000,1000))
		assert(hidden(model) < .01)
		print("CAMERA_HOUSE_PASS ",house.name," body/attic/empty AABB/near plane/shadow-GI flags")
	var variants := {}
	for tree: Node3D in get_nodes_in_group("forest_tree"):
		if variants.has(tree.scene_file_path): continue
		variants[tree.scene_file_path] = true
		var model: MeshInstance3D = tree.get_node("Model")
		camera.global_basis = Basis.IDENTITY
		sample(model.global_transform*Vector3(0,1,0))
		assert(hidden(model) > .99,"Camera inside trunk remains visible: "+tree.scene_file_path)
		# A coarse rectangular trunk collider includes air outside the tapered bark.
		var collider: CollisionShape3D = tree.get_node("Collision1").get_child(0)
		var box: BoxShape3D = collider.shape
		var free_corner := false
		for x in [-.48,.48]:
			for z in [-.48,.48]:
				sample(collider.global_transform*Vector3(box.size.x*x,box.size.y*.35,box.size.z*z))
				free_corner = free_corner or hidden(model) < .01
		assert(free_corner,"Trunk detection reduced to its collider AABB")
		sample(Vector3(0,1000,1000))
		assert(hidden(model) < .01)
		print("CAMERA_TRUNK_PASS ",tree.scene_file_path," bark interior/adjacent air/restore")
	assert(variants.size() == 3)
	# Explicitly authored instance values and camera switches must restore cleanly.
	var model: MeshInstance3D = houses[0].get_node("Building/Model")
	effect.call("OnDestroy")
	model.set_instance_shader_parameter(PARAM,.2)
	effect.call("OnInit")
	sample(houses[0].get_node("Building/Collision1").get_child(0).global_position)
	assert(hidden(model) > .99)
	var other_camera := Camera3D.new()
	scene.add_child(other_camera)
	other_camera.make_current()
	effect.call("OnUpdate",.1)
	assert(is_equal_approx(hidden(model),.2))
	effect.call("OnDestroy")
	assert(is_equal_approx(hidden(model),.2))
	model.set_instance_shader_parameter(PARAM,null)
	effect.set("Owner",null)
	effect = null
	scene.queue_free()
	await frames(3)
	var modules := root.get_node_or_null("StandaloneModules")
	if modules != null: modules.queue_free()
	await frames(2)
	print("CAMERA_INTERIORS_VALIDATED: all 10 houses + 3 tree variants; adjacent geometry and native shadow/GI retained")
	quit()
