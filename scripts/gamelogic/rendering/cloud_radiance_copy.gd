@tool
extends RefCounted

# Native callbacks survive a managed reload. Only RIDs cross this boundary;
# no scene-local ViewportTexture or node path is stored in receiving materials.
var sources: Array
var targets: Array
var owned: Array[RID] = []
var device: RenderingDevice
var disposed := false
var pending := false

func request_copy() -> void:
	if disposed or pending:
		return
	pending = true
	RenderingServer.frame_post_draw.connect(_after_draw, CONNECT_ONE_SHOT)

func _after_draw() -> void:
	pending = false
	if not disposed:
		RenderingServer.call_on_render_thread(_copy.bind(self))

func _copy(_keep_alive: RefCounted) -> void:
	if disposed:
		return
	device = RenderingServer.get_rendering_device()
	if device == null:
		return
	if owned.size() != sources.size():
		owned.resize(sources.size())
	for face in range(sources.size()):
		var source := RenderingServer.texture_get_rd_texture(sources[face])
		if not source.is_valid() or not device.texture_is_valid(source):
			continue
		if not owned[face].is_valid():
			# Render targets already belong to RenderingServer; wrapping them again
			# is rejected. Copy on the GPU into a texture owned by this helper.
			var format := device.texture_get_format(source)
			format.texture_type = RenderingDevice.TEXTURE_TYPE_2D
			format.array_layers = 1
			format.mipmaps = 1
			format.usage_bits = RenderingDevice.TEXTURE_USAGE_SAMPLING_BIT \
				| RenderingDevice.TEXTURE_USAGE_CAN_COPY_TO_BIT | RenderingDevice.TEXTURE_USAGE_CAN_COPY_FROM_BIT
			var texture := device.texture_create(format, RDTextureView.new())
			if not texture.is_valid():
				return
			owned[face] = texture
			targets[face].texture_rd_rid = texture
		var size := device.texture_get_format(owned[face])
		device.texture_copy(source, owned[face], Vector3.ZERO, Vector3.ZERO,
			Vector3(size.width, size.height, 1), 0, 0, 0, 0)

func release() -> void:
	disposed = true
	if RenderingServer.frame_post_draw.is_connected(_after_draw):
		RenderingServer.frame_post_draw.disconnect(_after_draw)
	RenderingServer.call_on_render_thread(_release.bind(self))

func _release(_keep_alive: RefCounted) -> void:
	for texture in targets:
		texture.texture_rd_rid = RID()
	targets.clear()
	if device != null:
		for texture in owned:
			if texture.is_valid():
				device.free_rid(texture)
	owned.clear()
	sources.clear()
	device = null
