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
			var expected := Image.load_from_file(textures + String(clip.key) + "/frames/attack_%03d.png" % n)
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
		assert(seconds >= 0.85 and seconds <= 1.2, "单段保留蓄势与完整回架，整体节奏控制在 0.85 至 1.2 秒")
		durations[animation] = seconds
		var trail: Resource = load("res://assets/resources/weapons/greatsword_attack_%s_trail.tres" % clip.key)
		assert(StringName(trail.get("Animation")) == animation)
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
			# Forward lunge may sink with the knees; the blade stays horizontal and never travels upward.
			var forward_travel := poses[-1].z - poses[0].z
			assert(tip_min_y >= 240 and tip_max_y - tip_min_y <= 18 and forward_travel >= 80 and absf(poses[-1].w - poses[0].w) <= forward_travel * 0.15, "低位平刺以向前送出为主，剑尖起伏须小于前送距离的 15%，不得上挑")
		var release_pose: Dictionary = clip.records[int(clip.holds.release_frame)]
		var impact_pose: Dictionary = clip.records[int(clip.holds.impact_frame)]
		var recovery_pose: Dictionary = clip.records[int(clip.holds.recovery_frame)]
		var release_frame := int(release_pose.runtime_first_frame)
		var impact_frame := int(impact_pose.runtime_first_frame)
		var recovery_frame := int(recovery_pose.runtime_first_frame)
		assert(int(keys[0]) >= release_frame and int(keys[-1]) <= impact_frame, "刀光只跟随出手与落点，不覆盖前后摇")
		var strike_seconds := 0.0
		for n in range(release_frame, impact_frame):
			strike_seconds += frames.get_frame_duration(animation, n) / frames.get_animation_speed(animation)
		assert(strike_seconds >= 0.075 and strike_seconds <= 0.12, "挥出保留可读的转刃姿势，集中于 75 至 120 毫秒")
		strike_checks[clip.key] = {"seconds":strike_seconds,"tip_y_span":tip_max_y-tip_min_y,"forward":true}
		var windup := 0.0
		for n in range(release_frame):
			windup += frames.get_frame_duration(animation, n) / frames.get_animation_speed(animation)
		var windup_pose: Dictionary = clip.records[int(clip.holds.windup_frame)]
		var windup_frame := int(windup_pose.runtime_first_frame)
		assert(windup_frame < release_frame and impact_frame >= int(keys[-1]), "顿帧必须位于挥出窗口两侧")
		var loaded_hold := float(windup_pose.runtime_frames) / frames.get_animation_speed(animation)
		var impact_hold := float(impact_pose.runtime_frames) / frames.get_animation_speed(animation)
		assert(windup >= 0.34 and windup <= 0.48 and windup >= strike_seconds * 3, "蓄势仍慢于出手，缩短等待后连续衔接挥出")
		assert(loaded_hold >= 0.05 and loaded_hold <= 0.09, "蓄满短停 50 至 90 毫秒，避免长时间定住")
		assert(impact_hold >= 0.04 and impact_hold <= 0.06, "落点短停 40 至 60 毫秒后连续收势")
		var recovery_seconds := seconds - float(recovery_frame) / frames.get_animation_speed(animation)
		assert(recovery_seconds >= 0.35 and recovery_seconds <= 0.55, "收势以连续姿势保留承重、屈肘与回架，控制在 350 至 550 毫秒")
		assert(int(clip.frames) - int(clip.holds.recovery_frame) >= 12, "收势必须有连续姿势，不能只延长末帧")
		rhythm_checks[clip.key] = {"windup_seconds":windup,"loaded_hold_seconds":loaded_hold,"impact_hold_seconds":impact_hold,"recovery_seconds":recovery_seconds}
		if int(clip.step) > 1:
			var start := Image.load_from_file("res://" + String(clip.start_frame))
			start.convert(Image.FORMAT_RGBA8)
			var first_pose := frames.get_frame_texture(animation, 0).get_image()
			first_pose.convert(Image.FORMAT_RGBA8)
			var start_pixels := start.get_data()
			var pose_pixels := first_pose.get_data()
			for i in range(0, start_pixels.size(), 4):
				assert(start_pixels[i+3] == pose_pixels[i+3], "续招首帧必须衔接上一击落点")
				if start_pixels[i+3] > 0:
					assert(start_pixels[i] == pose_pixels[i] and start_pixels[i+1] == pose_pixels[i+1] and start_pixels[i+2] == pose_pixels[i+2])
	var report := {"ok":true,"checked_frames":checked,"runtime_ticks":runtime_checked,"frame_size":[512,384],
		"visible_rgb_and_alpha_exact":true,"one_shot":true,"per_frame_timing_exact":true,"durations":durations,"strikes":strike_checks,"rhythm":rhythm_checks}
	var file := FileAccess.open(meta + "godot-validation.json",FileAccess.WRITE)
	assert(file != null)
	file.store_string(JSON.stringify(report,"  ")+"\n")
	print("ATTACK_IMPORT_CHECK: "+JSON.stringify(report))
	quit(0)
