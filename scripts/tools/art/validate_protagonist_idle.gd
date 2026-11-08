extends SceneTree

func _initialize() -> void:
	var meta := "res://assets/resources/sprites/amber_fox_style_v2/greatsword_stance_idle/"
	var frames := load(meta + "sprite_frames.tres") as SpriteFrames
	assert(frames != null)
	assert(frames.get_animation_names().size() == 3)
	var clips: Array = JSON.parse_string(FileAccess.get_file_as_string(meta + "clips.json"))
	var checked := 0
	for clip: Dictionary in clips:
		var name := StringName(clip.animation)
		assert(frames.get_frame_count(name) == 72)
		assert(frames.get_animation_speed(name) == 24.0)
		assert(frames.get_animation_loop(name))
		for n in range(72):
			var actual := frames.get_frame_texture(name, n).get_image()
			var expected := Image.load_from_file("res://" + String(clip.texture_directory) + "/frames/idle_%03d.png" % n)
			assert(actual.get_size() == Vector2i(320,384))
			actual.convert(Image.FORMAT_RGBA8)
			expected.convert(Image.FORMAT_RGBA8)
			var a := actual.get_data()
			var b := expected.get_data()
			for i in range(0,b.size(),4):
				assert(a[i+3] == b[i+3], "导入透明度不一致")
				if b[i+3] > 0:
					assert(a[i] == b[i] and a[i+1] == b[i+1] and a[i+2] == b[i+2], "导入颜色不一致")
			checked += 1
	var runtime := load("res://assets/resources/sprites/amber_fox_style_v2/greatsword_v4/sprite_frames.tres") as SpriteFrames
	assert(runtime != null and runtime.has_animation(&"attack") and runtime.has_animation(&"run"))
	assert(not runtime.has_animation(&"idle"))
	var report := {"ok":true,"checked_frames":checked,"animations":frames.get_animation_names(),"frame_size":[320,384],"visible_rgb_and_alpha_exact":true,"runtime_resource_loadable":true,"runtime_replaced":true}
	var out := FileAccess.open(meta + "godot-validation.json",FileAccess.WRITE)
	assert(out != null)
	out.store_string(JSON.stringify(report,"  ") + "\n")
	print(JSON.stringify(report))
	quit(0)
