extends RefCounted
## Preserve household locations; turn side-facing entrances and join their original access lanes.
const TURNED := ["Host","Smith","Carpenter","FarmerEast","FarmerWest","Smallholder"]

func apply(village: Node3D, builder: SceneTree) -> void:
	for name_: String in TURNED:
		var home: Node3D = village.get_node("Households/"+name_)
		var link: Path3D = village.get_node("Paths/"+name_+"DoorLink")
		var from: Vector3 = link.get_meta("side_access",link.curve.get_point_position(0))
		link.set_meta("side_access",from)
		home.rotation.y = 0.0
		home.editor_description += "" if home.editor_description.contains("入口朝向镜头") else " 入口朝向镜头（世界 +Z）。"
		var door: Vector3 = home.transform*home.get_node("DoorApproach").position
		door.y = 0.0
		from.y = 0.0
		var corner := Vector3(from.x,0,door.z)
		var curve := Curve3D.new()
		curve.bake_interval = .25
		for point: Vector3 in [from,corner,door]:
			if curve.point_count == 0 or point.distance_to(curve.get_point_position(curve.point_count-1)) > .05:
				curve.add_point(point)
		link.curve = curve
		for i in range(curve.point_count-1):
			var a := curve.get_point_position(i)
			var b := curve.get_point_position(i+1)
			builder.trail_runs.append([a,b,1.05])
			builder.planting_trails.append([a,b,1.4])
		builder.road_patches.append([Vector2(door.x,door.z),Vector2(1.25,1.05)])
		print("FRONT_DOOR ",name_," ",door)
