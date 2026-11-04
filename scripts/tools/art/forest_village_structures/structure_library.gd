extends RefCounted
## Offline scene assembly; saved meshes and simplified physics are shared at runtime.

const MODELS := "res://assets/models/environment/forest_village/structures/"
const SCENES := "res://assets/scenes/environment/forest_village/structures/"

func attach(parent: Node, child: Node) -> void:
	parent.add_child(child)

func visual(parent: Node, name_: String, asset: String, at: Vector3) -> MeshInstance3D:
	var model := MeshInstance3D.new()
	model.name = name_
	model.mesh = load(MODELS+asset+".mesh.res")
	model.position = at
	model.gi_mode = GeometryInstance3D.GI_MODE_STATIC
	attach(parent,model)
	return model

func collider(parent: Node, name_: String, at: Vector3, size: Vector3) -> void:
	var shape := CollisionShape3D.new()
	shape.name = name_
	var box := BoxShape3D.new()
	box.size = size
	shape.shape = box
	shape.position = at
	attach(parent,shape)

func fence(name_: String, a: Vector3, b: Vector3) -> Node3D:
	var run := Node3D.new()
	run.name = name_
	run.position = a
	var delta := b-a
	run.rotation.y = -atan2(delta.z,delta.x)
	run.set_meta("asset_category","finished_timber_fence")
	run.set_meta("fence_start",a)
	run.set_meta("fence_end",b)
	var length := delta.length()
	var count := ceili(length/2.2)
	var pitch := length/count
	for i: int in count+1:
		visual(run,"Post%02d" % i,"fence_post",Vector3(i*pitch,0,0))
	for i: int in count:
		var bay := visual(run,"Bay%02d" % i,"fence_bay_a" if i%2 == 0 else "fence_bay_b",Vector3(i*pitch,0,0))
		bay.scale.x = pitch/2.0
	var body := StaticBody3D.new()
	body.name = "Collision"
	body.collision_mask = 0
	attach(run,body)
	# Exactly the old two rail volumes; no new obstruction across yard entrances.
	for i: int in 2:
		collider(body,"Rail%d" % i,Vector3(length/2.0,0.38+i*0.35,0),Vector3(length,0.12,0.09))
	return run

func bridge() -> Node3D:
	var result: Node3D = load(SCENES+"creek_bridge.tscn").instantiate()
	result.name = "Bridge"
	result.position = Vector3(28.08,0,2.34)
	return result

func own_children(parent: Node, scene: Node) -> void:
	for child: Node in parent.get_children():
		child.owner = scene
		if child.scene_file_path.is_empty(): own_children(child,scene)
