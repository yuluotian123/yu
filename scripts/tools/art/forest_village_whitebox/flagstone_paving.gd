extends RefCounted
## Offline paving: irregular fitted slabs follow the authored path's coverage mask.

const GRID := 0.30
const CELL := Vector2(0.59,0.64)
var wear: Dictionary = {}
var sites: Dictionary = {}
var chunks: Dictionary = {}
var slabs := 0
var joints := 0.022

func coverage(at: Vector2) -> float:
	var p := at/GRID
	var cell := Vector2i(floori(p.x),floori(p.y))
	var f := p-Vector2(cell)
	return lerpf(lerpf(wear.get(cell,0.0),wear.get(cell+Vector2i.RIGHT,0.0),f.x),
		lerpf(wear.get(cell+Vector2i.DOWN,0.0),wear.get(cell+Vector2i.ONE,0.0),f.x),f.y)

func site(cell: Vector2i) -> Vector2:
	if not sites.has(cell):
		var rng := RandomNumberGenerator.new()
		rng.seed = 73919+cell.x*928371+cell.y*23917
		sites[cell] = (Vector2(cell)+Vector2(0.5,0.5))*CELL+Vector2(rng.randf_range(-0.15,0.15),rng.randf_range(-0.16,0.16))
	return sites[cell]

func clip_half_plane(polygon: PackedVector2Array, normal: Vector2, limit: float) -> PackedVector2Array:
	var clipped := PackedVector2Array()
	for i in range(polygon.size()):
		var a := polygon[i]
		var b := polygon[(i+1)%polygon.size()]
		var da := a.dot(normal)-limit
		var db := b.dot(normal)-limit
		if da <= 0.0: clipped.append(a)
		if (da < 0.0 and db > 0.0) or (da > 0.0 and db < 0.0):
			clipped.append(a.lerp(b,da/(da-db)))
	return clipped

func stone_polygon(cell: Vector2i) -> PackedVector2Array:
	var center := site(cell)
	var polygon := PackedVector2Array([center+Vector2(-1,-1),center+Vector2(1,-1),center+Vector2(1,1),center+Vector2(-1,1)])
	for x in range(-2,3):
		for z in range(-2,3):
			if x == 0 and z == 0: continue
			var neighbour := site(cell+Vector2i(x,z))
			var normal := neighbour-center
			polygon = clip_half_plane(polygon,normal,normal.dot((center+neighbour)*0.5))
	var inset := Geometry2D.offset_polygon(polygon,-joints,Geometry2D.JOIN_MITER)
	if inset.is_empty(): return PackedVector2Array()
	polygon = inset[0]
	if Geometry2D.is_polygon_clockwise(polygon): polygon.reverse()
	# Smaller outer stones leave a ragged earth shoulder without fading rock.
	for attempt in range(6):
		var inside := true
		for p in polygon:
			if coverage(p) < 0.22: inside = false
		if inside: break
		for i in range(polygon.size()): polygon[i] = center.lerp(polygon[i],0.84)
	return polygon

func triangle(tool: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, tone: Color, center: Vector2, angle: float) -> void:
	var normal := (b-a).cross(c-a).normalized()
	# Godot front faces use clockwise winding, independently of authored normals.
	for point in [a,c,b]:
		tool.set_normal(normal)
		var vertex_tone := tone
		vertex_tone.a = coverage(Vector2(point.x,point.z))
		tool.set_color(vertex_tone)
		var local := (Vector2(point.x,point.z)-center).rotated(angle)
		tool.set_uv(Vector2(0.125,0.375)+local*0.22)
		tool.add_vertex(point)

func build(parent: Node, earth: ShaderMaterial) -> void:
	for name_ in ["Flagstones","ForestStoneChips"]:
		var old := parent.get_node_or_null(name_)
		if old != null: old.free()
	var base: MeshInstance3D = parent.get_node("Surface")
	var arrays := base.mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	# Correct the old soil surface once; double-sided back faces invert NORMAL.
	if (vertices[indices[1]]-vertices[indices[0]]).cross(vertices[indices[2]]-vertices[indices[0]]).dot(normals[indices[0]]) > 0.0:
		for i in range(0,indices.size(),3):
			var previous := indices[i+1]
			indices[i+1] = indices[i+2]
			indices[i+2] = previous
		arrays[Mesh.ARRAY_INDEX] = indices
	# The shoulder settles into the terrain instead of ending in a raised sheet.
	for i in range(vertices.size()):
		vertices[i].y = lerpf(0.003,0.022,smoothstep(0.0,0.85,colors[i].a))
	arrays[Mesh.ARRAY_VERTEX] = vertices
	# The old late transparent overlay covered opaque weather/lighting effects.
	# Use the same opaque weather-capable ground shader for the exposed joints.
	var soil := earth.duplicate() as ShaderMaterial
	soil.resource_name = "Forest path · weathered earth joints"
	soil.set_shader_parameter("base_color",Color(1.08,1.0,0.86))
	soil.set_shader_parameter("use_vertex_coverage",true)
	soil.set_shader_parameter("vertex_coverage_cutoff",0.005)
	base.mesh = ArrayMesh.new()
	base.mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	base.mesh.surface_set_material(0,soil)
	base.set_meta("weather_surface","opaque ground with vertex boundary coverage")
	for i in range(vertices.size()):
		wear[Vector2i(roundi(vertices[i].x/GRID),roundi(vertices[i].z/GRID))] = colors[i].a
	var material := earth.duplicate() as ShaderMaterial
	material.resource_name = "Forest village · worn grey flagstone"
	material.set_shader_parameter("painterly_shadow_lift",0.05)
	material.set_shader_parameter("base_color",Color(1.10,1.04,0.94))
	material.set_shader_parameter("color_saturation",0.90)
	material.set_shader_parameter("texture_detail",0.48)
	material.set_shader_parameter("surface_roughness",0.98)
	var rng := RandomNumberGenerator.new()
	rng.seed = 20261026
	for x in range(-70,70):
		for z in range(-61,61):
			var center := site(Vector2i(x,z))
			if coverage(center) < 0.68: continue
			var edge_wear := smoothstep(0.95,0.68,coverage(center))
			if rng.randf() < edge_wear*0.22: continue
			# Leave the existing timber bridge open, including its rail clearances.
			if center.x > 25.05 and center.x < 31.15 and absf(center.y-2.34) < 1.8: continue
			var polygon := stone_polygon(Vector2i(x,z))
			if polygon.size() < 3: continue
			for i in range(polygon.size()):
				polygon[i] = center.lerp(polygon[i],1.0-edge_wear*0.16)
			var key := Vector2i(floori(center.x/6.0),floori(center.y/6.0))
			if not chunks.has(key):
				var surface := SurfaceTool.new()
				surface.begin(Mesh.PRIMITIVE_TRIANGLES)
				surface.set_material(material)
				chunks[key] = [surface,0]
			var tool: SurfaceTool = chunks[key][0]
			var grey := rng.randf_range(0.91,1.10)
			var warm := rng.randf_range(-0.018,0.018)
			var tone := Color(0.51+warm,0.54,0.50-warm)*grey
			tone.a = 1.0
			var height := rng.randf_range(0.042,0.061)-edge_wear*0.018
			var angle := rng.randf_range(0,TAU)
			var top := PackedVector3Array()
			var rim := PackedVector3Array()
			for point in polygon:
				var inward := point.move_toward(center,0.018)
				top.append(Vector3(inward.x,height,inward.y))
				rim.append(Vector3(point.x,height-0.013,point.y))
			var middle := Vector3.ZERO
			for point in top: middle += point
			middle /= top.size()
			for i in range(top.size()):
				var j := (i+1)%top.size()
				triangle(tool,middle,top[j],top[i],tone,center,angle)
				triangle(tool,top[i],top[j],rim[j],tone*0.94,center,angle)
				triangle(tool,top[i],rim[j],rim[i],tone*0.94,center,angle)
				var low_i := Vector3(rim[i].x,0.004,rim[i].z)
				var low_j := Vector3(rim[j].x,0.004,rim[j].z)
				triangle(tool,rim[i],rim[j],low_j,tone*0.77,center,angle)
				triangle(tool,rim[i],low_j,low_i,tone*0.77,center,angle)
			chunks[key][1] += 1
			slabs += 1
	var paving := Node3D.new()
	paving.name = "Flagstones"
	parent.add_child(paving)
	paving.set_meta("slab_count",slabs)
	paving.set_meta("decorative_only",true)
	paving.set_meta("style","irregular bevelled flagstone; earth joints and worn shoulders")
	for key: Vector2i in chunks:
		var instance := MeshInstance3D.new()
		instance.name = "Slabs_%d_%d" % [key.x,key.y]
		instance.mesh = (chunks[key][0] as SurfaceTool).commit()
		instance.set_meta("slab_count",chunks[key][1])
		paving.add_child(instance)
	assert(slabs > 700,"The flagstones must cover the village paths")
	print("FOREST_FLAGSTONES: ",slabs," decorative slabs in ",chunks.size()," chunks; no physics bodies; opaque weather materials")
