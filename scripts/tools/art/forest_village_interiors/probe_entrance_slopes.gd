extends SceneTree
## Uses the actual player's square body, not the older capsule approximation.
func _initialize() -> void:
	run.call_deferred()
func run() -> void:
	var village: Node3D = load("res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn").instantiate()
	root.add_child(village)
	var body := CharacterBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = Vector3(.468,.936,.468)
	shape.shape = box
	body.add_child(shape)
	body.floor_snap_length = .12
	root.add_child(body)
	var failures := 0
	for home: Node3D in village.get_node("Households").get_children():
		var building: Node3D = home.get_node("Building")
		var target: Vector3 = building.get_node("DoorAnchor").global_position+building.global_basis*Vector3(0,0,.35)
		body.global_position = building.get_node("ApproachAnchor").global_position+Vector3(0,.02,0)
		body.velocity = Vector3.ZERO
		for i: int in 180:
			await physics_frame
			var dt := body.get_physics_process_delta_time()
			var remaining := target-body.global_position
			remaining.y = 0
			var planar := remaining.normalized()*minf(1.8,remaining.length()/dt)
			body.velocity = Vector3(planar.x,body.velocity.y-16.5*dt,planar.z)
			body.move_and_slide()
		var delta := Vector2(target.x-body.global_position.x,target.z-body.global_position.z).length()
		var passed := delta<.06 and body.is_on_floor()
		if not passed: failures += 1
		print("BOX_STAIR_PASS " if passed else "BOX_STAIR_FAIL ",home.name," error=",delta," position=",body.global_position)
		if not passed:
			for i: int in body.get_slide_collision_count():
				var hit := body.get_slide_collision(i)
				print("STAIR_CONTACT ",hit.get_collider().get_path()," ",hit.get_normal())
	print("BOX_STAIRS_VALIDATED failures=",failures)
	quit(0 if failures == 0 else 1)
