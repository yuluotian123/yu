extends SceneTree
## GPU regression: distant sky misses must not dilute local occlusion moments.

var rd: RenderingDevice
var owned: Array[RID] = []

func _initialize() -> void:
	run.call_deferred()

func keep(rid: RID) -> RID:
	assert(rid.is_valid())
	owned.append(rid)
	return rid

func storage(binding: int, rid: RID) -> RDUniform:
	var uniform := RDUniform.new()
	uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_STORAGE_BUFFER
	uniform.binding = binding
	uniform.add_id(rid)
	return uniform

func image_uniform(binding: int, rid: RID) -> RDUniform:
	var uniform := RDUniform.new()
	uniform.uniform_type = RenderingDevice.UNIFORM_TYPE_IMAGE
	uniform.binding = binding
	uniform.add_id(rid)
	return uniform

func buffer(data: PackedByteArray) -> RID:
	return keep(rd.storage_buffer_create(data.size(),data))

func texture(width: int, height: int, format: int, initial: PackedByteArray = PackedByteArray()) -> RID:
	var description := RDTextureFormat.new()
	description.width = width
	description.height = height
	description.format = format
	description.usage_bits = RenderingDevice.TEXTURE_USAGE_STORAGE_BIT | RenderingDevice.TEXTURE_USAGE_CAN_COPY_FROM_BIT | RenderingDevice.TEXTURE_USAGE_CAN_COPY_TO_BIT | RenderingDevice.TEXTURE_USAGE_CAN_UPDATE_BIT
	var data: Array[PackedByteArray] = []
	if not initial.is_empty(): data.append(initial)
	var rid := keep(rd.texture_create(description,RDTextureView.new(),data))
	if initial.is_empty(): rd.texture_clear(rid,Color(0,0,0,0),0,1,0,1)
	return rid

func rays(near_hit: float, far_hit: float) -> PackedByteArray:
	# Equal directions give a known two-sample distribution in the forward texels.
	return PackedFloat32Array([.25,.5,1,near_hit, 0,0,1,0, .25,.5,1,far_hit, 0,0,1,0]).to_byte_array()

func run() -> void:
	rd = RenderingServer.create_local_rendering_device()
	assert(rd != null,"Run with Vulkan/Forward+, without --headless")
	var file: RDShaderFile = load("res://assets/shaders/rendering/ddgi/ddgi_integrate.glsl")
	var spirv := file.get_spirv()
	assert(spirv.compile_error_compute.is_empty(),spirv.compile_error_compute)
	var shader := keep(rd.shader_create_from_spirv(spirv))
	var pipeline := keep(rd.compute_pipeline_create(shader))
	var radiance := buffer(rays(.5,100))
	var visibility := buffer(rays(.5,100))
	var irradiance := texture(10,10,RenderingDevice.DATA_FORMAT_R16G16B16A16_SFLOAT)
	var distances := texture(18,18,RenderingDevice.DATA_FORMAT_R32G32_SFLOAT)
	var state := texture(1,1,RenderingDevice.DATA_FORMAT_R32G32B32A32_SFLOAT,PackedFloat32Array([0,0,0,1]).to_byte_array())
	var epochs := texture(1,1,RenderingDevice.DATA_FORMAT_R32G32_UINT,PackedInt32Array([0,0]).to_byte_array())
	var temporal_data := PackedByteArray()
	temporal_data.resize(544)
	var temporal := buffer(temporal_data)
	var unused_emission := PackedByteArray()
	unused_emission.resize(80)
	var uniforms: Array[RDUniform] = [storage(0,radiance),image_uniform(1,irradiance),image_uniform(2,distances),
		image_uniform(3,state),image_uniform(4,epochs),storage(5,temporal),storage(6,visibility),
		storage(7,buffer(unused_emission)),storage(8,buffer(PackedInt32Array([2,0,0,0]).to_byte_array()))]
	var uniform_set := keep(rd.uniform_set_create(uniforms,shader,0))
	var constants := PackedFloat32Array([0,0,0,2.6]).to_byte_array()
	constants.append_array(PackedInt32Array([1,1,1,2]).to_byte_array())
	constants.append_array(PackedFloat32Array([0,0,0,100]).to_byte_array())
	constants.append_array(PackedInt32Array([0,1,0,0]).to_byte_array())
	constants.append_array(PackedInt32Array([0,1,1,0]).to_byte_array())
	var local_limit := 2.6 * sqrt(3.0) * 1.5
	var previous := Vector2.ZERO
	for far_hit: float in [1.0,10.0,100.0]:
		var data := rays(.5,far_hit)
		rd.buffer_update(visibility,0,data.size(),data)
		var list := rd.compute_list_begin()
		rd.compute_list_bind_compute_pipeline(list,pipeline)
		rd.compute_list_bind_uniform_set(list,uniform_set,0)
		rd.compute_list_set_push_constant(list,constants,constants.size())
		rd.compute_list_dispatch(list,3,3,1)
		rd.compute_list_end()
		rd.submit()
		rd.sync()
		var result := rd.texture_get_data(distances,0)
		var address := (9*18+9)*8
		var moments := Vector2(result.decode_float(address),result.decode_float(address+4))
		var capped := minf(far_hit,local_limit)
		var expected := Vector2((.5+capped)*.5,(.25+capped*capped)*.5)
		assert(moments.distance_to(expected) < .001,"Incorrect visibility moments: %s / %s" % [moments,expected])
		if far_hit == 100.0:
			assert(moments.distance_to(previous) < .001,"Extending a distant miss changed local visibility")
		previous = moments
		print("DDGI_VISIBILITY_PASS far=",far_hit," mean/second_moment=",moments)
	var radiance_image := Image.create_from_data(10,10,false,Image.FORMAT_RGBAH,rd.texture_get_data(irradiance,0))
	var color := radiance_image.get_pixel(5,5)
	assert(absf(color.r-.25) < .001 and absf(color.g-.5) < .001 and absf(color.b-1) < .001,"Visibility correction changed constant irradiance")
	for i in range(owned.size()-1,-1,-1): rd.free_rid(owned[i])
	rd.free()
	print("DDGI_VISIBILITY_VALIDATED: local occlusion is independent of distant miss length; radiance preserved")
	quit()
