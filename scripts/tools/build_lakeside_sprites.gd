extends SceneTree

# Build the runtime atlas from imagegen artwork without changing the drawn poses.
const ROOT := "res://assets/textures/sprites/lakeside_greatsword/"
const RESOURCE_ROOT := "res://assets/resources/sprites/lakeside_greatsword/"
const FRAME_SIZE := 512
const ORIGIN := Vector2i(256, 448)
const BODY_HEIGHT := 288.0
const ATLAS_COLUMNS := 8
# Airborne frames share a torso anchor; folding the legs must not move the body.
const JUMP_CAPE_Y := [280.0, 288.0, 276.0, 260.0, 268.0, 260.0, 260.0, 260.0, 260.0, 260.0, 260.0, 260.0]
const SOURCES := [
	{"name": "support", "count": 12, "columns": 4, "rows": 3, "reference": 0},
	{"name": "run", "count": 8, "columns": 4, "rows": 2, "reference": 0},
	{"name": "attack", "count": 16, "columns": 4, "rows": 4, "reference": 0},
	{"name": "jump", "count": 16, "columns": 4, "rows": 4, "reference": 15}
]

func _initialize() -> void:
	var atlas := Image.create(FRAME_SIZE * ATLAS_COLUMNS, FRAME_SIZE * 7, false, Image.FORMAT_RGBA8)
	atlas.fill(Color.TRANSPARENT)
	var records: Dictionary = {}
	var frame_index := 0
	for spec in SOURCES:
		var source := Image.new()
		if source.load(ProjectSettings.globalize_path(ROOT + "sources/" + spec.name + ".png")) != OK:
			_fail("Missing source: " + spec.name)
			return
		source.convert(Image.FORMAT_RGBA8)
		var segmented := _segment(source, spec.count)
		if segmented.is_empty():
			return
		var parts: Array = segmented.parts
		parts.sort_custom(func(a, b):
			var row_a := clampi(int(a.box.get_center().y * spec.rows / source.get_height()), 0, spec.rows - 1)
			var row_b := clampi(int(b.box.get_center().y * spec.rows / source.get_height()), 0, spec.rows - 1)
			return a.box.get_center().x < b.box.get_center().x if row_a == row_b else row_a < row_b)
		var reference: Dictionary = parts[spec.reference]
		var top := _warm_top(source, reference.box, segmented.labels, reference.id)
		var body_height: float = reference.box.end.y - 1 - top
		var factor: float = BODY_HEIGHT / body_height
		for i in range(parts.size()):
			var part: Dictionary = parts[i]
			var box: Rect2i = part.box
			var crop := Image.create(box.size.x, box.size.y, false, Image.FORMAT_RGBA8)
			var pixels: PackedByteArray = source.get_data()
			var labels: PackedInt32Array = segmented.labels
			var clean := PackedByteArray()
			clean.resize(box.size.x * box.size.y * 4)
			for y in range(box.size.y):
				for x in range(box.size.x):
					var source_index: int = (y + box.position.y) * source.get_width() + x + box.position.x
					if labels[source_index] == part.id:
						var destination_index := (y * box.size.x + x) * 4
						for channel in range(4):
							clean[destination_index + channel] = pixels[source_index * 4 + channel]
			crop = Image.create_from_data(box.size.x, box.size.y, false, Image.FORMAT_RGBA8, clean)
			var cape := _cape_anchor(source, box, labels, part.id)
			var anchor := Vector2(cape.x + body_height * 0.04, box.end.y - 1)
			crop.resize(maxi(1, roundi(box.size.x * factor)), maxi(1, roundi(box.size.y * factor)), Image.INTERPOLATE_LANCZOS)
			var foot_lift := 0
			if spec.name == "run" and i in [3, 7]:
				foot_lift = 16
			var position := Vector2i(roundi(ORIGIN.x - (anchor.x - box.position.x) * factor), roundi(ORIGIN.y - foot_lift - (anchor.y - box.position.y) * factor))
			if spec.name == "jump" and i < 12:
				position.y = roundi(JUMP_CAPE_Y[i] - (cape.y - box.position.y) * factor)
				foot_lift = roundi(ORIGIN.y - position.y - (anchor.y - box.position.y) * factor)
			if position.x < 8 or position.y < 8 or position.x + crop.get_width() >= FRAME_SIZE - 8 or position.y + crop.get_height() >= FRAME_SIZE - 8:
				_fail("Frame exceeds atlas gutters: " + spec.name + "_" + str(i))
				return
			var frame := Image.create(FRAME_SIZE, FRAME_SIZE, false, Image.FORMAT_RGBA8)
			frame.fill(Color.TRANSPARENT)
			frame.blit_rect(crop, Rect2i(Vector2i.ZERO, crop.get_size()), position)
			var frame_name: String = spec.name + "_" + str(i)
			if spec.name == "support":
				frame_name = ["idle", "hurt", "death"][i / 4] + "_" + str(i % 4)
			var tile := Vector2i(frame_index % ATLAS_COLUMNS, frame_index / ATLAS_COLUMNS)
			atlas.blit_rect(frame, Rect2i(Vector2i.ZERO, frame.get_size()), tile * FRAME_SIZE)
			records[frame_name] = {"tile": [tile.x, tile.y], "source_box": [box.position.x, box.position.y, box.size.x, box.size.y], "source_anchor": [anchor.x, anchor.y], "scale": factor, "foot_lift": foot_lift, "bounds": [position.x, position.y, crop.get_width(), crop.get_height()]}
			frame_index += 1
		print(spec.name, ": ", parts.size(), " frames; body reference ", body_height, "; scale ", factor)
	if atlas.save_png(ROOT + "atlas.png") != OK:
		_fail("Could not save atlas")
		return
	DirAccess.make_dir_recursive_absolute(RESOURCE_ROOT)
	var metadata := FileAccess.open(RESOURCE_ROOT + "alignment.json", FileAccess.WRITE)
	metadata.store_string(JSON.stringify({"frame_size": FRAME_SIZE, "origin": [ORIGIN.x, ORIGIN.y], "base_pixel_size": 0.0025, "visual_scale": 1.3, "frames": records}, "\t"))
	_write_frames(records)
	# Neutral-background review output is diagnostic, not a runtime asset.
	var review := Image.create(atlas.get_width(), atlas.get_height(), false, Image.FORMAT_RGBA8)
	review.fill(Color(0.86, 0.89, 0.86, 1.0))
	review.blend_rect(atlas, Rect2i(Vector2i.ZERO, atlas.get_size()), Vector2i.ZERO)
	DirAccess.make_dir_recursive_absolute("res://.godot/art-validation/sprites")
	review.save_png("res://.godot/art-validation/sprites/atlas-review.png")
	print("Built ", frame_index, " distinct artwork frames and 11 compatible animation clips.")
	quit(0)

func _segment(source: Image, expected: int) -> Dictionary:
	var width := source.get_width()
	var height := source.get_height()
	var pixels := source.get_data()
	var labels := PackedInt32Array()
	labels.resize(width * height)
	labels.fill(-1)
	var parts: Array = []
	var next_id := 0
	for index in range(width * height):
		if labels[index] >= 0 or pixels[index * 4 + 3] < 16:
			continue
		var pending := PackedInt32Array([index])
		labels[index] = next_id
		var cursor := 0
		var min_x := width
		var min_y := height
		var max_x := 0
		var max_y := 0
		while cursor < pending.size():
			var point := pending[cursor]
			cursor += 1
			var x := point % width
			var y := point / width
			min_x = mini(min_x, x)
			max_x = maxi(max_x, x)
			min_y = mini(min_y, y)
			max_y = maxi(max_y, y)
			for neighbor in [point - width if y > 0 else -1, point + width if y + 1 < height else -1, point - 1 if x > 0 else -1, point + 1 if x + 1 < width else -1]:
				if neighbor < 0 or labels[neighbor] >= 0 or pixels[neighbor * 4 + 3] < 16:
					continue
				labels[neighbor] = next_id
				pending.append(neighbor)
		if pending.size() >= 256:
			parts.append({"id": next_id, "area": pending.size(), "box": Rect2i(min_x, min_y, max_x - min_x + 1, max_y - min_y + 1)})
		next_id += 1
	parts.sort_custom(func(a, b): return a.area > b.area)
	if parts.size() < expected:
		_fail("Expected " + str(expected) + " isolated poses; found " + str(parts.size()))
		return {}
	if parts.size() > expected and parts[expected].area > parts[expected - 1].area * 0.25:
		_fail("Artwork has an unexpected large disconnected fragment")
		return {}
	parts.resize(expected)
	return {"parts": parts, "labels": labels}

func _warm_top(source: Image, box: Rect2i, labels: PackedInt32Array, part_id: int) -> int:
	var top := box.end.y - 1
	var center := _cape_anchor(source, box, labels, part_id).x
	var half_width := box.size.y * 0.22
	for y in range(box.position.y, box.end.y):
		var count := 0
		for x in range(maxi(box.position.x, int(center - half_width)), mini(box.end.x, int(center + half_width))):
			if labels[y * source.get_width() + x] != part_id:
				continue
			var color := source.get_pixel(x, y)
			if color.a > 0.5 and color.r > color.b * 1.25 and color.r > 0.2 and color.g > color.b * 0.9:
				count += 1
		if count >= 5:
			top = y
			break
	return top

func _cape_anchor(source: Image, box: Rect2i, labels: PackedInt32Array, part_id: int) -> Vector2:
	var total := Vector2.ZERO
	var count := 0
	for y in range(box.position.y, box.end.y):
		for x in range(box.position.x, box.end.x):
			if labels[y * source.get_width() + x] != part_id:
				continue
			var color := source.get_pixel(x, y)
			if color.a > 0.5 and color.g > color.r * 1.03 and color.g > color.b * 1.15:
				total += Vector2(x, y)
				count += 1
	return total / count if count > 0 else Vector2(box.get_center())

func _write_frames(records: Dictionary) -> void:
	var text := "[gd_resource type=\"SpriteFrames\" format=3]\n\n[ext_resource type=\"Texture2D\" path=\"res://assets/textures/sprites/lakeside_greatsword/atlas.png\" id=\"1_atlas\"]\n"
	for frame_name in records:
		var tile: Array = records[frame_name].tile
		text += "\n[sub_resource type=\"AtlasTexture\" id=\"%s\"]\natlas = ExtResource(\"1_atlas\")\nregion = Rect2(%d, %d, %d, %d)\nfilter_clip = true\n" % [frame_name, tile[0] * FRAME_SIZE, tile[1] * FRAME_SIZE, FRAME_SIZE, FRAME_SIZE]
	var clips := [
		{"name": "idle", "frames": _sequence("idle", 0, 4), "fps": 5.0, "loop": true},
		{"name": "run", "frames": _sequence("run", 0, 8), "fps": 12.0, "loop": true},
		{"name": "attack", "frames": _sequence("attack", 0, 16), "fps": 100.0, "loop": false},
		{"name": "jump", "frames": ["jump_0", "jump_1", "jump_2", "jump_4", "jump_3"] + _sequence("jump", 5, 11), "fps": 24.0, "loop": false},
		{"name": "jumpup", "frames": ["jump_0", "jump_1", "jump_2", "jump_4", "jump_3"], "fps": 20.0, "loop": false},
		{"name": "inair", "frames": _sequence("jump", 5, 4), "fps": 24.0, "loop": false},
		{"name": "isfalling", "frames": _sequence("jump", 9, 3), "fps": 16.0, "loop": false},
		{"name": "land", "frames": _sequence("jump", 12, 4), "fps": 20.0, "loop": false},
		{"name": "dash", "frames": ["run_3", "run_7", "run_3", "run_7"], "fps": 16.0, "loop": false},
		{"name": "hurt", "frames": _sequence("hurt", 0, 4), "fps": 10.0, "loop": false},
		{"name": "death", "frames": _sequence("death", 0, 4), "fps": 6.0, "loop": false}
	]
	text += "\n[resource]\nanimations = [\n"
	for clip_index in range(clips.size()):
		var clip: Dictionary = clips[clip_index]
		text += "{\n\"frames\": ["
		for i in range(clip.frames.size()):
			var duration := 1.0
			if clip.name == "attack":
				duration = [4.5, 3.5, 3.0, 4.0][i / 4]
			text += "{\"duration\": %s, \"texture\": SubResource(\"%s\")}%s" % [str(duration), clip.frames[i], ", " if i + 1 < clip.frames.size() else ""]
		text += "],\n\"loop\": %s,\n\"name\": &\"%s\",\n\"speed\": %s\n}%s\n" % ["true" if clip.loop else "false", clip.name, str(clip.fps), "," if clip_index + 1 < clips.size() else ""]
	text += "]\n"
	var file := FileAccess.open(RESOURCE_ROOT + "sprite_frames.tres", FileAccess.WRITE)
	file.store_string(text)

func _sequence(prefix: String, first: int, count: int) -> Array:
	var names: Array = []
	for i in range(first, first + count):
		names.append(prefix + "_" + str(i))
	return names

func _fail(message: String) -> void:
	push_error(message)
	quit(1)
