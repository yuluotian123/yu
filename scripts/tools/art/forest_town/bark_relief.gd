extends RefCounted
## Offline OpenGL tangent-space relief aligned to the existing painted bark UVs.
const ROOT := "res://assets/textures/environment/forest_town/"
const NORMAL := ROOT+"bark_painted_v4_normal.res"

func bake() -> void:
	var source := Image.load_from_file(ROOT+"bark_painted_v4.png")
	assert(source != null and not source.is_empty())
	# Broad plates carry the relief; small painted color flecks stay smooth.
	var broad: Image = source.duplicate()
	broad.resize(256,256,Image.INTERPOLATE_LANCZOS)
	broad.resize(1024,1024,Image.INTERPOLATE_BILINEAR)
	var detail: Image = source.duplicate()
	detail.resize(512,512,Image.INTERPOLATE_LANCZOS)
	detail.resize(1024,1024,Image.INTERPOLATE_BILINEAR)
	var height := Image.create(1024,1024,false,Image.FORMAT_RF)
	for y in 1024:
		for x in 1024:
			var color := broad.get_pixel(x,y).lerp(detail.get_pixel(x,y),0.25)
			var value := color.r*.2126+color.g*.7152+color.b*.0722
			height.set_pixel(x,y,Color(value,0,0))
	height.bump_map_to_normal_map(20.0)
	assert(height.save_png(ROOT+"bark_painted_v4_normal.png") == OK)
	height.generate_mipmaps(true)
	var texture := ImageTexture.create_from_image(height)
	texture.resource_name = "Painted oak · aligned broad bark relief (OpenGL)"
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(texture,NORMAL)
	print("BARK_RELIEF_BAKED: 1024x1024 linear tangent normal; renormalized mipmaps")
