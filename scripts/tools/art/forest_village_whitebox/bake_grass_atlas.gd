extends SceneTree
## Bake the authored tuft geometry without lighting into a reusable alpha atlas.

const OUTPUT := "res://assets/textures/environment/forest_village/ground_flora/"
const SOURCE := preload("res://scripts/tools/art/forest_village_whitebox/grass_cards.gd")

func _initialize() -> void:
	run.call_deferred()

func run() -> void:
	DirAccess.make_dir_recursive_absolute(OUTPUT)
	var viewport := SubViewport.new()
	viewport.size = Vector2i(320,256)
	viewport.transparent_bg = true
	viewport.own_world_3d = true
	viewport.msaa_3d = Viewport.MSAA_4X
	root.add_child(viewport)
	var camera := Camera3D.new()
	camera.projection = Camera3D.PROJECTION_ORTHOGONAL
	camera.size = .42
	camera.position = Vector3(0,.21,2)
	viewport.add_child(camera)
	var material := ShaderMaterial.new()
	var shader := Shader.new()
	shader.code = "shader_type spatial; render_mode unshaded,cull_disabled; void fragment(){ALBEDO=COLOR.rgb;}"
	material.shader = shader
	var model := MeshInstance3D.new()
	model.material_override = material
	viewport.add_child(model)
	var atlas := Image.create(320*SOURCE.VARIANTS,256,false,Image.FORMAT_RGBA8)
	var source := SOURCE.new()
	for index in SOURCE.VARIANTS:
		model.mesh = source.source_mesh(index)
		viewport.render_target_update_mode = SubViewport.UPDATE_ONCE
		await process_frame
		await RenderingServer.frame_post_draw
		atlas.blit_rect(viewport.get_texture().get_image(),Rect2i(0,0,320,256),Vector2i(index*320,0))
	assert(atlas.save_png(OUTPUT+"grass_clumps.png") == OK)
	atlas.generate_mipmaps()
	var texture := ImageTexture.create_from_image(atlas)
	preload("res://scripts/tools/art/forest_town/material_palette.gd").new().save_resource(texture,SOURCE.ATLAS)
	print("GRASS_ATLAS_BAKED ",SOURCE.ATLAS," 6 authored tufts; 18 blades each; 1920x256 RGBA with mipmaps")
	viewport.queue_free()
	await process_frame
	quit()
