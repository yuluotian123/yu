extends SceneTree

func _initialize() -> void:
	var meta := "res://assets/resources/sprites/amber_fox_style_v2/greatsword_stance_attack/"
	var textures := "res://assets/textures/sprites/amber_fox_style_v2/greatsword_stance_attack/"
	var manifest: Dictionary = JSON.parse_string(FileAccess.get_file_as_string(meta + "manifest.json"))
	var frames := load(meta + "sprite_frames.tres") as SpriteFrames
	assert(frames != null and frames.get_animation_names().size() == 3 and manifest.clips.size() == 3)
	var checked := 0
	var durations := {}
	var strike_checks := {}
	var rhythm_checks := {}
	var runtime_checked := 0
	for clip: Dictionary in manifest.clips:
		var animation := StringName(clip.name)
		assert(frames.get_frame_count(animation) == int(clip.runtime_frames))
		assert(is_equal_approx(frames.get_animation_speed(animation), float(clip.runtime_fps)))
		assert(frames.get_animation_loop_mode(animation) == SpriteFrames.LOOP_NONE)
		var seconds := 0.0
		for n in range(int(clip.frames)):
			var record: Dictionary = clip.records[n]
			var first := int(record.runtime_first_frame)
			var count := int(record.runtime_frames)
			var expected := Image.load_from_file(textures + String(clip.stance) + "/frames/attack_%03d.png" % n)
			var texture := frames.get_frame_texture(animation, first)
			var actual := texture.get_image()
			assert(actual.get_size() == Vector2i(512,384))
			actual.convert(Image.FORMAT_RGBA8)
			expected.convert(Image.FORMAT_RGBA8)
			var a := actual.get_data()
			var b := expected.get_data()
			for i in range(0,b.size(),4):
				assert(a[i+3] == b[i+3], "攻击帧导入透明度不一致")
				if b[i+3] > 0:
					assert(a[i] == b[i] and a[i+1] == b[i+1] and a[i+2] == b[i+2], "攻击帧导入颜色不一致")
			var pose_seconds := 0.0
			for tick in range(first, first + count):
				assert(frames.get_frame_texture(animation, tick) == texture, "顿帧必须保持同一人物与剑姿势")
				assert(is_equal_approx(frames.get_frame_duration(animation, tick), 1.0), "运行时均匀采样，避免长短帧切换丢失时间")
				pose_seconds += frames.get_frame_duration(animation,tick) / frames.get_animation_speed(animation)
				runtime_checked += 1
			assert(is_equal_approx(pose_seconds, float(record.duration_ms)/1000.0))
			seconds += pose_seconds
			checked += 1
		assert(is_equal_approx(seconds, float(clip.duration)))
		assert(seconds <= 0.8, "蓄力与顿帧后仍应在 0.8 秒内回架")
		durations[animation] = seconds
		var trail: Resource = load("res://assets/resources/weapons/greatsword_attack_%s_trail.tres" % clip.stance)
		var poses: PackedVector4Array = trail.get("BladePoses")
		var keys: PackedFloat32Array = trail.get("Frames")
		assert(poses.size() >= 2 and poses.size() == keys.size())
		var tip_min_y := INF
		var tip_max_y := -INF
		for pose in poses:
			assert(lerpf(pose.x, pose.z, 0.78) >= 256 and pose.z >= 256, "发光的剑刃外缘必须位于角色前方")
			tip_min_y = minf(tip_min_y, pose.w)
			tip_max_y = maxf(tip_max_y, pose.w)
			if clip.stance == "low":
				assert(pose.z > pose.x and absf(pose.w - pose.y) <= 10, "下段必须向前平刺")
		if clip.stance == "mid":
			assert(tip_max_y - tip_min_y <= 32 and tip_min_y >= 200, "中段横扫不能变成过顶斩或上刺")
		elif clip.stance == "low":
			assert(tip_min_y >= 240 and tip_max_y - tip_min_y <= 18 and poses[-1].z - poses[0].z >= 80 and absf(poses[-1].w - poses[0].w) <= 10, "低位平刺主要沿水平方向前送")
		var strike_seconds := 0.0
		for n in range(int(keys[0]), int(keys[-1])):
			strike_seconds += frames.get_frame_duration(animation, n) / frames.get_animation_speed(animation)
		assert(strike_seconds <= 0.12, "出手应集中于 120 毫秒内")
		strike_checks[clip.stance] = {"seconds":strike_seconds,"tip_y_span":tip_max_y-tip_min_y,"forward":true}
		var windup := 0.0
		for n in range(int(keys[0])):
			windup += frames.get_frame_duration(animation, n) / frames.get_animation_speed(animation)
		var windup_pose: Dictionary = clip.records[int(clip.holds.windup_frame)]
		var impact_pose: Dictionary = clip.records[int(clip.holds.impact_frame)]
		var windup_frame := int(windup_pose.runtime_first_frame)
		var impact_frame := int(impact_pose.runtime_first_frame)
		assert(windup_frame < int(keys[0]) and impact_frame >= int(keys[-1]), "顿帧必须位于挥出窗口两侧")
		var loaded_hold := float(windup_pose.runtime_frames) / frames.get_animation_speed(animation)
		var impact_hold := float(impact_pose.runtime_frames) / frames.get_animation_speed(animation)
		assert(windup >= 0.2 and windup <= 0.4 and windup >= strike_seconds * 3.0, "蓄力必须形成慢蓄快出的对比")
		assert(loaded_hold >= 0.08 and loaded_hold <= 0.15, "蓄满姿势应有可见的短暂停顿")
		assert(impact_hold >= 0.04 and impact_hold <= 0.1, "落点顿帧应清晰而短促")
		rhythm_checks[clip.stance] = {"windup_seconds":windup,"loaded_hold_seconds":loaded_hold,"impact_hold_seconds":impact_hold}
	var report := {"ok":true,"checked_frames":checked,"runtime_ticks":runtime_checked,"frame_size":[512,384],
		"visible_rgb_and_alpha_exact":true,"one_shot":true,"per_frame_timing_exact":true,"durations":durations,"strikes":strike_checks,"rhythm":rhythm_checks}
	var file := FileAccess.open(meta + "godot-validation.json",FileAccess.WRITE)
	assert(file != null)
	file.store_string(JSON.stringify(report,"  ")+"\n")
	print("ATTACK_IMPORT_CHECK: "+JSON.stringify(report))
	quit(0)
