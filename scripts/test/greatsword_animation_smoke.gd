extends SceneTree

# godot --headless --path . --script res://scripts/test/greatsword_animation_smoke.gd
func _initialize() -> void:
	call_deferred("run")

func check(value: bool, message: String) -> void:
	if not value:
		push_error("[GreatswordAnimationSmoke] FAIL: " + message)
		quit(1)
		assert(value, message)

func run() -> void:
	var frames: SpriteFrames = load("res://assets/sprites/greatsword/sprite_frames.tres")
	check(frames != null, "Cannot load new SpriteFrames")
	var previous: SpriteFrames = load("res://assets/sprites/rika_sprite_frames.tres")
	for animation in previous.get_animation_names():
		check(frames.has_animation(animation), "Missing existing animation: " + animation)
	check(frames.has_animation("hurt") and frames.has_animation("death"), "Missing reaction clips")
	for animation in frames.get_animation_names():
		check(frames.get_frame_count(animation) > 0, "Empty animation: " + animation)
		for index in frames.get_frame_count(animation):
			var texture: AtlasTexture = frames.get_frame_texture(animation, index)
			check(texture != null and texture.get_size() == Vector2(192, 192), "Invalid frame canvas")
			check(texture.atlas.resource_path == "res://assets/sprites/greatsword/atlas.png", "Old character texture leaked")
			var pixels: Image = texture.get_image()
			var used := pixels.get_used_rect()
			check(used.has_area() and used.position.x > 0 and used.position.y > 0 and used.end.x < 192 and used.end.y < 192, "Clipped or empty sprite")
			check(pixels.get_pixel(0, 0).a == 0.0, "Frame is not transparent")
	var scene: PackedScene = load("res://assets/scenes/player.tscn")
	var player := scene.instantiate()
	var player_sprite: AnimatedSprite2D = player.get_node("VisualRoot/AnimatedSprite2D")
	check(player_sprite.sprite_frames == frames, "Player still uses old sprite resource")
	check(player_sprite.position == Vector2(0, -36), "Player foot alignment mismatch")
	check(player.get_node("PhysicsBody/CollisionShape2D").shape.size == Vector2(36, 72), "Collision shape changed")
	player.free()
	var sprite := AnimatedSprite2D.new()
	sprite.sprite_frames = frames
	root.add_child(sprite)
	for animation in ["attack", "dash", "jump", "land", "hurt", "death"]:
		sprite.play(animation)
		var duration := 0.0
		for index in frames.get_frame_count(animation):
			duration += frames.get_frame_duration(animation, index) / frames.get_animation_speed(animation)
		await create_timer(duration + 0.08).timeout
		check(not sprite.is_playing(), animation + " did not finish")
		check(sprite.frame == frames.get_frame_count(animation)-1, animation + " did not hold its last frame")
	for animation in ["idle", "run", "inair", "isfalling"]:
		sprite.play(animation)
		await create_timer(0.12).timeout
		check(sprite.is_playing(), animation + " loop stopped")
	sprite.queue_free()
	print("[GreatswordAnimationSmoke] PASS: 11 clips, atlas bounds/alpha, Player reference, collisions, loops and one-shots")
	quit(0)
