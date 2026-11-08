extends SceneTree

func _initialize() -> void:
	var meta := "res://assets/resources/sprites/amber_fox_style_v2/greatsword_stance_transition/"
	var textures := "res://assets/textures/sprites/amber_fox_style_v2/greatsword_stance_transition/"
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(meta + "manifest.json"))
	var frames := load(meta + "sprite_frames.tres") as SpriteFrames
	assert(frames != null and frames.get_animation_names().size() == 6)
	var checked := 0
	for clip: Dictionary in manifest.clips:
		for reverse in [false, true]:
			var animation := StringName(clip.reverse if reverse else clip.forward)
			assert(frames.get_frame_count(animation) == int(clip.frames))
			assert(is_equal_approx(frames.get_animation_speed(animation), 62.4))
			assert(frames.get_animation_loop_mode(animation) == SpriteFrames.LOOP_NONE)
			for n in range(int(clip.frames)):
				var index: int = int(clip.frames) - 1 - n if reverse else n
				var expected := Image.load_from_file(textures + String(clip.pair) + "/frames/transition_%03d.png" % index)
				var actual := frames.get_frame_texture(animation, n).get_image()
				assert(actual.get_size() == Vector2i(320, 384))
				assert_pixels(actual, expected)
				if index == 0 or index == int(clip.frames) - 1:
					var stance := String(clip.pair).split("-")[0 if index == 0 else 1]
					var idle := Image.load_from_file("res://assets/textures/sprites/amber_fox_style_v2/greatsword_stance_idle/" + stance + "/frames/idle_000.png")
					assert_pixels(actual, idle)
				checked += 1
	var report := {"ok": true, "directed_edges": 6, "checked_frames": checked, "fps": 62.4,
		"visible_rgb_and_alpha_exact": true, "idle_endpoints_exact": true, "frame_size": [320, 384]}
	var output := FileAccess.open(meta + "godot-validation.json", FileAccess.WRITE)
	assert(output != null)
	output.store_string(JSON.stringify(report, "  ") + "\n")
	print("TRANSITION_IMPORT_CHECK: " + JSON.stringify(report))
	quit()

func assert_pixels(actual: Image, expected: Image) -> void:
	actual.convert(Image.FORMAT_RGBA8)
	expected.convert(Image.FORMAT_RGBA8)
	var a := actual.get_data()
	var b := expected.get_data()
	assert(a.size() == b.size())
	for i in range(0, b.size(), 4):
		assert(a[i + 3] == b[i + 3], "Imported alpha mismatch")
		if b[i + 3] > 0:
			assert(a[i] == b[i] and a[i + 1] == b[i + 1] and a[i + 2] == b[i + 2], "Imported RGB mismatch")
