extends SceneTree

func _initialize() -> void:
	var meta := "res://assets/resources/sprites/amber_fox_style_v2/greatsword_stance_movement/"
	var textures := "res://assets/textures/sprites/amber_fox_style_v2/greatsword_stance_movement/"
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(meta + "manifest.json"))
	var frames := load(meta + "sprite_frames.tres") as SpriteFrames
	assert(frames != null and frames.get_animation_names().size() == manifest.clips.size())
	var checked := 0
	for clip: Dictionary in manifest.clips:
		var animation := StringName(clip.name)
		assert(frames.get_frame_count(animation) == int(clip.frames))
		assert(is_equal_approx(frames.get_animation_speed(animation), float(clip.fps)))
		assert(frames.get_animation_loop_mode(animation) == (SpriteFrames.LOOP_LINEAR if clip.loop else SpriteFrames.LOOP_NONE))
		for n in range(int(clip.frames)):
			var expected := Image.load_from_file(textures + String(clip.get("asset", clip.stance)) + "/frames/move_%03d.png" % n)
			var actual := frames.get_frame_texture(animation, n).get_image()
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
	var report := {"ok": true, "checked_frames": checked, "animations": frames.get_animation_names(),
		"frame_size": [320,384], "visible_rgb_and_alpha_exact": true, "loop_modes_match_manifest": true}
	var output := FileAccess.open(meta + "godot-validation.json",FileAccess.WRITE)
	assert(output != null)
	output.store_string(JSON.stringify(report,"  ") + "\n")
	print("MOVEMENT_IMPORT_CHECK: " + JSON.stringify(report))
	quit(0)
