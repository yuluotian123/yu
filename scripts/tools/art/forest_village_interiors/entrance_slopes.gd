extends RefCounted
## Collision-only stair treatment shared by the house importer and repair command.
func apply(model: Node3D, asset: Dictionary) -> int:
	var count := 0
	for index: int in asset.collision.size():
		var data: Dictionary = asset.collision[index]
		if data.get("shape","") != "convex" or data.points.size() != 6: continue
		var original := PackedVector3Array()
		for value: Array in data.points: original.append(Vector3(value[0],value[1],value[2]))
		var bounds := AABB(original[0],Vector3.ZERO)
		for point: Vector3 in original: bounds = bounds.expand(point)
		if bounds.end.y>1.0: continue
		var inner := bounds.position.z
		var front := bounds.end.z
		var high := bounds.end.y
		var points := PackedVector3Array()
		for x: float in [bounds.position.x,bounds.end.x]:
			points.append(Vector3(x,-.12,inner-.16))
			points.append(Vector3(x,-.12,front+.16))
			points.append(Vector3(x,-.045,front+.16))
			if high>.4:
				# Raised decks need a short level crest before the platform's front face.
				points.append(Vector3(x,high+.01,inner+.18))
				points.append(Vector3(x,high+.01,inner-.16))
			else:
				points.append(Vector3(x,high,inner))
		var shape: CollisionShape3D = model.get_node("Collision1/Shape%02d" % index)
		var convex := ConvexPolygonShape3D.new()
		convex.points = points
		shape.shape = convex
		shape.set_meta("stair_collision_profile","buried_toe_and_overlapping_crest")
		count += 1
	return count
