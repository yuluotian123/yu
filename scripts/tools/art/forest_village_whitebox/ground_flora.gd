extends RefCounted
## Authored native geometry, baked offline into culled patches; no physics/scripts.

const MODEL_ROOT := "res://assets/models/environment/forest_village/ground_flora/"
const MATERIAL_ROOT := "res://assets/materials/environment/forest_village/ground_flora/"
const CELL := 6.0
const GRASS_CELL := 4.0
const GRASS_STEP := 0.27
const GRASS_EXTENT := Vector2(39.68,36.16)
const KINDS := ["grass","fern","rosette","flowers"]
const RECT := Vector4(-40.56,-37.44,81.12,74.88)
var rng := RandomNumberGenerator.new()
var noise := FastNoiseLite.new()
var ground: Image
var meadow_mask: Image
var obstacles: Dictionary = {}
var doors: Array[Vector2] = []
var plots: Array[Rect2] = []
var chunks: Dictionary = {}
var variants: Dictionary = {}
var template_arrays: Dictionary = {}
var materials: Dictionary = {}
var counts := {"grass":0,"fern":0,"rosette":0,"flowers":0}
var tree_centers: Array[Vector2] = []
var grass_patches: Dictionary = {}
var saver := preload("res://scripts/tools/art/forest_town/material_palette.gd").new()

func relative(node: Node3D, root: Node3D) -> Transform3D:
	var value := node.transform
	var parent := node.get_parent()
	while parent != root:
		value = (parent as Node3D).transform * value
		parent = parent.get_parent()
	return value

func tone(hex: String) -> Color:
	return Color(hex).srgb_to_linear()

func save_mesh(mesh: Resource, path: String) -> void:
	var uid := ResourceLoader.get_resource_uid(path) if FileAccess.file_exists(path) else ResourceUID.create_id()
	assert(ResourceSaver.save(mesh,path,ResourceSaver.FLAG_COMPRESS) == OK)
	assert(ResourceSaver.set_uid(path,uid) == OK)
	mesh.take_over_path(path)

func triangle(tool: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, colors: Array[Color], heights: Vector3) -> void:
	var normal := ((b-a).cross(c-a).normalized()*0.65+Vector3.UP*0.35).normalized()
	var points := [a,b,c]
	for i in [0,2,1]:
		tool.set_normal(normal)
		tool.set_color(colors[i])
		tool.set_uv(Vector2(float(i)*0.5,heights[i]))
		tool.add_vertex(points[i])

func blade(tool: SurfaceTool, base: Vector3, angle: float, height: float, width: float, lean: float, palette: Array[Color], wind := 1.0) -> void:
	var forward := Vector3(cos(angle),0,sin(angle))
	var side := Vector3(-forward.z,0,forward.x)
	var sections: Array = []
	for t: float in [0.0,0.38,0.75,1.0]:
		var center := base+Vector3.UP*height*(t-0.13*t*t)+forward*lean*t*t
		var half_width := width*(1.0-t)*0.5
		sections.append([center-side*half_width,center+Vector3.UP*width*0.18,center+side*half_width])
	for j in range(3):
		var t0: float = [0.0,0.38,0.75][j]
		var t1: float = [0.38,0.75,1.0][j]
		var color0 := palette[0].lerp(palette[1],smoothstep(0.0,0.9,t0))
		var color1 := palette[0].lerp(palette[1],smoothstep(0.0,0.9,t1))
		var a: Array = sections[j]
		var b: Array = sections[j+1]
		for k in range(2):
			var shade := 0.88 if k == 0 else 1.05
			triangle(tool,a[k],b[k],a[k+1],[color0*shade,color1*shade,color0*shade],Vector3(t0,t1,t0)*wind)
			if j < 2:
				triangle(tool,a[k+1],b[k],b[k+1],[color0*shade,color1*shade,color1*shade],Vector3(t0,t1,t1)*wind)

func leaf(tool: SurfaceTool, base: Vector3, tip: Vector3, width: float, color: Color, wind: float) -> void:
	var along := tip-base
	var side := Vector3(-along.z,0,along.x).normalized()*width
	var middle := base.lerp(tip,0.48)
	var ridge := middle+Vector3.UP*width*0.22
	var left := middle-side
	var right := middle+side
	triangle(tool,base,left,ridge,[color*0.75,color*0.9,color],Vector3(wind*0.65,wind,wind))
	triangle(tool,left,tip,ridge,[color*0.9,color*1.05,color],Vector3(wind,wind,wind))
	triangle(tool,base,ridge,right,[color*0.75,color,color*1.08],Vector3(wind*0.65,wind,wind))
	triangle(tool,ridge,tip,right,[color,color*1.05,color*1.08],Vector3(wind,wind,wind))

func make_template(kind: String, index: int) -> ArrayMesh:
	rng.seed = 782309+index*739+KINDS.find(kind)*17179
	if kind == "grass": return make_grass_template(index)
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	tool.set_material(materials[kind])
	if kind == "fern":
		for i in range(8):
			var angle := float(i)*TAU/8.0+rng.randf_range(-0.18,0.18)
			var direction := Vector3(cos(angle),0,sin(angle))
			var side := Vector3(-direction.z,0,direction.x)
			var reach := rng.randf_range(0.35,0.57)
			var rise := rng.randf_range(0.21,0.38)
			var previous := Vector3.ZERO
			for j in range(13):
				var t := float(j)/12.0
				var center := direction*reach*t+Vector3.UP*(0.06+rise*sin(t*PI*0.7))
				var width := 0.004*(1.0-t)+0.001
				var stem := tone("6e8740")
				triangle(tool,previous-side*width,center-side*width,previous+side*width,[stem,stem,stem],Vector3(t,t,t)*0.7)
				triangle(tool,previous+side*width,center-side*width,center+side*width,[stem,stem,stem],Vector3(t,t,t)*0.7)
				previous = center
			for j in range(1,12):
				var t := float(j)/12.0
				var center := direction*reach*t+Vector3.UP*(0.06+rise*sin(t*PI*0.7))
				var length := reach*0.29*pow(sin(t*PI),0.7)
				for sign_: float in [-1.0,1.0]:
					var tip := center+side*length*sign_+direction*length*0.42+Vector3.DOWN*length*0.16
					var color := tone("42653a").lerp(tone("859d4b"),t*0.75+rng.randf()*0.15)
					leaf(tool,center,tip,length*0.27,color,t*0.7)
	elif kind == "rosette":
		for i in range(10):
			var angle := i*2.39996+index*0.4
			var length := rng.randf_range(0.17,0.30)
			var tip := Vector3(cos(angle)*length,rng.randf_range(0.07,0.18),sin(angle)*length)
			var color := tone("3d6335").lerp(tone("7d954c"),rng.randf_range(0.2,0.75))
			leaf(tool,Vector3(0,0.015,0),tip,length*0.31,color,0.4)
	else:
		for i in range(5):
			var angle := rng.randf()*TAU
			var base := Vector3(cos(angle),0,sin(angle))*rng.randf_range(0.02,0.15)
			var height := rng.randf_range(0.20,0.38)
			var center := base+Vector3(cos(angle)*0.035,height,sin(angle)*0.035)
			blade(tool,base,angle,height/0.87,0.009,0.035,[tone("40572d"),tone("7f9343")],0.8)
			leaf(tool,base+Vector3.UP*0.08,base+Vector3(cos(angle)*0.12,0.17,sin(angle)*0.12),0.025,tone("62813c"),0.5)
			var petal := tone("e5dfbd") if index%4 != 0 else tone("dfbf62")
			for k in range(6):
				var a := float(k)*TAU/6.0
				var radius := rng.randf_range(0.029,0.042)
				leaf(tool,center,center+Vector3(cos(a)*radius,0.006,sin(a)*radius),radius*0.36,petal,0.9)
			for k in range(6):
				var a := float(k)*TAU/6.0
				var b := float(k+1)*TAU/6.0
				triangle(tool,center+Vector3.UP*0.008,center+Vector3(cos(a)*0.009,0.007,sin(a)*0.009),center+Vector3(cos(b)*0.009,0.007,sin(b)*0.009),[tone("c49b34"),tone("c49b34"),tone("c49b34")],Vector3.ONE*0.9)
	tool.index()
	var mesh := tool.commit()
	mesh.resource_name = "Forest flora · %s %d" % [kind,index]
	var arrays := mesh.surface_get_arrays(0)
	# Non-instanced plants are merged into patches; their templates stay in memory.
	template_arrays[mesh] = arrays
	return mesh

func make_grass_template(index: int) -> ArrayMesh:
	var mesh := preload("res://scripts/tools/art/forest_village_whitebox/grass_cards.gd").new().mesh(index,materials.grass) as ArrayMesh
	mesh.resource_name = "Instanced grass tuft · "+str(index)
	save_mesh(mesh,MODEL_ROOT+"grass_%02d.mesh.res" % index)
	return mesh

func prepare_materials() -> void:
	DirAccess.make_dir_recursive_absolute(MATERIAL_ROOT)
	DirAccess.make_dir_recursive_absolute(MODEL_ROOT)
	for kind: String in KINDS:
		var material := ShaderMaterial.new()
		material.resource_name = "Forest floor · painted "+kind
		material.shader = load("res://assets/shaders/town/plant_painterly.gdshader")
		material.set_shader_parameter("palette_strength",0.0)
		material.set_shader_parameter("color_saturation",1.0)
		material.set_shader_parameter("albedo_value",1.0)
		material.set_shader_parameter("shadow_tint",Vector3(0.74,0.82,0.66))
		material.set_shader_parameter("light_wrap",0.22)
		material.set_shader_parameter("painterly_blend",0.8)
		material.set_shader_parameter("wind_strength",0.038 if kind == "grass" else 0.016)
		material.set_shader_parameter("wind_flutter",0.009 if kind == "grass" else 0.0)
		material.set_shader_parameter("grass_normal_blend",0.72 if kind == "grass" else 0.0)
		material.set_shader_parameter("weather_puddles_enabled",false)
		if kind == "grass":
			material.shader = load("res://assets/shaders/town/grass_tuft.gdshader")
			material.set_shader_parameter("grass_atlas",load("res://assets/textures/environment/forest_village/character_style/grass.res"))
			material.set_shader_parameter("directional_shadow_lift",0.23)
			material.set_shader_parameter("orthographic_camera_pullback",4.0)
		saver.apply_surface_response(material,"grass" if kind == "grass" else "plant")
		saver.save_resource(material,MATERIAL_ROOT+kind+".tres")
		materials[kind] = material
		variants[kind] = []
		for i in range(6): variants[kind].append(make_template(kind,i))

func ground_color(at: Vector2) -> Color:
	var uv := (at-Vector2(RECT.x,RECT.y))/Vector2(RECT.z,RECT.w)
	var p := Vector2i(uv*Vector2(ground.get_size())).clamp(Vector2i.ZERO,ground.get_size()-Vector2i.ONE)
	return ground.get_pixelv(p)

func add_obstacle(rect: Rect2) -> void:
	for x in range(floori(rect.position.x/3.0),ceili(rect.end.x/3.0)+1):
		for z in range(floori(rect.position.y/3.0),ceili(rect.end.y/3.0)+1):
			var key := Vector2i(x,z)
			if not obstacles.has(key): obstacles[key] = []
			obstacles[key].append(rect)

func collect(village: Node3D) -> RefCounted:
	var paving := preload("res://scripts/tools/art/forest_village_whitebox/flagstone_paving.gd").new()
	var surface: MeshInstance3D = village.get_node("Paths/Surface")
	var arrays := surface.mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	for i in vertices.size(): paving.wear[Vector2i(roundi(vertices[i].x/paving.GRID),roundi(vertices[i].z/paving.GRID))] = colors[i].a
	for body: CollisionShape3D in village.find_children("*","CollisionShape3D",true,false):
		if body.disabled or body.shape == null: continue
		var shape := body.shape.get_debug_mesh().get_aabb()
		var bounds := relative(body,village)*shape
		if bounds.end.y < 0.08 or bounds.position.y > 1.5: continue
		add_obstacle(Rect2(Vector2(bounds.position.x,bounds.position.z),Vector2(bounds.size.x,bounds.size.z)).grow(0.16))
	for household in village.get_node("Households").get_children():
		var door: Node3D = household.get_node("DoorApproach")
		var at := relative(door,village).origin
		doors.append(Vector2(at.x,at.z))
	for name_: String in ["WestFields","EastFields","HerbGarden","KitchenGarden"]:
		var plot: Node3D = village.get_node("Production/"+name_)
		var bounds := AABB()
		var first := true
		for model: MeshInstance3D in plot.find_children("*","MeshInstance3D",true,false):
			var candidate := relative(model,village)*model.get_aabb()
			bounds = candidate if first else bounds.merge(candidate)
			first = false
		if not first: plots.append(Rect2(Vector2(bounds.position.x,bounds.position.z),Vector2(bounds.size.x,bounds.size.z)).grow(0.25))
	for path: String in ["Production/ForgeApron","WashLanding"]:
		var model := village.get_node_or_null(path) as MeshInstance3D
		if model != null:
			var bounds := relative(model,village)*model.get_aabb()
			plots.append(Rect2(Vector2(bounds.position.x,bounds.position.z),Vector2(bounds.size.x,bounds.size.z)).grow(0.12))
	for tree: Node3D in village.get_node("Forest").find_children("Tree_*","Node3D",true,false):
		var at := relative(tree,village).origin
		tree_centers.append(Vector2(at.x,at.z))
	return paving

func allowed(at: Vector2, margin: float, paving: RefCounted) -> bool:
	if at.x > 25.2 and at.x < 31.0: return false
	if paving.coverage(at) > 0.32: return false
	var key := Vector2i(floori(at.x/3.0),floori(at.y/3.0))
	for rect: Rect2 in obstacles.get(key,[]):
		if rect.grow(margin).has_point(at): return false
	for door in doors:
		if at.distance_squared_to(door) < pow(0.85+margin,2.0): return false
	for plot in plots:
		if plot.grow(margin).has_point(at): return false
	return true

func plant(kind: String, at: Vector2, scale_: float, palette_mix := 0.0) -> void:
	if kind == "grass":
		plant_grass(at,scale_)
		return
	var key := "%s_%d_%d" % [kind,floori(at.x/CELL),floori(at.y/CELL)]
	if not chunks.has(key): chunks[key] = {"vertices":PackedVector3Array(),"normals":PackedVector3Array(),"colors":PackedColorArray(),"uv":PackedVector2Array(),"indices":PackedInt32Array(),"lod1":PackedInt32Array(),"lod2":PackedInt32Array(),"kind":kind,"plants":0}
	var chunk: Dictionary = chunks[key]
	var mesh: ArrayMesh = variants[kind][rng.randi()%variants[kind].size()]
	var arrays: Array = template_arrays[mesh]
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
	var basis := Basis(Vector3.UP,rng.randf_range(0,TAU))
	var tint := rng.randf_range(0.87,1.10)
	var terrain := ground_color(at)
	var offset: int = chunk.vertices.size()
	for i in vertices.size():
		chunk.vertices.append(basis*vertices[i]*scale_+Vector3(at.x,0.005,at.y))
		chunk.normals.append(basis*normals[i])
		var color := colors[i]*tint
		color = color.lerp(terrain,palette_mix)
		color.a = 1.0
		chunk.colors.append(color)
		chunk.uv.append(uv[i])
	var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
	for index in indices:
		chunk.indices.append(index+offset)
		if int(chunk.plants)%2 == 0: chunk.lod1.append(index+offset)
		if int(chunk.plants)%4 == 0: chunk.lod2.append(index+offset)
	chunk.plants += 1
	counts[kind] += 1

func plant_grass(at: Vector2, width_scale: float, height_scale := -1.0) -> void:
	var cell := Vector2i(floori(at.x/GRASS_CELL),floori(at.y/GRASS_CELL))
	# Keep variant selection independent of the following rotation/tint samples.
	rng.randi()
	if height_scale < 0.0: height_scale = width_scale
	var basis := Basis(Vector3.UP,rng.randf_range(0,TAU)).scaled(Vector3(width_scale,height_scale,width_scale))
	var patch := noise.get_noise_2dv(at*.37)*.5+.5
	var tint := lerpf(.90,1.08,patch)+rng.randf_range(-.025,.025)
	# Same eight-metre pigment tile as the terrain. Leave the quiet bands exposed.
	# Decide after consuming the legacy RNG samples to preserve the other plant sites.
	var mask_uv := (at/8.0).posmod(1.0)
	var density := meadow_mask.get_pixelv(Vector2i(mask_uv*Vector2(meadow_mask.get_size()))).r
	var terrain := ground_color(at)
	var site_hash := hash(Vector2i(roundi(at.x*100),roundi(at.y*100)))%10000
	if site_hash*0.0001 >= (0.018+0.63*pow(density,2.8))*lerpf(.22,1.0,terrain.a): return
	basis = basis.scaled(Vector3(0.90,lerpf(0.34,0.68,density)*lerpf(.34,1.0,terrain.a),0.90))
	if not grass_patches.has(cell): grass_patches[cell] = []
	var origin := Vector3(cell.x*GRASS_CELL,0,cell.y*GRASS_CELL)
	grass_patches[cell].append({"transform":Transform3D(basis,Vector3(at.x,.005,at.y)-origin),"data":Color(terrain.r,terrain.g,terrain.b,tint)})
	counts.grass += 1

func build(village: Node3D) -> void:
	var previous := village.get_node_or_null("GroundFlora")
	if previous != null: previous.free()
	ground = (load("res://assets/textures/environment/forest_village/ground_contact.res") as Texture2D).get_image()
	meadow_mask = Image.load_from_file("res://assets/textures/environment/forest_village/character_style/meadow_mask.png")
	var paving := collect(village)
	prepare_materials()
	rng.seed = 2026102607
	noise.seed = 39173
	noise.frequency = 0.33
	noise.fractal_octaves = 3
	# Overlapping knee-high meadow patches, with short grass at the stone verge.
	var extent := Vector2i(ceili(GRASS_EXTENT.x/GRASS_STEP),ceili(GRASS_EXTENT.y/GRASS_STEP))
	for x in range(-extent.x,extent.x):
		for z in range(-extent.y,extent.y):
			var at := Vector2(x,z)*GRASS_STEP+Vector2(rng.randf_range(-0.11,0.11),rng.randf_range(-0.11,0.11))
			if absf(at.x) > GRASS_EXTENT.x or absf(at.y) > GRASS_EXTENT.y: continue
			var growth := noise.get_noise_2dv(at)*0.5+0.5
			if rng.randf() > 0.76+smoothstep(0.26,0.71,growth)*0.22: continue
			if not allowed(at,0.02,paving): continue
			var fringe := 1.0-smoothstep(0.02,0.32,paving.coverage(at))
			var tall_patch := smoothstep(0.55,0.69,noise.get_noise_2dv(at*0.43)*0.5+0.5)
			var width_scale := rng.randf_range(0.88,1.10)*lerpf(0.92,1.08,growth)*lerpf(0.70,1.0,fringe)
			var height_scale := rng.randf_range(0.85,1.13)*lerpf(0.90,1.13,growth)*lerpf(1.0,1.30,tall_patch*fringe)*lerpf(0.35,1.0,fringe)
			plant_grass(at,width_scale,height_scale)
	# Ferns and broad leaves collect around roots, with a loose flower fringe.
	for center in tree_centers:
		for i in range(8):
			var angle := rng.randf()*TAU
			var at := center+Vector2(cos(angle),sin(angle))*rng.randf_range(1.0,3.1)
			var kind: String = "fern" if i < 3 else ("rosette" if i < 6 else "flowers")
			if not allowed(at,0.24,paving): continue
			plant(kind,at,rng.randf_range(0.75,1.25),0.08)
	for i in range(1800):
		var at := Vector2(rng.randf_range(-33,34),rng.randf_range(-32,34))
		if not allowed(at,0.2,paving): continue
		var kind: String = "flowers" if i%4 == 0 else "rosette"
		if noise.get_noise_2dv(at*0.7) > 0.05: plant(kind,at,rng.randf_range(0.8,1.15),0.07)
	# White and ochre flowers punctuate the verge, never the stone tread itself.
	for x in range(-36,37):
		for z in range(-33,34):
			var at := Vector2(x,z)+Vector2(rng.randf_range(-0.3,0.3),rng.randf_range(-0.3,0.3))
			if rng.randf() > 0.34 or not allowed(at,0.18,paving): continue
			var verge := false
			for delta: Vector2 in [Vector2(.75,0),Vector2(-.75,0),Vector2(0,.75),Vector2(0,-.75)]:
				if paving.coverage(at+delta) > .55: verge = true
			if verge: plant("flowers",at,rng.randf_range(.8,1.15),.04)
	var flora := Node3D.new()
	flora.name = "GroundFlora"
	flora.set_meta("decorative_only",true)
	flora.set_meta("plant_counts",counts)
	flora.set_meta("placement_seed",2026102607)
	flora.set_meta("revision",4)
	flora.set_meta("ddgi_skip_subtree",true)
	flora.set_meta("grass_step_m",GRASS_STEP)
	flora.set_meta("grass_card_height_m",0.60)
	flora.set_meta("grass_lod_keeps_clump_sites",true)
	village.add_child(flora)
	var triangles := 0
	var generated := {}
	for cell: Vector2i in grass_patches:
		var plants: Array = grass_patches[cell]
		var multimesh := MultiMesh.new()
		multimesh.transform_format = MultiMesh.TRANSFORM_3D
		multimesh.use_custom_data = true
		multimesh.mesh = variants.grass[posmod(cell.x*17+cell.y*31,6)]
		multimesh.instance_count = plants.size()
		var bounds := AABB()
		var buffer := PackedFloat32Array()
		for i in plants.size():
			var transform: Transform3D = plants[i].transform
			var data: Color = plants[i].data
			# Bulk buffers also serialize correctly with Godot's headless dummy renderer.
			# Its per-instance setters are no-ops, which would save empty instances.
			buffer.append_array(PackedFloat32Array([
				transform.basis.x.x,transform.basis.y.x,transform.basis.z.x,transform.origin.x,
				transform.basis.x.y,transform.basis.y.y,transform.basis.z.y,transform.origin.y,
				transform.basis.x.z,transform.basis.y.z,transform.basis.z.z,transform.origin.z,
				data.r,data.g,data.b,data.a]))
			var plant_bounds: AABB = plants[i].transform*multimesh.mesh.get_aabb()
			bounds = plant_bounds if i == 0 else bounds.merge(plant_bounds)
		multimesh.buffer = buffer
		assert(multimesh.buffer.size() == plants.size()*16)
		multimesh.custom_aabb = bounds.grow(.12)
		var file := "patch_grass_%d_%d.multimesh.res" % [cell.x,cell.y]
		save_mesh(multimesh,MODEL_ROOT+file)
		generated[file] = true
		var instance := MultiMeshInstance3D.new()
		instance.name = "Grass_%d_%d" % [cell.x,cell.y]
		instance.position = Vector3(cell.x*GRASS_CELL,0,cell.y*GRASS_CELL)
		instance.multimesh = multimesh
		instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		instance.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
		instance.visibility_range_end = 44.0
		instance.lod_bias = .25
		instance.set_meta("ddgi_contribute",false)
		instance.set_meta("plant_kind","grass")
		instance.set_meta("plant_count",plants.size())
		flora.add_child(instance)
		triangles += plants.size()*12
	for key: String in chunks:
		var chunk: Dictionary = chunks[key]
		var arrays: Array = []
		arrays.resize(Mesh.ARRAY_MAX)
		arrays[Mesh.ARRAY_VERTEX] = chunk.vertices
		arrays[Mesh.ARRAY_NORMAL] = chunk.normals
		arrays[Mesh.ARRAY_COLOR] = chunk.colors
		arrays[Mesh.ARRAY_TEX_UV] = chunk.uv
		arrays[Mesh.ARRAY_INDEX] = chunk.indices
		var mesh := ArrayMesh.new()
		var lods := {0.045:chunk.lod1,0.14:chunk.lod2} if int(chunk.plants) >= 8 else {}
		mesh.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays,[],lods,Mesh.ARRAY_FLAG_COMPRESS_ATTRIBUTES)
		mesh.surface_set_material(0,materials[chunk.kind])
		mesh.resource_name = "Forest ground flora · "+key
		var file := "patch_"+key+".mesh.res"
		save_mesh(mesh,MODEL_ROOT+file)
		generated[file] = true
		var instance := MeshInstance3D.new()
		instance.name = key.to_pascal_case().replace("-","N")
		instance.mesh = mesh
		instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF if chunk.kind == "grass" else GeometryInstance3D.SHADOW_CASTING_SETTING_ON
		instance.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
		instance.extra_cull_margin = 0.12
		if chunk.kind == "grass": instance.lod_bias = 0.25
		instance.set_meta("ddgi_contribute",false)
		instance.set_meta("plant_kind",chunk.kind)
		instance.set_meta("plant_count",chunk.plants)
		flora.add_child(instance)
		triangles += chunk.indices.size()/3
	flora.set_meta("lod0_triangles",triangles)
	# Only retire our own obsolete generated patches after a successful rebuild.
	var directory := DirAccess.open(MODEL_ROOT)
	for file in directory.get_files():
		if file.begins_with("patch_") and (file.ends_with(".mesh.res") or file.ends_with(".multimesh.res")) and not generated.has(file):
			assert(directory.remove(file) == OK)
	print("GROUND_FLORA: ",counts,"; ",flora.get_child_count()," culled patches (",grass_patches.size()," instanced grass); ",triangles," LOD0 triangles; native wind/weather/GI receivers")
