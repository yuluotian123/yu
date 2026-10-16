extends SceneTree

func _initialize() -> void:
	var destination := "res://assets/textures/rendering/relink_post"
	DirAccess.make_dir_recursive_absolute(destination)
	for name in ["g_NearLUT", "g_FarLUT"]:
		var bytes := FileAccess.get_file_as_bytes("res://.godot/relink-analysis/" + name + ".rgba16f")
		if bytes.size() != 32 * 32 * 32 * 8:
			push_error("Missing/invalid raw LUT: " + name)
			quit(1)
			return
		var slices: Array[Image] = []
		for z in range(32):
			slices.append(Image.create_from_data(32, 32, false, Image.FORMAT_RGBAH,
				bytes.slice(z * 8192, (z + 1) * 8192)))
		var lut := ImageTexture3D.new()
		var error := lut.create(Image.FORMAT_RGBAH, 32, 32, 32, false, slices)
		if error == OK:
			# A real RenderingDevice is required. Dummy/headless and Compatibility can change float data.
			var roundtrip := PackedByteArray()
			for slice: Image in lut.get_data():
				roundtrip.append_array(slice.get_data())
			if roundtrip != bytes:
				push_error("Use the project's Forward+ renderer to preserve the captured half floats: " + name)
				quit(1)
				return
			error = ResourceSaver.save(lut, destination + "/" + name + ".res")
		if error != OK:
			push_error("LUT conversion failed: " + name)
			quit(1)
			return
		print("[Relink] Saved lossless RGBA16F Texture3D: ", name)
	quit()
