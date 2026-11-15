extends SceneTree

func _initialize() -> void:
	var base := "res://assets/resources/sprites/amber_fox_style_v2/greatsword_jump/"
	var config: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(base + "production.json"))
	var frames := load(base + "sprite_frames.tres") as SpriteFrames
	assert(frames != null and frames.get_animation_names().size() == config.clips.size() * config.phases.size())
	var checked := 0
	for clip: Dictionary in config.clips:
		var index := 0
		for phase: Dictionary in config.phases:
			var animation := StringName(phase.name + "_" + clip.stance)
			var count := int(clip.phase_counts[phase.name])
			assert(frames.get_frame_count(animation) == count)
			assert(frames.get_animation_loop_mode(animation) == SpriteFrames.LOOP_NONE)
			assert(abs(count / frames.get_animation_speed(animation) - phase.seconds) < .000001)
			for n in range(count):
				var imported := frames.get_frame_texture(animation, n).get_image()
				assert(imported.get_size() == Vector2i(512, 384))
				if imported.is_compressed():
					imported.decompress()
				imported.convert(Image.FORMAT_RGBA8)
				var raw := Image.load_from_file("res://assets/textures/sprites/amber_fox_style_v2/greatsword_jump/%s/frames/jump_%03d.png" % [clip.stance, index])
				raw.convert(Image.FORMAT_RGBA8)
				var expected := raw.get_data()
				var actual := imported.get_data()
				assert(expected.size() == actual.size())
				for p in range(0, expected.size(), 4):
					assert(expected[p+3] == actual[p+3], "Imported alpha differs")
					if expected[p+3] > 0:
						assert(expected[p] == actual[p] and expected[p+1] == actual[p+1] and expected[p+2] == actual[p+2], "Imported RGB differs")
				index += 1
				checked += 1
		assert(index == clip.frames.size())
	var report := {"frames": checked, "phase_animations": frames.get_animation_names().size(),
		"visible_rgb_and_alpha_exact": true, "phase_durations_exact": true}
	var report_dir := "res://.godot/art-validation/greatsword_jump/"
	DirAccess.make_dir_recursive_absolute(report_dir)
	var output := FileAccess.open(report_dir + "import.json", FileAccess.WRITE)
	assert(output != null)
	output.store_string(JSON.stringify(report, "  ") + "\n")
	print("JUMP_IMPORT: PASS ", checked, " frames")
	quit(0)
