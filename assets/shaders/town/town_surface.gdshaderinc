// 底色保留原模型配色。纹理均值与细节混合，减弱照片式碎纹理；不改变 UV。
uniform vec4 base_color : source_color = vec4(1.0);
uniform bool use_texture = false;
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 texture_average = vec3(0.4);
uniform float texture_detail : hint_range(0.0, 1.0) = 0.62;
uniform bool use_vertex_color = false;
uniform float color_saturation : hint_range(0.0, 2.0) = 1.02;
uniform vec2 uv_scale = vec2(1.0);
uniform vec2 uv_offset = vec2(0.0);
uniform vec3 emission_color = vec3(0.0);
// Linear RGB tint. In luminance mode intensity is cd/m² (nits), independent of exposure.
uniform bool emission_luminance_units = false;
uniform float emission_intensity : hint_range(0.0, 30000.0, 10.0) = 1.0;
// Toon 与地面 PBR 共用材质输入；光照模型由入口 Shader 选择。
uniform float surface_roughness : hint_range(0.0, 1.0) = 1.0;
uniform float surface_metallic : hint_range(0.0, 1.0) = 0.0;
uniform float specular_strength : hint_range(0.0, 1.0) = 0.5;
uniform bool use_orm_texture = false;
// Linear data: R = ambient occlusion, G = roughness, B = metallic.
uniform sampler2D orm_texture : hint_default_white, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float ao_light_affect : hint_range(0.0, 1.0) = 0.0;
uniform bool use_normal_texture = false;
uniform sampler2D normal_texture : hint_normal, filter_linear_mipmap_anisotropic, repeat_enable;
uniform float normal_scale : hint_range(0.0, 4.0) = 1.0;

void vertex() {
	weather_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	UV = UV * uv_scale + uv_offset;
}

void fragment() {
	vec3 color = base_color.rgb;
	if (use_texture) color *= mix(texture_average, texture(albedo_texture, UV).rgb, texture_detail);
	if (use_vertex_color) color *= COLOR.rgb;
	float grey = dot(color, vec3(0.2126, 0.7152, 0.0722));
	ALBEDO = clamp(mix(vec3(grey), color, color_saturation), vec3(0.0), vec3(1.0));
	ROUGHNESS = surface_roughness;
	SPECULAR = specular_strength;
	METALLIC = surface_metallic;
	AO = 1.0;
	AO_LIGHT_AFFECT = ao_light_affect;
	if (use_orm_texture) {
		vec3 orm = texture(orm_texture, UV).rgb;
		AO = orm.r;
		ROUGHNESS *= orm.g;
		METALLIC *= orm.b;
	}
	EMISSION = emission_color * emission_intensity;
	if (emission_luminance_units) EMISSION /= max(dot(emission_color, vec3(0.2126, 0.7152, 0.0722)), 1e-6);
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float snow = snow_surface_sample(weather_position, world_normal);
	vec4 wet = wet_surface_sample(weather_position, world_normal);
	wet *= 1.0 - snow;
	// 水底石材比薄水膜更暗，维持俯视时的水岸对比；倒影仍由原生反射计算。
	ALBEDO *= (1.0 - wet.x * weather_darkening) * (1.0 - wet.y * 0.35);
	float film = wet_film_amount(wet.x);
	ROUGHNESS = mix(ROUGHNESS, min(ROUGHNESS, weather_wet_roughness), film);
	ROUGHNESS = mix(ROUGHNESS, weather_puddle_roughness, wet.y);
	SPECULAR = mix(SPECULAR, weather_wet_specular, max(film, wet.y));
	METALLIC *= 1.0 - wet.y;
	vec3 water_normal = normalize((VIEW_MATRIX * vec4(wet.z, 1.0, wet.w, 0.0)).xyz);
	NORMAL = normalize(mix(NORMAL, water_normal, wet.y * 0.96));
	ALBEDO = mix(ALBEDO, snow_surface_color(weather_position), snow);
	ROUGHNESS = mix(ROUGHNESS, weather_snow_roughness, snow);
	SPECULAR = mix(SPECULAR, 0.28, snow);
	METALLIC *= 1.0 - snow;
	EMISSION *= 1.0 - snow;
	world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	NORMAL = normalize((VIEW_MATRIX * vec4(snow_surface_normal(weather_position, world_normal, snow), 0.0)).xyz);
	// Forward+ reconstructs the RG normal with the same TBN path as StandardMaterial3D.
	// Puddles and snow cover the base material's small-scale normal detail.
	NORMAL_MAP = use_normal_texture ? texture(normal_texture, UV).rgb : vec3(0.5, 0.5, 1.0);
	NORMAL_MAP_DEPTH = use_normal_texture ? normal_scale * (1.0 - wet.y * 0.96) * (1.0 - snow) : 0.0;
}
