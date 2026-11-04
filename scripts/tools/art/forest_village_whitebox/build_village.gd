extends SceneTree
## Rebuilds the editable blockout scenes. No runtime geometry generation.

const ENV_PATH := "res://assets/scenes/environment/forest_village_whitebox/forest_village_whitebox.tscn"
const LEVEL_PATH := "res://assets/scenes/levels/forest_village_whitebox.tscn"
const PLAN_SCALE := 0.78
const FOREST_MODEL_ROOT := "res://assets/scenes/environment/forest_vegetation/"
const HOUSE_MODEL_ROOT := "res://assets/scenes/environment/forest_village/houses/"
const TREE_MODELS := ["oak_broad","hornbeam_tall","oak_sweeping"]
const SHRUB_MODELS := ["hazel_round","hazel_tall","woodland_low"]
const HOUSE_MODELS := {
	"Chief":["01_chief",1.0], "Host":["02_host",1.0],
	"Smith":["03_smith",1.0], "Carpenter":["04_carpenter",1.0],
	"Herbalist":["05_herbalist",1.0], "Forester":["06_forester",1.0],
	"Gatherer":["07_gatherer",1.0], "FarmerEast":["08_farmer_east",1.0],
	"FarmerWest":["09_farmer_west",1.0], "Smallholder":["10_smallholder",1.0]
}
const GROUND_MATERIAL := "res://assets/materials/environment/forest_town/ground.tres"
const HOUSES := [
	["Chief", "01 村长家", Vector3(-1,0,-18), Vector3(7.2,3.5,6.0), 0.0, "议事、调解、公共账册；门廊面向共同空地"],
	["Host", "02 杂货与待客户", Vector3(-12,0,12), Vector3(7.5,3.1,6.0), 90.0, "入口附近接待来客；前店后宅，兼储货"],
	["Smith", "03 工具修补户", Vector3(21,0,10), Vector3(6.0,2.8,5.2), -90.0, "村边修补农具；独立炉台与防火空地"],
	["Carpenter", "04 木工户", Vector3(-24,0,-5), Vector3(6.4,2.8,5.6), 90.0, "接林道与村路；院内锯台和木料"],
	["Herbalist", "05 草药照护户", Vector3(12,0,-18), Vector3(5.6,2.8,5.4), 0.0, "安静短巷中的家庭照护；后院种植草药"],
	["Forester", "06 伐木户", Vector3(-23,0,-20), Vector3(5.8,2.7,5.4), 20.0, "林内家庭；后侧接生产林道"],
	["Gatherer", "07 猎采户", Vector3(-12,0,-7), Vector3(5.6,2.7,5.2), 0.0, "兼采集、晾晒与季节劳动；邻里共用短巷"],
	["FarmerEast", "08 东田农户", Vector3(11,0,20), Vector3(5.8,2.8,5.4), -90.0, "前门接村路，后院通向耕地"],
	["FarmerWest", "09 西田农户", Vector3(-11,0,25), Vector3(6.0,2.8,5.6), 90.0, "与另一农户共享晒场、农具与田间路"],
	["Smallholder", "10 小畜养户", Vector3(21,0,-6), Vector3(5.8,2.7,5.2), -90.0, "住宅与围栏分开；外缘草场兼少量蜂箱"]
]

var mats: Dictionary = {}
var boxes: Dictionary = {}
var village: Node3D
var annotations: Node3D
var trail_runs: Array = []
var planting_trails: Array = []
var road_patches: Array = []
var road_bins: Dictionary = {}
var road_noise := FastNoiseLite.new()
var tree_points: Array[Vector3] = []
var forest_scenes: Dictionary = {}
var painted_meshes: Dictionary = {}
var ground_palettes := {"ground":[],"path":[],"yard":[],"stone":[]}
var forest_gardens := [
	[Vector3(-22,0,28),Vector2(9,7)],
	[Vector3(22,0,27),Vector2(9,7)],
	[Vector3(12,0,-25),Vector2(6,4)],
	[Vector3(-18.5,0,19),Vector2(4,5)]
]

func layout_point(value: Vector3) -> Vector3:
	return Vector3(value.x * PLAN_SCALE, value.y, value.z * PLAN_SCALE)

func layout_box(parent: Node, name_: String, size: Vector3, at: Vector3, mat: String, solid := false) -> MeshInstance3D:
	return box(parent, name_, layout_point(size), layout_point(at), mat, solid)

func layout_label(text_: String, at: Vector3, size := 42) -> void:
	label(text_, layout_point(at), size)

func _initialize() -> void:
	call_deferred("build")

func node(parent: Node, name_: String) -> Node3D:
	var result := Node3D.new()
	result.name = name_
	parent.add_child(result)
	return result

func material(key: String, color: Color) -> StandardMaterial3D:
	var result := StandardMaterial3D.new()
	result.resource_name = "Whitebox " + key
	result.albedo_color = color
	result.roughness = 1.0
	mats[key] = result
	return result

func prepare_ground() -> void:
	var source: Mesh = load("res://assets/models/environment/forest_town/village_ground.mesh.res")
	var arrays := source.surface_get_arrays(0)
	var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	for i in range(colors.size()):
		var cell := Vector2i(floori(uv[i].x*4.0),floori(uv[i].y*4.0))
		var kind := ""
		if cell == Vector2i(2,1): kind = "path"
		elif cell == Vector2i(0,3): kind = "ground"
		elif cell == Vector2i(0,1): kind = "stone"
		if not kind.is_empty() and ground_palettes[kind].size() < 24 and not ground_palettes[kind].has(colors[i]):
			ground_palettes[kind].append(colors[i])
	for kind in ["ground","path","stone"]:
		assert(not ground_palettes[kind].is_empty(),"Forest ground palette missing: "+kind)
	ground_palettes["yard"] = ground_palettes["path"]
	var forest_ground: Material = load(GROUND_MATERIAL)
	for kind in ["ground","path","yard"]:
		mats[kind] = load("res://assets/materials/environment/forest_village/grass.tres") if kind == "ground" else forest_ground

func painted_ground(source: Mesh, kind: String) -> Mesh:
	var key := str(source.get_instance_id())+":"+kind
	if painted_meshes.has(key): return painted_meshes[key]
	var geometry: Mesh = source
	if source is BoxMesh:
		var box_mesh: BoxMesh = source.duplicate()
		box_mesh.subdivide_width = maxi(0,ceili(box_mesh.size.x/1.4)-1)
		box_mesh.subdivide_depth = maxi(0,ceili(box_mesh.size.z/1.4)-1)
		geometry = box_mesh
	var arrays := geometry.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	var colors := PackedColorArray()
	colors.resize(vertices.size())
	var palette: Array = ground_palettes[kind]
	var offset := Vector2(0.003,0.753) if kind == "ground" else Vector2(0.503,0.253)
	for i in range(vertices.size()):
		var at := vertices[i]
		colors[i] = palette[absi(roundi(at.x*7.0+at.z*13.0))%palette.size()]
		var tile := Vector2(fposmod(at.x*0.32,1.0),fposmod(at.z*0.32,1.0)) if kind == "ground" else uv[i]
		uv[i] = Vector2(at.x,at.z)*0.85 if kind == "ground" else offset+tile*0.244
	arrays[Mesh.ARRAY_TEX_UV] = uv
	arrays[Mesh.ARRAY_COLOR] = colors
	var painted := ArrayMesh.new()
	painted.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	painted.surface_set_material(0,mats[kind])
	painted_meshes[key] = painted
	return painted

func mesh(parent: Node, name_: String, shape: Mesh, at: Vector3, mat: String) -> MeshInstance3D:
	var instance := MeshInstance3D.new()
	instance.name = name_
	instance.mesh = painted_ground(shape,mat) if mat in ["ground","path","yard"] else shape
	instance.material_override = mats[mat]
	instance.position = at
	parent.add_child(instance)
	return instance

func box(parent: Node, name_: String, size: Vector3, at: Vector3, mat: String, solid := false) -> MeshInstance3D:
	var key := str(size)
	if not boxes.has(key):
		var resource := BoxMesh.new()
		resource.size = size
		boxes[key] = resource
	var instance := mesh(parent, name_, boxes[key], at, mat)
	if solid:
		var body := StaticBody3D.new()
		body.name = "Collision"
		body.collision_mask = 0
		instance.add_child(body)
		var collision := CollisionShape3D.new()
		var shape := BoxShape3D.new()
		shape.size = size
		collision.shape = shape
		body.add_child(collision)
	return instance

func cylinder(parent: Node, name_: String, radius: float, height: float, at: Vector3, mat: String, solid := false) -> MeshInstance3D:
	var shape := CylinderMesh.new()
	shape.top_radius = radius
	shape.bottom_radius = radius
	shape.height = height
	shape.radial_segments = 12
	var instance := mesh(parent, name_, shape, at, mat)
	if solid:
		var body := StaticBody3D.new()
		body.name = "Collision"
		body.collision_mask = 0
		instance.add_child(body)
		var collision := CollisionShape3D.new()
		var cylinder_shape := CylinderShape3D.new()
		cylinder_shape.radius = radius
		cylinder_shape.height = height
		collision.shape = cylinder_shape
		body.add_child(collision)
	return instance

func label(text_: String, at: Vector3, size := 42) -> void:
	var result := Label3D.new()
	result.name = "Note_%d" % annotations.get_child_count()
	result.text = text_
	result.position = at
	result.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	result.font_size = size
	result.pixel_size = 0.04
	result.modulate = Color(0.16,0.20,0.20)
	result.outline_modulate = Color(0.97,0.97,0.93)
	result.outline_size = 4
	result.no_depth_test = true
	annotations.add_child(result)

func route(parent: Node, name_: String, points: Array, width := 2.4) -> void:
	var path := Path3D.new()
	path.name = name_
	parent.add_child(path)
	var curve := Curve3D.new()
	curve.bake_interval = 0.35
	for i in range(points.size()):
		var at := layout_point(points[i])
		var before := layout_point(points[maxi(0,i-1)])
		var after := layout_point(points[mini(points.size()-1,i+1)])
		var tangent := (after-before)*0.18
		var limit := at.distance_to(after) if i == 0 else at.distance_to(before)
		if i > 0 and i < points.size()-1: limit = minf(limit,at.distance_to(after))
		tangent = tangent.limit_length(limit*0.30)
		curve.add_point(at,-tangent,tangent)
		if i < points.size()-1:
			# Preserve the established forest planting clearances while rebuilding
			# the visible trail as a narrower, smoothly turning worn surface.
			planting_trails.append([at,after,minf(width*0.7,1.8)])
	path.curve = curve
	width = clampf(width*0.55,0.85,1.55)
	var points_baked := curve.get_baked_points()
	var phase := float(absi(name_.hash())%1000)*0.01
	var distance := 0.0
	for i in range(points_baked.size()-1):
		var a: Vector3 = points_baked[i]
		var b: Vector3 = points_baked[i+1]
		if a.distance_squared_to(b) < 0.0001: continue
		distance += a.distance_to(b)
		var variation := 1.0+sin(distance*0.7+phase)*0.10+sin(distance*1.8+phase)*0.04
		trail_runs.append([a,b,width*variation])

func road_field(at: Vector2) -> float:
	var distance := 1000.0
	var bin := Vector2i(floori(at.x/3.0),floori(at.y/3.0))
	for index in road_bins.get(bin,[]):
		var run: Array = trail_runs[index]
		var a := Vector2(run[0].x,run[0].z)
		var delta := Vector2(run[1].x,run[1].z)-a
		var nearest := a+delta*clampf((at-a).dot(delta)/delta.length_squared(),0.0,1.0)
		distance = minf(distance,at.distance_to(nearest)-float(run[2])*0.5)
	for patch in road_patches:
		var relative: Vector2 = at-patch[0]
		var radius: Vector2 = patch[1]
		distance = minf(distance,((relative/radius).length()-1.0)*minf(radius.x,radius.y))
	return distance+road_noise.get_noise_2d(at.x,at.y)*0.18

func build_paths(previous_wear: Dictionary = {}) -> void:
	road_noise.seed = 20261025
	road_noise.frequency = 0.65
	road_noise.fractal_octaves = 2
	for index in range(trail_runs.size()):
		var run: Array = trail_runs[index]
		var a := Vector2(run[0].x,run[0].z)
		var b := Vector2(run[1].x,run[1].z)
		var reach := float(run[2])*0.5+0.8
		for x in range(floori((minf(a.x,b.x)-reach)/3.0),floori((maxf(a.x,b.x)+reach)/3.0)+1):
			for z in range(floori((minf(a.y,b.y)-reach)/3.0),floori((maxf(a.y,b.y)+reach)/3.0)+1):
				var bin := Vector2i(x,z)
				if not road_bins.has(bin): road_bins[bin] = []
				road_bins[bin].append(index)
	const STEP := 0.30
	const ORIGIN := Vector2(-40.8,-37.5)
	const COLUMNS := 273
	const ROWS := 251
	var fields := PackedFloat32Array()
	fields.resize(COLUMNS*ROWS)
	for z in range(ROWS):
		for x in range(COLUMNS):
			fields[z*COLUMNS+x] = road_field(ORIGIN+Vector2(x,z)*STEP)
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	var uv := PackedVector2Array()
	var indices := PackedInt32Array()
	var used: Dictionary = {}
	var soil_color := Color(0,0,0,0)
	for tone in ground_palettes["path"]: soil_color += tone
	soil_color /= ground_palettes["path"].size()
	for z in range(ROWS-1):
		for x in range(COLUMNS-1):
			var n := z*COLUMNS+x
			var old_wear := maxf(maxf(previous_wear.get(n,0.0),previous_wear.get(n+1,0.0)),maxf(previous_wear.get(n+COLUMNS,0.0),previous_wear.get(n+COLUMNS+1,0.0)))
			if old_wear <= 0.0 and minf(minf(fields[n],fields[n+1]),minf(fields[n+COLUMNS],fields[n+COLUMNS+1])) > 0.34: continue
			var midpoint := ORIGIN+(Vector2(x,z)+Vector2(0.5,0.5))*STEP
			if midpoint.x > 25.15 and midpoint.x < 31.01 and absf(midpoint.y-2.34) < 1.65: continue
			var quad := PackedInt32Array()
			for grid in [n,n+1,n+COLUMNS,n+COLUMNS+1]:
				if not used.has(grid):
					var at := ORIGIN+Vector2(grid%COLUMNS,grid/COLUMNS)*STEP
					var wear := maxf(previous_wear.get(grid,0.0),1.0-smoothstep(-0.22,0.32,fields[grid]))
					var shade := 0.96+road_noise.get_noise_2d(at.x+31,at.y-17)*0.08
					var tone := soil_color*shade
					tone.a = wear
					used[grid] = vertices.size()
					vertices.append(Vector3(at.x,0.022,at.y))
					normals.append(Vector3.UP)
					colors.append(tone)
					uv.append(Vector2(0.503,0.253)+Vector2(fposmod(at.x*0.28,1.0),fposmod(at.y*0.28,1.0))*0.244)
				quad.append(used[grid])
			indices.append_array(PackedInt32Array([quad[0],quad[2],quad[1],quad[1],quad[2],quad[3]]))
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_TEX_UV] = uv
	arrays[Mesh.ARRAY_INDEX] = indices
	var geometry := ArrayMesh.new()
	geometry.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	var trail: ShaderMaterial = (mats["path"] as ShaderMaterial).duplicate()
	trail.resource_name = "Forest path · weathered earth joints"
	trail.set_shader_parameter("use_vertex_coverage",true)
	geometry.surface_set_material(0,trail)
	var surface := MeshInstance3D.new()
	surface.name = "Surface"
	surface.mesh = geometry
	surface.set_meta("source_material",GROUND_MATERIAL)
	surface.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	village.get_node("Paths").add_child(surface)
	var paving := preload("res://scripts/tools/art/forest_village_whitebox/flagstone_paving.gd").new()
	paving.build(village.get_node("Paths"),mats["path"])
	preload("res://scripts/tools/art/forest_village_whitebox/ground_contact.gd").new().build(village,paving,mats["path"])
	print("FOREST PATHS: continuous curved surface; ",vertices.size()," vertices; ",indices.size()/3," triangles")

func fence(parent: Node, a: Vector3, b: Vector3) -> void:
	var library := preload("res://scripts/tools/art/forest_village_structures/structure_library.gd").new()
	parent.add_child(library.fence("Fence_%d" % parent.get_child_count(),a,b))

func plot(parent: Node, name_: String, at: Vector3, size: Vector2, rows: int) -> void:
	at = layout_point(at)
	size *= PLAN_SCALE
	var garden := node(parent,name_)
	box(garden,"Soil",Vector3(size.x,0.06,size.y),at+Vector3(0,0.035,0),"soil")
	for i in range(rows):
		var x := -size.x/2+(i+0.5)*size.x/rows
		box(garden,"Bed_%02d" % i,Vector3(size.x/rows*0.55,0.16,size.y-0.6),at+Vector3(x,0.12,0),"crop")

func make_house(parent: Node, entry: Array) -> void:
	var home := node(parent,entry[0])
	home.position = entry[2]
	home.rotation_degrees.y = entry[4]
	home.add_to_group("village_house",true)
	home.add_to_group("camera_occluder",true)
	home.set_meta("household",entry[1])
	home.editor_description = entry[5] + "。第二版完整四面住宅模型，内部暂不开放。"
	var s: Vector3 = entry[3]
	var spec: Array = HOUSE_MODELS[entry[0]]
	var building: Node3D = load(HOUSE_MODEL_ROOT+spec[0]+".tscn").instantiate()
	building.name = "Building"
	home.add_child(building)
	building.add_to_group("camera_near_fade",true)
	var uniform_scale: float = spec[1]
	building.scale = Vector3.ONE*uniform_scale
	var model: MeshInstance3D = building.get_node("Model")
	var bounds := model.get_aabb()
	var approach_anchor: Vector3 = building.get_node("ApproachAnchor").position*uniform_scale
	var door_x := approach_anchor.x
	var approach_z := approach_anchor.z
	if entry[0] == "Gatherer": approach_z += .30
	var yard_center := home.transform*Vector3(door_x*0.35,0,approach_z)
	road_patches.append([Vector2(yard_center.x,yard_center.z),Vector2(1.25,1.05)])
	var entrance := Marker3D.new()
	entrance.name = "DoorApproach"
	entrance.position = Vector3(door_x,0.5,approach_z)
	entrance.add_to_group("village_entrance",true)
	home.add_child(entrance)
	# Short fence sides leave the entire road-facing entrance open.
	fence(home,Vector3(-s.x/2-0.8,0,-s.z/2-3),Vector3(-s.x/2-0.8,0,-s.z/2))
	fence(home,Vector3(s.x/2+0.8,0,-s.z/2-3),Vector3(s.x/2+0.8,0,-s.z/2))
	label(entry[1],entry[2]+Vector3(0,bounds.end.y*uniform_scale+0.45,0))
	var from := home.transform*Vector3(0,0,s.z/2+1.6)
	var to := home.transform*Vector3(door_x,0,approach_z)
	if from.distance_to(to) > 0.1:
		route(village.get_node("Paths"),String(home.name)+"DoorLink",[from/PLAN_SCALE,to/PLAN_SCALE],1.6)

func build_environment() -> Node3D:
	village = Node3D.new()
	village.name = "ForestVillageWhitebox"
	village.editor_description = "森林内部聚落白模：连续乔木林中的十户林隙住宅，以穿林小径连接公共井、议事处和家庭生产小院。"
	annotations = node(village,"Annotations")
	annotations.visible = false
	var land := node(village,"Terrain")
	layout_box(land,"VillageGround",Vector3(86,0.7,96),Vector3(-9,-0.35,0),"ground",true)
	layout_box(land,"EastBank",Vector3(14,0.7,96),Vector3(45,-0.35,0),"ground",true)
	layout_box(land,"StreamBed",Vector3(4,0.5,96),Vector3(36,-0.95,0),"soil",true)
	make_river(land)
	var roads := node(village,"Paths")
	route(roads,"VillageSpine",[Vector3(2,0,43),Vector3(3,0,36),Vector3(2,0,29),Vector3(0.5,0,22),Vector3(3,0,16),Vector3(1,0,10),Vector3(4,0,4),Vector3(3,0,-3),Vector3(1,0,-9),Vector3(-1,0,-13)],2.5)
	route(roads,"EasternServiceRoad",[Vector3(3,0,3),Vector3(11,0,5),Vector3(18,0,3),Vector3(25,0,5),Vector3(32,0,3),Vector3(48,0,3)],2.5)
	route(roads,"WesternTimberRoad",[Vector3(2,0,1),Vector3(-6,0,0),Vector3(-13,0,2),Vector3(-18,0,1),Vector3(-20,0,-6),Vector3(-19,0,-11),Vector3(-30,0,-12),Vector3(-42,0,-11)],2.3)
	route(roads,"NorthLane",[Vector3(3,0,-8),Vector3(7,0,-12),Vector3(12,0,-11),Vector3(17,0,-13),Vector3(21,0,-11)],1.8)
	route(roads,"ForesterLane",[Vector3(-21.5,0,-15.9),Vector3(-20,0,-12),Vector3(-19,0,-8)],2.5)
	route(roads,"HerbalistDoor",[Vector3(12,0,-13.7),Vector3(12,0,-11)],2.0)
	route(roads,"GathererDoor",[Vector3(-12,0,-2.8),Vector3(-12,0,1)],2.2)
	route(roads,"CarpenterDoor",[Vector3(-19.6,0,-5),Vector3(-18.7,0,-5)],2.4)
	route(roads,"HostDoor",[Vector3(-7.4,0,12),Vector3(2,0,12)],3.0)
	route(roads,"SmithDoor",[Vector3(16.8,0,10),Vector3(14,0,10),Vector3(14,0,3)],3.2)
	route(roads,"SmallholderDoor",[Vector3(16.8,0,-6),Vector3(15,0,-6),Vector3(15,0,3)],2.5)
	route(roads,"EastFarmDoor",[Vector3(6.7,0,20),Vector3(2,0,20)],2.8)
	route(roads,"WestFarmDoor",[Vector3(-6.6,0,25),Vector3(2,0,25)],2.8)
	route(roads,"FieldPath",[Vector3(-23,0,34),Vector3(-16,0,36),Vector3(-9,0,33),Vector3(2,0,34),Vector3(10,0,33),Vector3(18,0,35),Vector3(26,0,34)],1.8)
	route(roads,"EasternFieldAccess",[Vector3(7,0,20),Vector3(7,0,28),Vector3(14.4,0,28),Vector3(14.4,0,34)],2.0)
	route(roads,"WesternFieldAccess",[Vector3(-7,0,25),Vector3(-7,0,30),Vector3(-14.4,0,30),Vector3(-14.4,0,35)],2.0)
	route(roads,"WashPath",[Vector3(14,0,10),Vector3(14,0,15),Vector3(31,0,18),Vector3(33,0,21)],1.8)
	route(roads,"ForestFootpath",[Vector3(-24.5,0,-25),Vector3(-29,0,-28),Vector3(-36,0,-26),Vector3(-38,0,-11)],1.8)
	route(roads,"HerbGardenAccess",[Vector3(15,0,-13),Vector3(17.5,0,-15),Vector3(17.5,0,-25),Vector3(15,0,-25)],1.6)
	route(roads,"PastureAccess",[Vector3(17,0,-6),Vector3(16,0,-11),Vector3(24,0,-13)],1.8)
	var homes := node(village,"Households")
	for entry in HOUSES:
		var compact_entry: Array = entry.duplicate()
		compact_entry[2] = layout_point(entry[2])
		make_house(homes,compact_entry)
	var common := node(village,"Commons")
	road_patches.append([Vector2(-0.4,-1.0),Vector2(3.8,3.1)])
	cylinder(common,"WellPlinth",1.55,0.16,layout_point(Vector3(-2,0.08,-2)),"stone",true)
	cylinder(common,"Well",0.8,0.9,layout_point(Vector3(-2,0.6,-2)),"wall",true)
	cylinder(common,"WellOpening",0.59,0.02,layout_point(Vector3(-2,1.06,-2)),"door")
	for x in [-3.15,-0.85]:
		layout_box(common,"WellPost",Vector3(0.15,2.2,0.15),Vector3(x,1.1,-2),"timber")
	layout_box(common,"WellBeam",Vector3(2.6,0.18,0.18),Vector3(-2,2.25,-2),"timber")
	for at in [Vector3(-5,0,2),Vector3(-4,0,-7),Vector3(4,0,-5)]:
		layout_box(common,"Bench",Vector3(2.3,0.45,0.45),at+Vector3(0,0.225,0),"timber",true)
	layout_box(common,"NoticeBoard",Vector3(1.5,1.2,0.12),Vector3(-5,1.4,-10),"timber")
	layout_label("公共井 · 议事与互助",Vector3(0,1.3,5),35)
	var production := node(village,"Production")
	plot(production,"WestFields",Vector3(-22,0,28),Vector2(9,7),6)
	plot(production,"EastFields",Vector3(22,0,27),Vector2(9,7),6)
	plot(production,"HerbGarden",Vector3(12,0,-25),Vector2(6,4),5)
	plot(production,"KitchenGarden",Vector3(-18.5,0,19),Vector2(4,5),4)
	road_patches.append([Vector2(-5,36)*PLAN_SCALE,Vector2(2.0,1.45)])
	layout_label("共用晒场",Vector3(-6,0.6,39),32)
	layout_label("林隙小田 / 林下生产",Vector3(21,0.7,35.8),32)
	for i in range(5):
		var log_ := cylinder(production,"Timber",0.26,4.2,layout_point(Vector3(-31,0.28+i%2*0.5,-6.4+i*0.6)),"timber")
		log_.rotation.z = PI/2
	layout_box(production,"SawBench",Vector3(3.4,0.9,0.9),Vector3(-29.5,0.45,0),"timber",true)
	layout_box(production,"ForgeApron",Vector3(6,0.03,7),Vector3(27,0.025,11),"stone")
	layout_box(production,"Forge",Vector3(1.4,1.0,1.3),Vector3(26,0.5,10),"roof",true)
	layout_box(production,"ForgeHood",Vector3(0.8,2.2,0.8),Vector3(26,1.4,9.6),"stone")
	layout_box(production,"Anvil",Vector3(0.9,0.8,0.5),Vector3(28,0.4,12),"door",true)
	layout_label("炉台 / 防火空地",Vector3(27,0.5,16),30)
	for pair in [[Vector3(24,0,-11),Vector3(31,0,-11)],[Vector3(31,0,-11),Vector3(31,0,-24)],[Vector3(31,0,-24),Vector3(22,0,-24)],[Vector3(22,0,-24),Vector3(22,0,-15)]]:
		fence(production,layout_point(pair[0]),layout_point(pair[1]))
	for i in range(3):
		layout_box(production,"Beehive",Vector3(0.7,0.8,0.7),Vector3(24+i*1.4,0.4,-22),"wall")
	layout_label("小畜围栏 · 蜂箱",Vector3(27,1,-21),32)
	village.add_child(preload("res://scripts/tools/art/forest_village_structures/structure_library.gd").new().bridge())
	layout_label("溪流 ↓ / 下游洗涤处",Vector3(40,0.5,20),32)
	layout_box(village,"WashLanding",Vector3(2.2,0.12,3),Vector3(33,0.02,21),"stone")
	layout_label("林业通道",Vector3(-35,0.5,-12),34)
	layout_label("南入口 · 通往外村",Vector3(2,0.6,40),36)
	layout_label("北 ↑ / 林地",Vector3(0,0.6,-31),34)
	box(annotations,"ScaleBar",Vector3(10,0.08,0.12),layout_point(Vector3(-31,0.08,37)),"door")
	for offset in [-5,0,5]:
		box(annotations,"ScaleTick",Vector3(0.12,0.08,0.6),layout_point(Vector3(-31,0.08,37))+Vector3(offset,0,0),"door")
	layout_label("10 米",Vector3(-31,0.6,39),32)
	preload("res://scripts/tools/art/forest_village_whitebox/front_facing_houses.gd").new().apply(village,self)
	build_forest()
	build_paths()
	preload("res://scripts/tools/art/forest_village_whitebox/river_course.gd").new().build(village)
	return village

func make_river(parent: Node) -> void:
	var width := 4.0*PLAN_SCALE
	var length := 96.0*PLAN_SCALE
	var plane := PlaneMesh.new()
	plane.size = Vector2(width,length)
	plane.subdivide_width = 11
	plane.subdivide_depth = 149
	var arrays := plane.surface_get_arrays(0)
	var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	for i in range(uv.size()):
		# The forest water shader reads UV.y as distance from shore. Fold it
		# across this narrow stream so both banks get shallow water and foam.
		uv[i] = Vector2(uv[i].y,minf(uv[i].x,1.0-uv[i].x))
	arrays[Mesh.ARRAY_TEX_UV] = uv
	var river_mesh := ArrayMesh.new()
	river_mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays)
	var source: ShaderMaterial = load("res://assets/materials/environment/forest_town/river.tres")
	var water: ShaderMaterial = source.duplicate()
	water.resource_name = "Forest river · north-south village stream"
	water.set_shader_parameter("river_width",width)
	water.set_shader_parameter("flow_direction",Vector2(0,1))
	river_mesh.surface_set_material(0,water)
	var surface := MeshInstance3D.new()
	surface.name = "Stream"
	surface.mesh = river_mesh
	surface.position = layout_point(Vector3(36,-0.25,0))
	surface.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	surface.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
	surface.set_meta("source_material",source.resource_path)
	parent.add_child(surface)

func inside_patch(at: Vector3, center: Vector3, half_size: Vector2, margin := 0.0) -> bool:
	return absf(at.x-center.x) < half_size.x+margin and absf(at.z-center.z) < half_size.y+margin

func can_plant(at: Vector3, trail_margin := 0.65) -> bool:
	# Keep the creek, actual household clearings and working areas open; all other
	# ground belongs to the woodland, including the gaps between households.
	if at.x > 25.9 and at.x < 30.4: return false
	if inside_patch(at,Vector3(-0.4,0,-1.0),Vector2(4.9,4.3)): return false
	if at.distance_to(layout_point(Vector3(2,0,29))) < 1.2: return false
	for entry in HOUSES:
		var home: Node3D = village.get_node("Households/"+entry[0])
		var relative: Vector3 = home.transform.affine_inverse()*at
		var size: Vector3 = entry[3]
		if absf(relative.x) < size.x*0.5+0.85 and relative.z > -size.z*0.5-0.85 and relative.z < size.z*0.5+3.15:
			return false
	# Chief's shared store sits behind the house.
	if inside_patch(at,layout_point(Vector3(-1,0,-18))+Vector3(-1.7,0,-4.1),Vector2(2.1,2.0)): return false
	for garden in forest_gardens:
		if inside_patch(at,layout_point(garden[0]),garden[1]*PLAN_SCALE*0.5,0.55): return false
	for patch in [[Vector3(27,0,11),Vector2(6,7)],[Vector3(26.5,0,-17.5),Vector2(9,13)],[Vector3(-5,0,36),Vector2(6,4)],[Vector3(-31,0,-4),Vector2(8,9)],[Vector3(33,0,21),Vector2(3,4)]]:
		if inside_patch(at,layout_point(patch[0]),patch[1]*PLAN_SCALE*0.5,0.7): return false
	for run in planting_trails:
		var a: Vector3 = run[0]
		var delta: Vector3 = run[1]-a
		var closest := a+delta*clampf((at-a).dot(delta)/delta.length_squared(),0.0,1.0)
		if at.distance_to(closest) < run[2]*0.5+trail_margin: return false
	return true

func build_forest() -> void:
	var forest := node(village,"Forest")
	var inner := node(forest,"BetweenHouseholds")
	var outer := node(forest,"ContinuousWoodland")
	var understory := node(forest,"Understory")
	var rng := RandomNumberGenerator.new()
	rng.seed = 20261025
	for ix in range(22):
		for iz in range(21):
			var at := layout_point(Vector3(-51+ix*4.9+rng.randf_range(-1.4,1.4),0,-47+iz*4.7+rng.randf_range(-1.4,1.4)))
			if not can_plant(at): continue
			var too_close := false
			for planted in tree_points:
				if planted.distance_to(at) < 2.6:
					too_close = true
					break
			if too_close: continue
			tree_points.append(at)
			var interior := at.x > -24 and at.x < 25 and at.z > -24 and at.z < 29
			var height := rng.randf_range(12.0,18.0)
			var tree := make_tree(inner if interior else outer,"Tree_%03d" % tree_points.size(),at,height,rng.randf_range(2.8,4.0))
			if interior: tree.add_to_group("village_tree",true)
			for i in range(rng.randi_range(1,3)):
				var angle := rng.randf_range(0,TAU)
				var shrub_at := at+Vector3(cos(angle),0,sin(angle))*rng.randf_range(1.0,2.1)
				if not can_plant(shrub_at,0.3): continue
				var shrub_radius := rng.randf_range(0.6,1.0)
				var shrub_height := rng.randf_range(0.7,1.3)
				make_shrub(understory,"Shrub_%03d" % understory.get_child_count(),shrub_at,shrub_radius,shrub_height)
	village.set_meta("forest_tree_count",tree_points.size())
	print("FOREST STRUCTURE: ",tree_points.size()," mature trees; ",inner.get_child_count()," between households; ",understory.get_child_count()," understory clumps")

func make_tree(parent: Node, name_: String, at: Vector3, height: float, _radius: float) -> Node3D:
	var index := tree_points.size()-1
	var tree := forest_model(parent,name_,TREE_MODELS[index%TREE_MODELS.size()],at)
	var model: MeshInstance3D = tree.get_node("Model")
	var bounds := model.get_aabb()
	var uniform_scale := height/bounds.size.y
	tree.scale = Vector3.ONE*uniform_scale
	tree.rotation.y = fmod(float(index)*2.399963,TAU)
	tree.add_to_group("forest_tree",true)
	tree.add_to_group("camera_occluder",true)
	tree.add_to_group("camera_near_fade",true)
	return tree

func make_shrub(parent: Node, name_: String, at: Vector3, radius: float, height: float) -> void:
	var index := parent.get_child_count()
	var shrub := forest_model(parent,name_,SHRUB_MODELS[index%SHRUB_MODELS.size()],at)
	var model: MeshInstance3D = shrub.get_node("Model")
	var bounds := model.get_aabb()
	var uniform_scale := minf(radius*2.0/maxf(bounds.size.x,bounds.size.z),height/bounds.size.y)
	shrub.scale = Vector3.ONE*uniform_scale*.68
	shrub.rotation.y = fmod(float(index)*2.399963,TAU)
	shrub.add_to_group("forest_shrub",true)

func forest_model(parent: Node, name_: String, key: String, at: Vector3) -> Node3D:
	if not forest_scenes.has(key):
		forest_scenes[key] = load(FOREST_MODEL_ROOT+key+".tscn")
	var instance: Node3D = (forest_scenes[key] as PackedScene).instantiate()
	instance.name = name_
	instance.position = at
	parent.add_child(instance)
	return instance

func own_children(parent: Node, owner_: Node) -> void:
	for child in parent.get_children():
		child.owner = owner_
		if child.scene_file_path.is_empty(): own_children(child,owner_)

func save_scene(root_: Node, path: String) -> void:
	own_children(root_,root_)
	DirAccess.make_dir_recursive_absolute(path.get_base_dir())
	var packed := PackedScene.new()
	assert(packed.pack(root_) == OK)
	assert(ResourceSaver.save(packed,path) == OK)

func add_component(host: Node, path: String) -> Resource:
	var component: Resource = load(path).new()
	var components: Array = host.get("Components")
	components.append(component)
	host.set("Components",components)
	return component

func hud_label(parent: Node, name_: String, text_: String, pos: Vector2, font_size: int) -> Label:
	var result := Label.new()
	result.name = name_
	result.text = text_
	result.position = pos
	result.add_theme_font_size_override("font_size",font_size)
	result.add_theme_color_override("font_color",Color(0.13,0.18,0.18))
	result.add_theme_color_override("font_outline_color",Color(1,1,1,0.9))
	result.add_theme_constant_override("outline_size",4)
	parent.add_child(result)
	return result

func build() -> void:
	material("wall",Color(0.89,0.88,0.83)); material("roof",Color(0.46,0.49,0.48))
	material("stone",Color(0.64,0.65,0.61)); material("timber",Color(0.52,0.53,0.47))
	material("door",Color(0.24,0.28,0.27)); material("window",Color(0.38,0.43,0.43))
	prepare_ground()
	material("soil",Color(0.46,0.47,0.38))
	material("crop",Color(0.53,0.58,0.45))
	var environment_scene := build_environment()
	preload("res://scripts/tools/art/forest_village_whitebox/ground_flora.gd").new().build(environment_scene)
	save_scene(environment_scene,ENV_PATH)
	environment_scene.free()
	var level: Node3D = load("res://scripts/gamelogic/gameobject/GameObject3D.cs").new()
	level.name = "ForestVillageWhitebox"
	level.editor_description = "F1总览 / F2平面 / F3步行 / L标注 / R回入口。10户独立白模。按设计假设组织家庭兼业、公共水井、短巷和生产通路。"
	var environment_instance: Node3D = load(ENV_PATH).instantiate()
	environment_instance.name = "Village"
	level.add_child(environment_instance)
	var environment := WorldEnvironment.new()
	environment.name = "WorldEnvironment"
	environment.environment = Environment.new()
	environment.environment.background_mode = Environment.BG_COLOR
	environment.environment.background_color = Color(0.32,0.40,0.34)
	environment.environment.background_intensity = 2.0
	environment.environment.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	environment.environment.ambient_light_color = Color(0.86,0.91,1.0)
	environment.environment.ambient_light_energy = 0.45
	environment.environment.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	environment.environment.ssao_enabled = true
	environment.environment.ssao_radius = 1.5
	environment.environment.ssao_intensity = 1.5
	level.add_child(environment)
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-48,-32,0)
	sun.light_intensity_lux = 2.8
	sun.light_color = Color(1,0.96,0.87)
	sun.shadow_enabled = true
	sun.directional_shadow_max_distance = 180
	sun.shadow_blur = 1.0
	level.add_child(sun)
	var player: Node3D = load("res://assets/scenes/player.tscn").instantiate()
	player.name = "Player"
	player.position = layout_point(Vector3(2,0.55,29))
	level.add_child(player)
	var spawn := Marker3D.new()
	spawn.name = "Spawn"
	spawn.position = player.position
	level.add_child(spawn)
	var rig: Node3D = load("res://scripts/gamelogic/gameobject/GameObject3D.cs").new()
	rig.name = "WalkRig"
	level.add_child(rig)
	var walk := Camera3D.new()
	walk.name = "Camera3D"
	walk.fov = 45
	walk.far = 204
	walk.rotation_degrees.x = -20
	walk.position = player.position + Vector3(0,3,8)
	walk.projection = Camera3D.PROJECTION_PERSPECTIVE
	walk.size = 5.8
	walk.near = .001
	rig.add_child(walk)
	var controller := add_component(rig,"res://scripts/gamelogic/camera/SceneCameraComponent3D.cs")
	controller.resource_local_to_scene = true
	controller.set("Profile",load("res://assets/resources/camera/hd2d_free_3d.tres"))
	controller.set("FramingOriginPath",NodePath("../Spawn"))
	controller.set("UseComponentFraming",true)
	controller.set("CameraOffset",Vector3(0,3,8))
	controller.set("CameraRotationDegrees",Vector3(-20,0,0))
	var occlusion := add_component(rig,"res://scripts/gamelogic/camera/CameraOcclusionComponent3D.cs")
	occlusion.resource_local_to_scene = true
	occlusion.set("OccluderRootPath",NodePath("../Village"))
	occlusion.set("OccludedTransparency",1.0)
	occlusion.set("FadeSeconds",.25)
	occlusion.set("RestoreDelay",.25)
	for spec in [["OverviewCamera",Vector3(43,58,68),68.0],["PlanCamera",Vector3(0,85,1),102.0]]:
		var camera := Camera3D.new()
		camera.name = spec[0]
		camera.projection = Camera3D.PROJECTION_ORTHOGONAL
		camera.size = spec[2] * PLAN_SCALE
		camera.far = 180
		camera.position = layout_point(spec[1])
		if camera.name == "PlanCamera": camera.rotation_degrees.x = -90
		else: camera.basis = Basis.looking_at(layout_point(Vector3(-1,0,1))-camera.position)
		level.add_child(camera)
	var hud := CanvasLayer.new()
	hud.name = "HUD"
	level.add_child(hud)
	hud_label(hud,"Title","林中聚落 / 生活型白模",Vector2(28,22),30)
	hud_label(hud,"Subtitle","连续森林 · 10 户林隙住宅 · 穿林小径",Vector2(30,65),17)
	hud_label(hud,"ModeLabel","村落总览",Vector2(30,94),15)
	var help := hud_label(hud,"Help","F1 总览    F2 平面    F3 步行    L 标注    R 回入口    |    WASD 移动 · 空格跳跃",Vector2.ZERO,16)
	help.anchor_top = 1
	help.anchor_bottom = 1
	help.offset_left = 28
	help.offset_top = -42
	help.offset_bottom = -15
	add_component(level,"res://scripts/gamelogic/camera/BlockoutPreviewComponent3D.cs")
	save_scene(level,LEVEL_PATH)
	level.free()
	print("FOREST_VILLAGE_WHITEBOX_BUILT: 10 households; ",LEVEL_PATH)
	quit()
