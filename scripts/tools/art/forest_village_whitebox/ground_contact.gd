extends RefCounted
## Offline terrain matching and sparse contact vegetation; no runtime scripts/physics.

const MAP_PATH := "res://assets/textures/environment/forest_village/ground_contact.res"
const RECT := Vector4(-40.56,-37.44,81.12,74.88)
const RESOLUTION := Vector2i(640,592)
var ground_image: Image
var ground_map: ImageTexture
var atlas: Image
var patches: SurfaceTool
var grass_chunks: Dictionary = {}
var blade_material: ShaderMaterial
var tufts := 0
var roots := 0
var rng := RandomNumberGenerator.new()
var river := preload("res://scripts/tools/art/forest_village_whitebox/river_course.gd").new()

func relative_transform(node: Node3D, root: Node3D) -> Transform3D:
	var result := node.transform
	var parent := node.get_parent()
	while parent != root:
		result = (parent as Node3D).transform * result
		parent = parent.get_parent()
	return result

func atlas_color(uv: Vector2) -> Color:
	var pixel := uv * Vector2(atlas.get_size()) - Vector2(0.5,0.5)
	var cell := Vector2i(floori(pixel.x),floori(pixel.y))
	var f := pixel-Vector2(cell)
	var size := atlas.get_size()
	var a := atlas.get_pixel(posmod(cell.x,size.x),posmod(cell.y,size.y)).srgb_to_linear()
	var b := atlas.get_pixel(posmod(cell.x+1,size.x),posmod(cell.y,size.y)).srgb_to_linear()
	var c := atlas.get_pixel(posmod(cell.x,size.x),posmod(cell.y+1,size.y)).srgb_to_linear()
	var d := atlas.get_pixel(posmod(cell.x+1,size.x),posmod(cell.y+1,size.y)).srgb_to_linear()
	return a.lerp(b,f.x).lerp(c.lerp(d,f.x),f.y)

func bake_ground(village: Node3D, _source: ShaderMaterial) -> void:
	ground_image = Image.create(RESOLUTION.x,RESOLUTION.y,false,Image.FORMAT_RGBAH)
	ground_image.fill(Color(0.24,0.29,0.16))
	for name_ in ["VillageGround","EastBank"]:
		var ground: MeshInstance3D = village.get_node("Terrain/"+name_)
		var source := ground.get_active_material(0) as ShaderMaterial
		atlas = (source.get_shader_parameter("albedo_texture") as Texture2D).get_image()
		if atlas.is_compressed(): atlas.decompress()
		var average: Vector3 = source.get_shader_parameter("texture_average")
		var average_color := Color(average.x,average.y,average.z)
		var detail: float = source.get_shader_parameter("texture_detail")
		var uv_scale: Vector2 = source.get_shader_parameter("uv_scale")
		var uv_offset: Vector2 = source.get_shader_parameter("uv_offset")
		var pixel_brush: bool = source.get_shader_parameter("pixel_brush")
		var brush_grid := Vector2(atlas.get_size()) * 0.25
		var tint: Color = source.get_shader_parameter("base_color")
		tint = tint.srgb_to_linear()
		var saturation: float = source.get_shader_parameter("color_saturation")
		var transform := relative_transform(ground,village)
		var arrays := ground.mesh.surface_get_arrays(0)
		var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
		var normals: PackedVector3Array = arrays[Mesh.ARRAY_NORMAL]
		var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
		var uv: PackedVector2Array = arrays[Mesh.ARRAY_TEX_UV]
		var indices: PackedInt32Array = arrays[Mesh.ARRAY_INDEX]
		for i in range(0,indices.size(),3):
			var ids := [indices[i],indices[i+1],indices[i+2]]
			if normals[ids[0]].y < 0.9: continue
			var points := PackedVector2Array()
			for id_ in ids:
				var at := transform * vertices[id_]
				points.append((Vector2(at.x,at.z)-Vector2(RECT.x,RECT.y))/Vector2(RECT.z,RECT.w)*Vector2(RESOLUTION))
			var a := points[0]
			var ab := points[1]-a
			var ac := points[2]-a
			var determinant := ab.cross(ac)
			if absf(determinant) < 0.00001: continue
			var lower := Vector2i(maxi(0,floori(minf(a.x,minf(points[1].x,points[2].x)))),maxi(0,floori(minf(a.y,minf(points[1].y,points[2].y)))))
			var upper := Vector2i(mini(RESOLUTION.x-1,ceili(maxf(a.x,maxf(points[1].x,points[2].x)))),mini(RESOLUTION.y-1,ceili(maxf(a.y,maxf(points[1].y,points[2].y)))))
			for y in range(lower.y,upper.y+1):
				for x in range(lower.x,upper.x+1):
					var relative := Vector2(x+0.5,y+0.5)-a
					var b := relative.cross(ac)/determinant
					var c := ab.cross(relative)/determinant
					var w := 1.0-b-c
					if minf(w,minf(b,c)) < -0.0001: continue
					var vertex_color := colors[ids[0]]*w+colors[ids[1]]*b+colors[ids[2]]*c
					var sample_uv := (uv[ids[0]]*w+uv[ids[1]]*b+uv[ids[2]]*c)*uv_scale+uv_offset
					if pixel_brush: sample_uv = ((sample_uv*brush_grid).floor()+Vector2(.5,.5))/brush_grid
					var color := tint * vertex_color * average_color.lerp(atlas_color(sample_uv),detail)
					var grey := Vector3(color.r,color.g,color.b).dot(Vector3(0.2126,0.7152,0.0722))
					color = Color(grey,grey,grey).lerp(color,saturation).clamp()
					color.a = 1.0
					ground_image.set_pixel(x,y,color)
	if village.get_node("Terrain/VillageGround").get_active_material(0).get_shader_parameter("paint_floor_from_contact") == true:
		paint_shoulders(village)
	ground_image.generate_mipmaps()
	ground_map = ImageTexture.create_from_image(ground_image)
	ground_map.resource_name = "Village terrain contact albedo · linear"
	DirAccess.make_dir_recursive_absolute(MAP_PATH.get_base_dir())
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(ground_map,MAP_PATH)

func paint_shoulders(village: Node3D) -> void:
	var paving := preload("res://scripts/tools/art/forest_village_whitebox/flagstone_paving.gd").new()
	var arrays := (village.get_node("Paths/Surface") as MeshInstance3D).mesh.surface_get_arrays(0)
	var vertices: PackedVector3Array = arrays[Mesh.ARRAY_VERTEX]
	var colors: PackedColorArray = arrays[Mesh.ARRAY_COLOR]
	for i in vertices.size():
		paving.wear[Vector2i(roundi(vertices[i].x/paving.GRID),roundi(vertices[i].z/paving.GRID))] = colors[i].a
	var texel := Vector2(RECT.z,RECT.w)/Vector2(RESOLUTION)
	var distance := PackedFloat32Array()
	distance.resize(RESOLUTION.x*RESOLUTION.y)
	for y in RESOLUTION.y:
		for x in RESOLUTION.x:
			var at := Vector2(RECT.x,RECT.y)+(Vector2(x,y)+Vector2(.5,.5))*texel
			distance[y*RESOLUTION.x+x] = 0.0 if paving.coverage(at) > .23 else 100.0
	for step: int in [1,-1]:
		for row in RESOLUTION.y:
			var y := row if step == 1 else RESOLUTION.y-1-row
			for col in RESOLUTION.x:
				var x := col if step == 1 else RESOLUTION.x-1-col
				var i := y*RESOLUTION.x+x
				if x-step >= 0 and x-step < RESOLUTION.x:
					distance[i] = minf(distance[i],distance[i-step]+texel.x)
				if y-step >= 0 and y-step < RESOLUTION.y:
					distance[i] = minf(distance[i],distance[i-step*RESOLUTION.x]+texel.y)
	for y in RESOLUTION.y:
		for x in RESOLUTION.x:
			var d := distance[y*RESOLUTION.x+x]
			if d >= 1.25: continue
			var at := Vector2(x,y)*texel
			var margin := .90+.18*sin(at.x*4.2+sin(at.y*2.5))+.11*cos(at.y*5.1)
			var worn := 1.0-smoothstep(0.0,margin,d)
			var turf := ground_image.get_pixel(x,y)
			var earth := Color(turf.r*.68+.12,turf.g*.56+.062,turf.b*.60+.039)
			var paint := turf.lerp(earth,worn*.78)
			paint.a = 1.0-worn
			ground_image.set_pixel(x,y,paint)

func ground_color(at: Vector2) -> Color:
	var p := Vector2i((at-Vector2(RECT.x,RECT.y))/Vector2(RECT.z,RECT.w)*Vector2(RESOLUTION))
	return ground_image.get_pixelv(p.clamp(Vector2i.ZERO,RESOLUTION-Vector2i.ONE))

func contact_material(source: ShaderMaterial, vertex_mask: bool, strength := 1.0) -> ShaderMaterial:
	var result := source.duplicate() as ShaderMaterial
	result.set_shader_parameter("use_ground_contact",true)
	result.set_shader_parameter("ground_contact_map",ground_map)
	result.set_shader_parameter("ground_contact_rect",RECT)
	result.set_shader_parameter("ground_contact_from_vertex",vertex_mask)
	result.set_shader_parameter("ground_contact_height",0.38)
	result.set_shader_parameter("ground_contact_strength",strength)
	return result

func triangle(tool: SurfaceTool, a: Vector3, b: Vector3, c: Vector3, color: Color) -> void:
	var normal := (b-a).cross(c-a).normalized()
	for at in [a,c,b]:
		tool.set_normal(normal)
		tool.set_color(color)
		tool.set_uv(Vector2(0.503,0.253)+Vector2(fposmod(at.x*0.28,1.0),fposmod(at.z*0.28,1.0))*0.244)
		tool.add_vertex(at)

func tuft(at: Vector2, scale_: float) -> void:
	if not river.dry(at,0.48): return
	var key := Vector2i(floori(at.x/8.0),floori(at.y/8.0))
	if not grass_chunks.has(key):
		var tool := SurfaceTool.new()
		tool.begin(Mesh.PRIMITIVE_TRIANGLES)
		tool.set_material(blade_material)
		grass_chunks[key] = tool
	var tool: SurfaceTool = grass_chunks[key]
	var tone := ground_color(at)*rng.randf_range(1.02,1.28)
	tone.a = 1.0
	for i in range(4):
		var angle := rng.randf_range(0,TAU)
		var axis := Vector3(cos(angle),0,sin(angle))
		var base := Vector3(at.x+rng.randf_range(-0.06,0.06),0.003,at.y+rng.randf_range(-0.06,0.06))
		var width := rng.randf_range(0.018,0.034)*scale_
		var height := rng.randf_range(0.075,0.18)*scale_
		var bend := Vector3(-axis.z,0,axis.x)*rng.randf_range(0.025,0.07)*scale_
		var mid := base+Vector3.UP*height*0.55+bend*0.3
		var tip := base+Vector3.UP*height+bend
		triangle(tool,base-axis*width,mid-axis*width*0.55,base+axis*width,tone*0.91)
		triangle(tool,base+axis*width,mid-axis*width*0.55,mid+axis*width*0.55,tone)
		triangle(tool,mid-axis*width*0.55,tip,mid+axis*width*0.55,tone*1.08)
	tufts += 1

func root_patch(center: Vector2, radius: float, paving: RefCounted) -> void:
	const SIDES := 24
	var ring := PackedVector2Array()
	for i in range(SIDES):
		var angle := TAU*i/SIDES
		ring.append(center+Vector2(cos(angle),sin(angle))*radius*rng.randf_range(0.88,1.16))
	var earth := Color(0.37,0.31,0.19,0.32)
	for i in range(SIDES):
		var j := (i+1)%SIDES
		var middle := center.lerp(ring[i],0.62)
		var next := center.lerp(ring[j],0.62)
		# Vertex alpha controls opaque albedo blending, never transparency.
		var color := earth
		for triangle_ in [[center,next,middle],[middle,next,ring[j]],[middle,ring[j],ring[i]]]:
			for polygon: PackedVector2Array in river.land_polygons(PackedVector2Array(triangle_)):
				for index in Geometry2D.triangulate_polygon(polygon):
					var point := polygon[index]
					color.a = 0.32*(1.0-smoothstep(radius*0.55,radius,point.distance_to(center)))
					patches.set_normal(Vector3.UP)
					patches.set_color(color)
					patches.set_uv(Vector2(0.56,0.31)+(point-center)*0.025)
					patches.add_vertex(Vector3(point.x,0.006,point.y))
	for i in range(rng.randi_range(9,15)):
		var angle := rng.randf_range(0,TAU)
		var at := center+Vector2(cos(angle),sin(angle))*radius*rng.randf_range(0.68,1.04)
		if paving.coverage(at) < 0.12: tuft(at,rng.randf_range(0.8,1.2))
	roots += 1

func build(village: Node3D, paving: RefCounted, source: ShaderMaterial) -> void:
	var previous := village.get_node_or_null("GroundContact")
	if previous != null: previous.free()
	rng.seed = 2026102601
	bake_ground(village,source)
	var shoulder: MeshInstance3D = village.get_node("Paths/Surface")
	shoulder.mesh.surface_set_material(0,contact_material(shoulder.get_active_material(0),true))
	var stones := village.get_node("Paths/Flagstones")
	var stone_material := contact_material(stones.get_child(0).get_active_material(0),true,0.52)
	for stone in stones.get_children(): stone.mesh.surface_set_material(0,stone_material)
	var leaf_material := contact_material(source,true)
	leaf_material.resource_name = "Forest floor · irregular root litter"
	patches = SurfaceTool.new()
	patches.begin(Mesh.PRIMITIVE_TRIANGLES)
	patches.set_material(leaf_material)
	blade_material = preload("res://assets/materials/environment/forest_village/ground_flora/verge.tres")
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().apply_surface_response(leaf_material,"soil")
	var bark_copies: Dictionary = {}
	for tree: Node3D in village.get_node("Forest").find_children("Tree_*","Node3D",true,false):
		var model: MeshInstance3D = tree.get_node("Model")
		# Read the source material so rebuilding cannot accumulate overrides.
		var bark := model.mesh.surface_get_material(0) as ShaderMaterial
		if not bark_copies.has(bark):
			var copy := contact_material(bark,false)
			copy.resource_name = bark.resource_name+" · moss at terrain contact"
			bark_copies[bark] = copy
		model.set_surface_override_material(0,bark_copies[bark])
		village.set_editable_instance(tree,true)
		var transform := relative_transform(tree,village)
		var vertices: PackedVector3Array = model.mesh.surface_get_arrays(0)[Mesh.ARRAY_VERTEX]
		var radius := 0.0
		for point in vertices:
			if point.y < 0.12: radius = maxf(radius,Vector2(point.x,point.z).length())
		var center := Vector2(transform.origin.x,transform.origin.z)
		root_patch(center,radius*transform.basis.get_scale().x+0.5,paving)
	for x in range(-165,165):
		for z in range(-153,153):
			var at := Vector2(x,z)*0.24+Vector2(rng.randf_range(-0.1,0.1),rng.randf_range(-0.1,0.1))
			var wear: float = paving.coverage(at)
			if wear < 0.025 or wear > 0.60 or rng.randf() > 0.20: continue
			if at.x > 25.1 and at.x < 31.1 and absf(at.y-2.34) < 1.85: continue
			tuft(at,rng.randf_range(0.65,1.0))
	var contact := Node3D.new()
	contact.name = "GroundContact"
	contact.set_meta("decorative_only",true)
	contact.set_meta("root_count",roots)
	contact.set_meta("grass_tufts",tufts)
	village.add_child(contact)
	var litter := MeshInstance3D.new()
	litter.name = "RootLitter"
	litter.mesh = patches.commit()
	litter.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	litter.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
	litter.set_meta("ddgi_contribute",false)
	contact.add_child(litter)
	for key: Vector2i in grass_chunks:
		var instance := MeshInstance3D.new()
		instance.name = "Grass_%d_%d" % [key.x,key.y]
		instance.mesh = (grass_chunks[key] as SurfaceTool).commit()
		instance.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
		instance.gi_mode = GeometryInstance3D.GI_MODE_DISABLED
		instance.set_meta("ddgi_contribute",false)
		contact.add_child(instance)
	print("FOREST_GROUND_CONTACT: ",roots," root transitions; ",tufts," decorative grass tufts; opaque terrain matching")
