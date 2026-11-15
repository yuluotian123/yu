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
		assert(int(clip.frames) == 64 and int(clip.runtime_frames) == 64, "每招交付序列和 Godot 实际播放均为 64 帧")
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
			var runtime_ms := float(record.get("runtime_duration_ms", record.duration_ms))
			assert(is_equal_approx(pose_seconds, runtime_ms/1000.0))
			assert(absf(runtime_ms-float(record.duration_ms)) < 1.0, "APNG/Aseprite 每帧毫秒取整误差小于 1 毫秒")
			seconds += pose_seconds
			checked += 1
		assert(is_equal_approx(seconds, float(clip.duration)))
		var is_high := String(clip.key) == "high"
		var is_mid := String(clip.key) == "mid"
		assert(seconds >= (1.599 if is_high else 1.25) and seconds <= (1.601 if is_high else 1.6),
			"上段压缩至 1.600 秒，其他架势保留各自完整动作")
		durations[animation] = seconds
		var release_pose: Dictionary = clip.records[int(clip.holds.release_frame)]
		var impact_pose: Dictionary = clip.records[int(clip.holds.impact_frame)]
		var recovery_pose: Dictionary = clip.records[int(clip.holds.recovery_frame)]
		var release_frame := int(release_pose.runtime_first_frame)
		var impact_frame := int(impact_pose.runtime_first_frame)
		var recovery_frame := int(recovery_pose.runtime_first_frame)
		var strike_seconds := 0.0
		for n in range(release_frame, impact_frame):
			strike_seconds += frames.get_frame_duration(animation, n) / frames.get_animation_speed(animation)
		assert(strike_seconds >= (0.074 if is_high else (0.16 if is_mid else 0.075)) - 0.000001
			and strike_seconds <= (0.076 if is_high else (0.20 if is_mid else 0.12)) + 0.000001,
			"上段用 75 毫秒表现蹬腿到胸肩带刀；中段连续横切 160 至 200 毫秒，下段出手 75 至 120 毫秒")
		strike_checks[clip.key] = {"seconds":strike_seconds}
		if String(clip.key) == "mid":
			var contact := frames.get_frame_texture(animation, impact_frame).get_image()
			contact.convert(Image.FORMAT_RGBA8)
			var left := 512
			var right := -1
			var top := 384
			var bottom := -1
			# Beyond the hands, the right-facing contact must expose the cutting edge.
			for y in range(225, 275):
				for x in range(325, 445):
					if contact.get_pixel(x, y).a > 0.0:
						left = mini(left, x)
						right = maxi(right, x)
						top = mini(top, y)
						bottom = maxi(bottom, y)
			var blade_width := right - left + 1
			var blade_height := bottom - top + 1
			assert(blade_width >= 65, "中段落点必须朝右侧敌人切出，不能停在身后")
			assert(blade_height > 0 and blade_height <= 10 and blade_width >= blade_height * 8,
				"中段横砍落点应露出转刃后的薄投影，不能是竖直宽剑面")
			strike_checks[clip.key]["contact_blade_bounds"] = [left, top, right + 1, bottom + 1]
		var windup := 0.0
		for n in range(release_frame):
			windup += frames.get_frame_duration(animation, n) / frames.get_animation_speed(animation)
		var windup_pose: Dictionary = clip.records[int(clip.holds.windup_frame)]
		var windup_frame := int(windup_pose.runtime_first_frame)
		assert(windup_frame < release_frame and release_frame < impact_frame, "顿帧必须位于挥出窗口两侧")
		var loaded_hold := float(clip.holds.get("windup_hold_frames", windup_pose.runtime_frames)) / frames.get_animation_speed(animation)
		var impact_hold := float(clip.holds.get("impact_hold_frames", impact_pose.runtime_frames)) / frames.get_animation_speed(animation)
		if is_mid:
			assert(windup >= 0.301 and windup <= 0.303 and seconds >= 1.379 and seconds <= 1.381,
				"中段保持快起手：准备约 302 毫秒，整招 1.380 秒，随挥和回架使用剩余时间")
		elif is_high:
			assert(windup >= 0.299 and windup <= 0.301 and windup >= strike_seconds * 3,
				"上段 300 毫秒向后蓄重，快斩前保留身体准备")
		else:
			assert(windup >= 0.34 and windup <= 0.48 and windup >= strike_seconds * 3)
		assert(loaded_hold >= (0.042 if is_mid else 0.045) and loaded_hold <= 0.09,
			"中段缩短起手后的两帧蓄满为 43 毫秒，其他架势保留原短停")
		assert(impact_hold >= (0.015 if is_high else (0.042 if is_mid else 0.045)) and impact_hold <= (0.035 if is_high else 0.065),
			"上段短停后继续下沉；中段两帧触点约 43 毫秒，下段保留原短停")
		var recovery_seconds := seconds - float(recovery_frame) / frames.get_animation_speed(animation)
		assert(recovery_seconds >= (1.199 if is_high else 0.70) and recovery_seconds <= (1.201 if is_high else 0.90),
			"上段贯穿至回架共 1200 毫秒，其他架势保留 700 至 900 毫秒收势")
		assert(int(clip.frames) - int(clip.holds.recovery_frame) >= 12, "收势必须有连续姿势，不能只延长末帧")
		if is_high:
			var rise: Array = clip.phases.rise
			var rise_seconds := float(int(rise[1]) - int(rise[0])) / frames.get_animation_speed(animation)
			assert(rise_seconds >= 0.55 and rise_seconds <= 1.0, "上抬回架保留连续姿势且明显慢于下斩")
			strike_checks[clip.key]["rise_seconds"] = rise_seconds
			# This image-space probe checks continuing blade travel, not anatomy or physical mass.
			var contact_bottom := _high_blade_bottom(frames.get_frame_texture(animation, impact_frame).get_image())
			var follow_bottom := contact_bottom
			for pose in range(int(clip.holds.recovery_frame), int(clip.holds.recovery_frame) + 5):
				var record: Dictionary = clip.records[pose]
				follow_bottom = maxi(follow_bottom, _high_blade_bottom(frames.get_frame_texture(animation, int(record.runtime_first_frame)).get_image()))
			assert(contact_bottom >= 0 and follow_bottom - contact_bottom >= 50,
				"上段触点之后，刀锋必须继续下沉至少 50 原生像素，不能直接定住回架")
			strike_checks[clip.key]["post_contact_blade_descent_pixels"] = follow_bottom - contact_bottom
		if is_high:
			var motion: Dictionary = clip.root_motion
			var distances: Array = motion.forward_pixels
			assert(distances.size() == 64 and int(distances[0]) == 0 and int(distances[63]) == 48,
				"上段下劈有 64 个位移采样，累计前移 48 原生像素")
			for n in range(1, 64):
				assert(int(distances[n]) >= int(distances[n-1]), "收招不能向后滑回原点")
			var brake_frame := int(motion.brake_frame)
			assert(int(distances[release_frame]) == 0 and int(distances[impact_frame]) == int(distances[63]),
				"位移跟随下劈启动，触点时完成全部距离，不能在剑触地后继续前移")
			assert(brake_frame == impact_frame,
				"下劈触点即制动，之后在落点承重回架")
			for n in range(brake_frame, 64):
				assert(int(distances[n]) == int(distances[63]), "制动后保留落点，后摇不再前滑")
			strike_checks[clip.key]["root_motion_metres"] = float(distances[63]) * float(motion.pixel_size)
			strike_checks[clip.key]["advance_fraction_at_contact"] = float(distances[impact_frame]) / float(distances[63])
			strike_checks[clip.key]["advance_end_seconds"] = float(brake_frame) / frames.get_animation_speed(animation)
		elif is_mid:
			assert(not clip.has("root_motion") or clip.root_motion == null,
				"中段使用原地转髋回架，不叠加没有落脚动作的滑行位移")
			strike_checks[clip.key]["root_motion_metres"] = 0.0
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
	var report := {"ok":true,"checked_frames":checked,"runtime_frames":runtime_checked,"target_frames_per_attack":64,"frame_size":[512,384],
		"visible_rgb_and_alpha_exact":true,"one_shot":true,"uniform_runtime_timing_exact":true,"apng_frame_rounding_below_one_ms":true,"durations":durations,"strikes":strike_checks,"rhythm":rhythm_checks}
	var report_dir := "res://.godot/art-validation/greatsword_stance_attack/"
	DirAccess.make_dir_recursive_absolute(report_dir)
	var file := FileAccess.open(report_dir + "godot-validation.json",FileAccess.WRITE)
	assert(file != null)
	file.store_string(JSON.stringify(report,"  ")+"\n")
	print("ATTACK_IMPORT_CHECK: "+JSON.stringify(report))
	quit(0)

func _high_blade_bottom(image: Image) -> int:
	image.convert(Image.FORMAT_RGBA8)
	var bottom := -1
	for y in range(180, 360):
		for x in range(325, 480):
			if image.get_pixel(x, y).a > 0.0:
				bottom = maxi(bottom, y)
	return bottom
