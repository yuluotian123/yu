extends RefCounted
## Offline shoreline, bank collision and decoration clipping shared by rebuilds.
const HALF_LENGTH := 37.44
const WATER_Y := -0.25
const BASE_WIDTH := 3.12
const TOP_MARGIN := 0.40
const LAND_OVERLAP := 0.08
const WATER_OVERLAP := 0.035
const CLIP_STEP := 0.1
const CLIP_GUARD := 0.005
var exclusions: Dictionary = {}

func banks(z: float) -> Vector2:
	var bridge := 1.0-smoothstep(3.0,6.0,absf(z-2.34))
	var landing := 1.0-smoothstep(1.6,3.3,absf(z-16.38))
	var natural_weight := 1.0-maxf(bridge,landing)
	var center := 28.08+(sin(z*.17)*.62+sin(z*.43+1.2)*.22)*natural_weight
	var width := BASE_WIDTH+(sin(z*.21+.8)*.38+sin(z*.57-1.0)*.19)*natural_weight
	return Vector2(center-width*.5+sin(z*1.3)*.065*natural_weight,center+width*.5+sin(z*1.1+.8)*.075*natural_weight)

func dry(at: Vector2, margin := TOP_MARGIN) -> bool:
	var edge := banks(at.y)
	return at.x <= edge.x-margin or at.x >= edge.y+margin

func land_polygons(polygon: PackedVector2Array, margin := TOP_MARGIN) -> Array:
	var min_x := INF
	var max_x := -INF
	for point in polygon:
		min_x = minf(min_x,point.x)
		max_x = maxf(max_x,point.x)
	if max_x < 24.5 or min_x > 31.8: return [polygon]
	if not exclusions.has(margin):
		var mask := PackedVector2Array()
		# Short chords and an outward guard keep the curved bank clear between samples,
		# including the tight bend where the washing landing joins the natural shore.
		var steps := ceili(90.0/CLIP_STEP)
		for i in range(steps+1):
			var z := -45.0+i*CLIP_STEP
			mask.append(Vector2(banks(z).x-margin-CLIP_GUARD,z))
		for i in range(steps,-1,-1):
			var z := -45.0+i*CLIP_STEP
			mask.append(Vector2(banks(z).y+margin+CLIP_GUARD,z))
		exclusions[margin] = mask
	return Geometry2D.clip_polygons(polygon,exclusions[margin])

func deform_land(model: MeshInstance3D, west: bool) -> void:
	if model.has_meta("river_course_revision"): return
	var arrays: Array = model.mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	for i in vertices.size():
		var world := model.transform*vertices[i]
		var edge := banks(world.z)
		var weight := smoothstep(23.5,26.52,world.x) if west else 1.0-smoothstep(29.64,32.7,world.x)
		var offset := (edge.x-TOP_MARGIN-26.52) if west else (edge.y+TOP_MARGIN-29.64)
		world.x += offset*weight
		vertices[i] = model.transform.affine_inverse()*world
		uv[i].x += offset*weight*.85
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_TEX_UV] = uv
	var mesh := ArrayMesh.new()
	mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	mesh.surface_set_material(0,model.get_active_material(0))
	model.mesh = mesh
	for collision: CollisionShape3D in model.find_children("*","CollisionShape3D",true,false):
		collision.shape = mesh.create_trimesh_shape()
	model.set_meta("river_course_revision",1)

func triangle(tool: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, colors: Array[Color]) -> void:
	var normal := (c-a).cross(b-a).normalized()
	var points := [a,b,c]
	for i in 3:
		tool.set_normal(normal)
		tool.set_color(colors[i])
		tool.set_uv(Vector2(points[i].x,points[i].z)*.25)
		tool.add_vertex(points[i])

func bank_row(z: float) -> Array[Vector3]:
	var edge := banks(z)
	return [Vector3(edge.x-TOP_MARGIN-LAND_OVERLAP,-.008,z),Vector3(edge.x-.16,-.12,z),Vector3(edge.x+.10,-.36,z),
		Vector3(edge.x+.42,-.72,z),Vector3(edge.y-.42,-.72,z),Vector3(edge.y-.10,-.36,z),
		Vector3(edge.y+.16,-.12,z),Vector3(edge.y+TOP_MARGIN+LAND_OVERLAP,-.008,z)]

func rebuild_water(surface: MeshInstance3D) -> void:
	var material: ShaderMaterial = surface.get_active_material(0).duplicate()
	material.resource_name = "Forest stream · varied width and curved shoreline"
	material.set_shader_parameter("river_width",BASE_WIDTH)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_material(material)
	for row in 150:
		var z0 := lerpf(-HALF_LENGTH,HALF_LENGTH,float(row)/150.0)
		var z1 := lerpf(-HALF_LENGTH,HALF_LENGTH,float(row+1)/150.0)
		for column in 8:
			var vertices: Array[Vector3] = []
			for corner in [Vector2(column,z0),Vector2(column+1,z0),Vector2(column+1,z1),Vector2(column,z1)]:
				var edge := banks(corner.y)
				vertices.append(Vector3(lerpf(edge.x-WATER_OVERLAP,edge.y+WATER_OVERLAP,corner.x/8.0),WATER_Y,corner.y))
			for index in [0,1,2,0,2,3]:
				var point := vertices[index]
				var edge := banks(point.z)
				tool.set_normal(Vector3.UP)
				tool.set_uv(Vector2(point.z,maxf(0.0,minf(point.x-edge.x,edge.y-point.x))/BASE_WIDTH))
				tool.add_vertex(point)
	tool.index()
	surface.mesh = tool.commit()
	surface.position = Vector3.ZERO
	surface.set_meta("river_course_revision",1)

func clip_decoration(model: MeshInstance3D, flat: bool) -> int:
	var arrays: Array = model.mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var indices := PackedInt32Array()
	if arrays[Mesh.ARRAY_INDEX] != null: indices = arrays[Mesh.ARRAY_INDEX]
	if indices.is_empty():
		indices.resize(vertices.size())
		for i in indices.size(): indices[i] = i
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_material(model.get_active_material(0))
	var clipped := 0
	var written := 0
	for i in range(0,indices.size(),3):
		var ids := [indices[i],indices[i+1],indices[i+2]]
		var a := Vector2(vertices[ids[0]].x,vertices[ids[0]].z)
		var ab := Vector2(vertices[ids[1]].x,vertices[ids[1]].z)-a
		var ac := Vector2(vertices[ids[2]].x,vertices[ids[2]].z)-a
		var unchanged := dry(a) and dry(a+ab) and dry(a+ac)
		if not flat:
			if not unchanged:
				clipped += 1
				continue
		if unchanged and (maxf(a.x,maxf((a+ab).x,(a+ac).x)) < 24.5 or minf(a.x,minf((a+ab).x,(a+ac).x)) > 31.8 or not flat):
			for id_ in ids:
				tool.set_normal(normals[id_])
				tool.set_color(colors[id_])
				tool.set_uv(uv[id_])
				tool.add_vertex(vertices[id_])
				written += 1
			continue
		clipped += 1
		var det := ab.cross(ac)
		if absf(det) < .000001: continue
		for polygon: PackedVector2Array in land_polygons(PackedVector2Array([a,a+ab,a+ac])):
			for id_ in Geometry2D.triangulate_polygon(polygon):
				var point := polygon[id_]
				var b := (point-a).cross(ac)/det
				var c := ab.cross(point-a)/det
				var w := 1.0-b-c
				tool.set_normal((normals[ids[0]]*w+normals[ids[1]]*b+normals[ids[2]]*c).normalized())
				tool.set_color(colors[ids[0]]*w+colors[ids[1]]*b+colors[ids[2]]*c)
				tool.set_uv(uv[ids[0]]*w+uv[ids[1]]*b+uv[ids[2]]*c)
				tool.add_vertex(vertices[ids[0]]*w+vertices[ids[1]]*b+vertices[ids[2]]*c)
				written += 1
	if written == 0:
		model.free()
		return clipped
	tool.index()
	model.mesh = tool.commit()
	return clipped

func stones(terrain: Node3D) -> void:
	var old := terrain.get_node_or_null("RiverStones")
	if old != null: old.free()
	var mesh: Mesh = load("res://assets/models/environment/forest_town/river_stone.mesh.res")
	var multimesh := MultiMesh.new()
	multimesh.transform_format = MultiMesh.TRANSFORM_3D
	multimesh.mesh = mesh
	var transforms: Array[Transform3D] = []
	var rng := RandomNumberGenerator.new()
	rng.seed = 2026102617
	for center: float in [-33,-25,-17,-10,-5,10,13,22,28,34]:
		for side in 2:
			for i in 2:
				var z := center+rng.randf_range(-.8,.8)
				var edge := banks(z)
				var x := (edge.x if side == 0 else edge.y)+rng.randf_range(-.15,.15)
				var scale_ := rng.randf_range(.30,.58)/maxf(mesh.get_aabb().size.x,mesh.get_aabb().size.z)
				var basis := Basis(Vector3.UP,rng.randf()*TAU).scaled(Vector3(scale_,scale_*rng.randf_range(.55,.85),scale_))
				transforms.append(Transform3D(basis,Vector3(x,-.28-mesh.get_aabb().position.y*basis.y.length(),z)))
	multimesh.instance_count = transforms.size()
	var buffer := PackedFloat32Array()
	var bounds := AABB()
	for i in transforms.size():
		var t := transforms[i]
		buffer.append_array(PackedFloat32Array([t.basis.x.x,t.basis.y.x,t.basis.z.x,t.origin.x,t.basis.x.y,t.basis.y.y,t.basis.z.y,t.origin.y,t.basis.x.z,t.basis.y.z,t.basis.z.z,t.origin.z]))
		bounds = t*mesh.get_aabb() if i == 0 else bounds.merge(t*mesh.get_aabb())
	multimesh.buffer = buffer
	multimesh.custom_aabb = bounds
	var instance := MultiMeshInstance3D.new()
	instance.name = "RiverStones"
	instance.multimesh = multimesh
	var stone_material: ShaderMaterial = (load("res://assets/materials/environment/forest_town/masonry.tres") as ShaderMaterial).duplicate()
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().apply_surface_response(stone_material,"stone")
	stone_material.set_shader_parameter("weather_puddles_enabled",false)
	instance.material_override = stone_material
	instance.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
	instance.set_meta("ddgi_contribute",false)
	instance.set_meta("decorative_only",true)
	terrain.add_child(instance)

func build(village: Node3D) -> void:
	var terrain: Node3D = village.get_node("Terrain")
	deform_land(terrain.get_node("VillageGround"),true)
	deform_land(terrain.get_node("EastBank"),false)
	var contact: RefCounted = load("res://scripts/tools/art/forest_village_whitebox/ground_contact.gd").new()
	var palette := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()
	var source: ShaderMaterial = load(palette.ROOT+"ground.tres")
	contact.bake_ground(village,source)
	var material: ShaderMaterial = contact.contact_material(source,true)
	material.resource_name = "Forest stream · moss shoulder and exposed earth banks"
	material.set_shader_parameter("use_texture",false)
	material.set_shader_parameter("base_color",Color.WHITE)
	material.set_shader_parameter("color_saturation",1.0)
	material.set_shader_parameter("ground_contact_map",contact.ground_map)
	palette.apply_surface_response(material,"soil")
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_material(material)
	for row in 150:
		var a := bank_row(lerpf(-HALF_LENGTH,HALF_LENGTH,float(row)/150.0))
		var b := bank_row(lerpf(-HALF_LENGTH,HALF_LENGTH,float(row+1)/150.0))
		for band in 7:
			var c0 := Color("796c50").srgb_to_linear()
			var c1 := c0
			c0.a = 0.0 if band == 0 else .95
			c1.a = 0.0 if band == 6 else .95
			triangle(tool,a[band],a[band+1],b[band+1],[c0,c1,c1])
			triangle(tool,a[band],b[band+1],b[band],[c0,c1,c0])
	tool.index()
	var bed: MeshInstance3D = terrain.get_node("StreamBed")
	bed.mesh = tool.commit()
	bed.material_override = material
	bed.position = Vector3.ZERO
	for collision: CollisionShape3D in bed.find_children("*","CollisionShape3D",true,false): collision.shape = bed.mesh.create_trimesh_shape()
	rebuild_water(terrain.get_node("Stream"))
	stones(terrain)
	var clipped := 0
	if village.has_node("GroundContact"):
		for decoration: MeshInstance3D in village.get_node("GroundContact").find_children("*","MeshInstance3D",true,false):
			clipped += clip_decoration(decoration,decoration.name == "RootLitter")
	village.set_meta("river_course_revision",1)
	print("FOREST_RIVER_COURSE: curved banks, varied width, sloped bank collision, 40 native river stones; clipped ",clipped," decoration triangles near water")
