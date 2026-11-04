extends RefCounted
## Offline tuft source for the atlas, and shared bent cards for runtime instancing.

const ATLAS := "res://assets/textures/environment/forest_village/ground_flora/grass_clumps.res"
const VARIANTS := 6

func source_mesh(index: int) -> ArrayMesh:
	var rng := RandomNumberGenerator.new()
	rng.seed = 98213+index*779
	var tool := SurfaceTool.new()
	tool.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in range(18):
		var x := lerpf(-.19,.19,float(i)/17.0)+rng.randf_range(-.012,.012)
		var height := rng.randf_range(.16,.34)
		var lean := rng.randf_range(-.10,.10)
		var width := rng.randf_range(.022,.041)
		var low := Color("49632d").srgb_to_linear()
		var top := Color("86a947").srgb_to_linear().lerp(Color("a6bc64").srgb_to_linear(),rng.randf()*.45)
		for segment in range(7):
			var points: Array[Vector3] = []
			var colors: Array[Color] = []
			for t: float in [float(segment)/7.0,float(segment+1)/7.0]:
				var center := Vector3(x+lean*t*t,height*(t-.10*t*t),float(i)*.0002)
				var half := width*pow(1.0-t,.8)*.5
				var color := low.lerp(top,smoothstep(0,.9,t))
				points.append(center+Vector3.LEFT*half)
				points.append(center+Vector3(0,0,.002))
				points.append(center+Vector3.RIGHT*half)
				colors.append(color*.9)
				colors.append(color)
				colors.append(color*1.04)
			for v in [0,3,1,1,3,4,1,4,2,2,4,5]:
				tool.set_color(colors[v])
				tool.set_normal(Vector3.BACK)
				tool.add_vertex(points[v])
	tool.index()
	return tool.commit()

func mesh(index: int, material: Material) -> ArrayMesh:
	var vertices := PackedVector3Array()
	var normals := PackedVector3Array()
	var colors := PackedColorArray()
	var uv := PackedVector2Array()
	var indices := PackedInt32Array()
	var distant := PackedInt32Array()
	for plane in range(3):
		var angle := float(plane)*PI/3.0
		var side := Vector3(cos(angle),0,sin(angle))
		var start := vertices.size()
		for t: float in [0.0,.45,1.0]:
			for sign_: float in [-1.0,1.0]:
				vertices.append(side*.28*sign_+Vector3(0,.60*t,.035*t*t))
				normals.append(Vector3.UP)
				colors.append(Color(1,1,1,t))
				uv.append(Vector2((index+(sign_+1)*.5)/float(VARIANTS),1.0-t))
		for v in [0,1,2,1,3,2,2,3,4,3,5,4]: indices.append(start+v)
		for v in [0,1,4,1,5,4]: distant.append(start+v)
	var arrays: Array = []
	arrays.resize(Mesh.ARRAY_MAX)
	arrays[Mesh.ARRAY_VERTEX] = vertices
	arrays[Mesh.ARRAY_NORMAL] = normals
	arrays[Mesh.ARRAY_COLOR] = colors
	arrays[Mesh.ARRAY_TEX_UV] = uv
	arrays[Mesh.ARRAY_INDEX] = indices
	var result := ArrayMesh.new()
	result.add_surface_from_arrays(Mesh.PRIMITIVE_TRIANGLES,arrays,[],{.012:distant},Mesh.ARRAY_FLAG_COMPRESS_ATTRIBUTES)
	result.surface_set_material(0,material)
	return result
