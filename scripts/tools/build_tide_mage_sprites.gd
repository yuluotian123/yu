extends SceneTree

# Import the authored poses onto one native pixel grid; never smooth pixel artwork.
var texture_root := "res://assets/textures/sprites/tide_mage/"
var resource_root := "res://assets/resources/sprites/tide_mage/"
var review_root := "res://.godot/art-validation/tide-mage/"
var character_height := 112.0
var use_warm_head_anchor := false
var attack_duration := 0.6
var frame_size := 192
var origin := Vector2i(96, 160)
const COLUMNS := 8
const SOURCES := [
	{"name": "support", "count": 12, "rows": 3, "reference": 0},
	{"name": "run", "count": 8, "rows": 2, "reference": 0},
	{"name": "attack", "count": 8, "rows": 2, "reference": 0},
	{"name": "jump", "count": 12, "rows": 3, "reference": 11}
]
var _labels := PackedInt32Array()
var _major_ids: Array[int] = []

func _initialize() -> void:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--character="):
			var character := argument.trim_prefix("--character=")
			if not character.is_valid_identifier() or character.begins_with("_"):
				_fail("Character must be a plain resource folder name")
				return
			texture_root = "res://assets/textures/sprites/" + character + "/"
			resource_root = "res://assets/resources/sprites/" + character + "/"
			review_root = "res://.godot/art-validation/" + character + "/"
		elif argument.begins_with("--height="):
			var value := argument.trim_prefix("--height=")
			if not value.is_valid_int() or int(value) < 64 or int(value) > 144:
				_fail("Character height must be between 64 and 144 native pixels")
				return
			character_height = float(value)
		elif argument.begins_with("--frame-size="):
			var value := argument.trim_prefix("--frame-size=")
			if not value.is_valid_int() or int(value) < 192 or int(value) > 256:
				_fail("Frame size must be between 192 and 256 pixels")
				return
			frame_size = int(value)
		elif argument == "--warm-head":
			use_warm_head_anchor = true
		elif argument.begins_with("--attack-duration="):
			var value := argument.trim_prefix("--attack-duration=")
			if not value.is_valid_float() or not is_finite(float(value)) or float(value) < 0.1 or float(value) > 3.0:
				_fail("Attack duration must be between 0.1 and 3 seconds")
				return
			attack_duration = float(value)
		else:
			_fail("Unknown sprite build argument: " + argument)
			return
	origin = Vector2i(frame_size / 2, frame_size - 32)
	DirAccess.make_dir_recursive_absolute(resource_root)
	var atlas := Image.create(frame_size * COLUMNS, frame_size * 5, false, Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	var records: Dictionary = {}
	var frame_index := 0
	for spec in SOURCES:
		var source := Image.load_from_file(texture_root + "sources/" + spec.name + ".png")
		if source == null or source.is_empty():
			_fail("Missing sprite source: " + spec.name)
			return
		source.convert(Image.FORMAT_RGBA8)
		var parts := _find_poses(source, spec.count)
		if parts.size() != spec.count:
			return
		parts.sort_custom(func(part_a: Dictionary, part_b: Dictionary):
			var a: Rect2i = part_a.box
			var b: Rect2i = part_b.box
			var row_a := clampi(int(a.get_center().y * spec.rows / source.get_height()), 0, spec.rows - 1)
			var row_b := clampi(int(b.get_center().y * spec.rows / source.get_height()), 0, spec.rows - 1)
			return a.get_center().x < b.get_center().x if row_a == row_b else row_a < row_b)
		var reference: Rect2i = parts[spec.reference].box
		# Raised weapons add transparent-frame height, not extra character height.
		var reference_body_top := _body_top(source.get_region(reference))
		var factor := character_height / (reference.size.y - reference_body_top)
		for i in range(parts.size()):
			var box: Rect2i = parts[i].box
			var crop := source.get_region(box)
			# Poses may have overlapping bounding boxes even while their pixels are separate.
			for y in range(box.size.y):
				for x in range(box.size.x):
					var label := _labels[(box.position.y + y) * source.get_width() + box.position.x + x]
					if label != parts[i].id and label in _major_ids:
						crop.set_pixel(x, y, Color.TRANSPARENT)
			var anchor_x := _head_center(crop) if spec.name != "support" or i < 8 else box.size.x * 0.5
			crop.resize(maxi(1, roundi(box.size.x * factor)), maxi(1, roundi(box.size.y * factor)), Image.INTERPOLATE_NEAREST)
			# An opaque/transparent boundary prevents colored fringes and partial pixels.
			var pixels := crop.get_data()
			for p in range(0, pixels.size(), 4):
				if pixels[p + 3] < 128:
					pixels[p] = 0
					pixels[p + 1] = 0
					pixels[p + 2] = 0
					pixels[p + 3] = 0
				else:
					pixels[p + 3] = 255
			crop = Image.create_from_data(crop.get_width(), crop.get_height(), false, Image.FORMAT_RGBA8, pixels)
			var position := Vector2i(roundi(origin.x - anchor_x * factor), origin.y - crop.get_height() + 1)
			if spec.name == "jump" and i >= 1 and i <= 7:
				position.y = origin.y - int(character_height) + 1 - _body_top(crop)
			if spec.name == "run" and i in [3, 7]:
				position.y -= 2
			if position.x < 4 or position.y < 4 or position.x + crop.get_width() > frame_size - 4 or position.y + crop.get_height() > frame_size - 4:
				_fail("Pose crosses atlas gutter: %s_%d at %s, size %s" % [spec.name, i, position, crop.get_size()])
				return
			var frame_name: String = spec.name + "_" + str(i)
			if spec.name == "support":
				frame_name = ["idle", "hurt", "death"][i / 4] + "_" + str(i % 4)
			var tile := Vector2i(frame_index % COLUMNS, frame_index / COLUMNS)
			atlas.blit_rect(crop, Rect2i(Vector2i.ZERO, crop.get_size()), tile * frame_size + position)
			records[frame_name] = {"tile": [tile.x, tile.y], "source": spec.name, "source_box": [box.position.x, box.position.y, box.size.x, box.size.y], "scale": factor, "bounds": [position.x, position.y, crop.get_width(), crop.get_height()]}
			frame_index += 1
		print(spec.name, ": ", parts.size(), " poses; reference height ", reference.size.y, "; scale ", factor)
	if atlas.save_png(texture_root + "atlas.png") != OK:
		_fail("Could not save sprite atlas")
		return
	var metadata := FileAccess.open(resource_root + "alignment.json", FileAccess.WRITE)
	metadata.store_string(JSON.stringify({"frame_size": frame_size, "character_height": character_height, "attack_duration": attack_duration, "origin": [origin.x, origin.y], "base_pixel_size": 0.0075, "visual_scale": 1.3, "frames": records}, "\t"))
	_write_frames(records)
	_write_import_options()
	# Review output is disposable and stays out of the source asset tree.
	var review := Image.create(atlas.get_width(), atlas.get_height(), false, Image.FORMAT_RGBA8)
	review.fill(Color(0.38, 0.4, 0.46, 1.0))
	review.blend_rect(atlas, Rect2i(Vector2i.ZERO, atlas.get_size()), Vector2i.ZERO)
	review.resize(atlas.get_width() * 2, atlas.get_height() * 2, Image.INTERPOLATE_NEAREST)
	DirAccess.make_dir_recursive_absolute(review_root)
	review.save_png(review_root + "atlas-review.png")
	var idle := atlas.get_region(Rect2i(0, 0, frame_size, frame_size))
	idle = idle.get_region(idle.get_used_rect())
	idle.save_png(texture_root + "idle-native.png")
	idle.resize(idle.get_width() * 6, idle.get_height() * 6, Image.INTERPOLATE_NEAREST)
	idle.save_png(review_root + "idle-preview.png")
	print("PIXEL_CHARACTER_ATLAS_COMPLETE frames=", frame_index, " clips=11 height=", character_height)
	quit(0)

func _find_poses(source: Image, expected: int) -> Array[Dictionary]:
	var width := source.get_width()
	var height := source.get_height()
	var pixels := source.get_data()
	_labels.resize(width * height)
	_labels.fill(-1)
	_major_ids.clear()
	var components: Array[Dictionary] = []
	var next_id := 0
	for index in range(width * height):
		if _labels[index] >= 0 or pixels[index * 4 + 3] < 128:
			continue
		var pending := PackedInt32Array([index])
		_labels[index] = next_id
		var cursor := 0
		var low := Vector2i(width, height)
		var high := Vector2i.ZERO
		while cursor < pending.size():
			var point := pending[cursor]
			cursor += 1
			var x := point % width
			var y := point / width
			low = Vector2i(mini(low.x, x), mini(low.y, y))
			high = Vector2i(maxi(high.x, x), maxi(high.y, y))
			for neighbor in [point - width if y > 0 else -1, point + width if y + 1 < height else -1, point - 1 if x > 0 else -1, point + 1 if x + 1 < width else -1]:
				if neighbor >= 0 and _labels[neighbor] < 0 and pixels[neighbor * 4 + 3] >= 128:
					_labels[neighbor] = next_id
					pending.append(neighbor)
		if pending.size() >= 256:
			components.append({"id": next_id, "area": pending.size(), "box": Rect2i(low, high - low + Vector2i.ONE)})
		next_id += 1
	components.sort_custom(func(a, b): return a.area > b.area)
	if components.size() < expected or (components.size() > expected and components[expected].area > components[expected - 1].area * 0.3):
		_fail("Expected %d isolated poses; found %d major fragments" % [expected, components.size()])
		return []
	var result: Array[Dictionary] = []
	for i in range(expected):
		result.append(components[i])
		_major_ids.append(components[i].id)
	return result

func _head_center(image: Image) -> float:
	var total := 0.0
	var count := 0
	var start := 0 if use_warm_head_anchor else roundi(image.get_height() * 0.08)
	var end := roundi(image.get_height() * (0.65 if use_warm_head_anchor else 0.3))
	for y in range(start, end):
		for x in range(image.get_width()):
			var color := image.get_pixel(x, y)
			var skin := color.a > 0.5 and color.r > color.g * 1.08 and color.r > color.b * 1.08 and color.g > 0.45 and color.b > 0.45
			if use_warm_head_anchor:
				skin = color.a > 0.5 and color.r > color.g * 1.12 and color.r > color.b * 1.12 and color.g > 0.5 and color.b > 0.52
			if skin:
				total += x
				count += 1
	return total / count if count > 0 else image.get_width() * 0.6

func _body_top(image: Image) -> int:
	if not use_warm_head_anchor:
		return 0
	# Warm fur/skin identifies the head while excluding gray blades and dark outlines.
	for y in range(image.get_height()):
		var count := 0
		for x in range(image.get_width()):
			var color := image.get_pixel(x, y)
			if color.a > 0.5 and color.r > 0.2 and color.r > color.g * 1.12 and color.r > color.b * 1.4:
				count += 1
		if count >= 3:
			return y
	return 0

func _write_frames(records: Dictionary) -> void:
	var text := "[gd_resource type=\"SpriteFrames\" format=3]\n\n[ext_resource type=\"Texture2D\" path=\"" + texture_root + "atlas.png\" id=\"1_atlas\"]\n"
	for frame_name in records:
		var tile: Array = records[frame_name].tile
		text += "\n[sub_resource type=\"AtlasTexture\" id=\"%s\"]\natlas = ExtResource(\"1_atlas\")\nregion = Rect2(%d, %d, %d, %d)\nfilter_clip = true\n" % [frame_name, tile[0] * frame_size, tile[1] * frame_size, frame_size, frame_size]
	var clips := [
		{"name": "idle", "frames": _sequence("idle", 0, 4), "fps": 5.0, "loop": true},
		{"name": "run", "frames": _sequence("run", 0, 8), "fps": 12.0, "loop": true},
		{"name": "attack", "frames": _sequence("attack", 0, 8), "fps": 8.0 / attack_duration, "loop": false},
		{"name": "jump", "frames": _sequence("jump", 0, 12), "fps": 20.0, "loop": false},
		{"name": "jumpup", "frames": _sequence("jump", 0, 4), "fps": 20.0, "loop": false},
		{"name": "inair", "frames": _sequence("jump", 4, 2), "fps": 12.0, "loop": false},
		{"name": "isfalling", "frames": _sequence("jump", 6, 2), "fps": 12.0, "loop": false},
		{"name": "land", "frames": _sequence("jump", 8, 4), "fps": 20.0, "loop": false},
		{"name": "dash", "frames": ["run_3", "run_7", "run_3", "run_7"], "fps": 16.0, "loop": false},
		{"name": "hurt", "frames": _sequence("hurt", 0, 4), "fps": 10.0, "loop": false},
		{"name": "death", "frames": _sequence("death", 0, 4), "fps": 6.0, "loop": false}
	]
	text += "\n[resource]\nanimations = [\n"
	for clip_index in range(clips.size()):
		var clip: Dictionary = clips[clip_index]
		text += "{\n\"frames\": ["
		for i in range(clip.frames.size()):
			text += "{\"duration\": 1.0, \"texture\": SubResource(\"%s\")}%s" % [clip.frames[i], ", " if i + 1 < clip.frames.size() else ""]
		text += "],\n\"loop\": %s,\n\"name\": &\"%s\",\n\"speed\": %s\n}%s\n" % ["true" if clip.loop else "false", clip.name, str(clip.fps), "," if clip_index + 1 < clips.size() else ""]
	text += "]\n"
	FileAccess.open(resource_root + "sprite_frames.tres", FileAccess.WRITE).store_string(text)

func _write_import_options() -> void:
	# Set both first-import and rebuild settings without replacing an existing UID.
	var path := texture_root + "atlas.png.import"
	var config := ConfigFile.new()
	config.load(path)
	config.set_value("remap", "importer", "texture")
	config.set_value("remap", "type", "CompressedTexture2D")
	config.set_value("deps", "source_file", texture_root + "atlas.png")
	config.set_value("params", "compress/mode", 0)
	config.set_value("params", "mipmaps/generate", false)
	config.set_value("params", "process/fix_alpha_border", false)
	config.set_value("params", "detect_3d/compress_to", 0)
	config.save(path)

func _sequence(prefix: String, first: int, count: int) -> Array:
	var names: Array = []
	for i in range(first, first + count):
		names.append(prefix + "_" + str(i))
	return names

func _fail(message: String) -> void:
	push_error(message)
	quit(1)
