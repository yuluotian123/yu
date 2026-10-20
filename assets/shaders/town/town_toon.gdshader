shader_type spatial;
// Keep Forward+'s PBR reflection path; Toon affects direct diffuse lighting only.
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;
#include "res://assets/shaders/weather/wet_surface.gdshaderinc"

// Offline Godot 4.6.1 DFG energy integral (128x128, linear RH).
uniform sampler2D ggx_energy_lut : hint_default_white, filter_linear, repeat_disable;
uniform float toon_strength : hint_range(0.0, 1.0) = 1.0;
uniform float toon_softness : hint_range(0.01, 2.0) = 1.0;
// Opt-in material treatment for the forest concept. Other town materials keep
// their existing native diffuse response when painterly_strength is zero.
uniform float painterly_strength : hint_range(0.0, 1.0) = 0.0;
uniform float painterly_light_wrap : hint_range(0.0, 1.5) = 0.8;
uniform float painterly_dapple_strength : hint_range(0.0, 1.0) = 0.0;
uniform float painterly_dapple_scale : hint_range(0.1, 2.0) = 0.18;
uniform float painterly_shadow_lift : hint_range(0.0, 1.0) = 0.0;
uniform float painterly_path_clearance : hint_range(0.0, 1.0) = 0.0;
uniform sampler2D painterly_atlas : source_color, filter_nearest_mipmap, repeat_disable;
varying float town_specular;
varying float town_toon_width;
varying float town_toon_amount;
varying float town_painted_mask;

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
// 基础粗糙度影响高光与干燥端点的 Toon 过渡宽度。
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
	town_toon_width = max(ROUGHNESS * toon_softness, 0.01);
	town_painted_mask = 1.0;
	if (painterly_strength > 0.0 && painterly_dapple_strength > 0.0) {
		// A painted canopy mask belongs to this surface material; it never changes
		// the actual sun, shadow maps, exposure or camera. Quantized coordinates
		// retain the reference's small stepped paint contours in world space.
		vec2 brush_position = floor((weather_position.xz + weather_position.y * vec2(0.21, 0.13)) * 40.0) / 40.0;
		vec2 tile = fract(brush_position * painterly_dapple_scale);
		vec2 mask_uv = vec2(0.753) + tile * 0.244;
		float edge_distance = min(min(tile.x, 1.0 - tile.x), min(tile.y, 1.0 - tile.y));
		float canopy_a = texture(painterly_atlas, mask_uv).r;
		float canopy_b = texture(painterly_atlas, vec2(0.753) + fract(tile + vec2(0.381, 0.271)) * 0.244).r;
		float canopy = mix(canopy_b, canopy_a, smoothstep(0.025, 0.10, edge_distance));
		float road_center = 0.8 + max(0.0, -weather_position.z) * 0.15;
		float clearing = mix(0.12, 1.0, smoothstep(1.2, 4.4, abs(weather_position.x - road_center)));
		float strength = painterly_dapple_strength * mix(1.0, clearing, painterly_path_clearance);
		town_painted_mask = mix(1.0, mix(0.08, 1.0, smoothstep(0.32, 0.72, canopy)), strength);
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
	town_toon_amount = toon_strength * (1.0 - wet.y);
	vec3 water_normal = normalize((VIEW_MATRIX * vec4(wet.z, 1.0, wet.w, 0.0)).xyz);
	NORMAL = normalize(mix(NORMAL, water_normal, wet.y * 0.96));
	ALBEDO = mix(ALBEDO, snow_surface_color(weather_position), snow);
	ROUGHNESS = mix(ROUGHNESS, weather_snow_roughness, snow);
	SPECULAR = mix(SPECULAR, 0.28, snow);
	METALLIC *= 1.0 - snow;
	town_specular = SPECULAR;
	EMISSION *= 1.0 - snow;
	world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	NORMAL = normalize((VIEW_MATRIX * vec4(snow_surface_normal(weather_position, world_normal, snow), 0.0)).xyz);
	// Forward+ reconstructs the RG normal with the same TBN path as StandardMaterial3D.
	// Puddles and snow cover the base material's small-scale normal detail.
	NORMAL_MAP = use_normal_texture ? texture(normal_texture, UV).rgb : vec3(0.5, 0.5, 1.0);
	NORMAL_MAP_DEPTH = use_normal_texture ? normal_scale * (1.0 - wet.y * 0.96) * (1.0 - snow) : 0.0;
}

float town_pow5(float value) {
	float squared = value * value;
	return squared * squared * value;
}

void light() {
	float nl = dot(NORMAL, LIGHT);
	float clamped_nl = max(nl, 0.0);
	float nv = max(dot(NORMAL, VIEW), 0.0001);
	vec3 halfway = LIGHT + VIEW;
	halfway *= inversesqrt(max(dot(halfway, halfway), 1e-12));
	float lh = clamp(dot(LIGHT, halfway), 0.0, 1.0);

	// Godot's Burley and Toon diffuse terms; albedo, metallic and AO are applied by Forward+.
	float fd90 = 2.0 * lh * lh * ROUGHNESS - 0.5;
	float burley = (1.0 + fd90 * town_pow5(1.0 - nv))
				 * (1.0 + fd90 * town_pow5(1.0 - clamped_nl)) * clamped_nl;
	float toon = smoothstep(-town_toon_width, town_toon_width, nl);
	float native_diffuse = mix(burley, toon, town_toon_amount);
	vec3 diffuse = vec3(native_diffuse) * ATTENUATION;
	if (painterly_strength > 0.0) {
		float facing = clamp((nl + painterly_light_wrap) / (1.0 + painterly_light_wrap), 0.0, 1.0);
		float middle = smoothstep(0.22, 0.32, facing);
		float bright = smoothstep(0.65, 0.73, facing);
		vec3 painted = mix(vec3(0.19, 0.28, 0.36), vec3(0.76, 0.75, 0.67), middle);
		painted = mix(painted, vec3(1.04, 1.00, 0.85), bright);
		if (LIGHT_IS_DIRECTIONAL) painted *= town_painted_mask;
		float painted_attenuation = LIGHT_IS_DIRECTIONAL ? mix(painterly_shadow_lift, 1.0, ATTENUATION) : ATTENUATION;
		diffuse = mix(diffuse, painted * painted_attenuation, painterly_strength);
	}
	DIFFUSE_LIGHT += LIGHT_COLOR * diffuse / PI;

	if (clamped_nl > 0.0 && ROUGHNESS > 0.0) {
		// GGX distribution, visibility, Schlick Fresnel and multiscatter compensation.
		vec3 f0 = mix(vec3(0.16 * town_specular * town_specular), ALBEDO, METALLIC);
		float alpha = ROUGHNESS * ROUGHNESS;
		float nh = clamp(dot(NORMAL, halfway), 0.0, 1.0);
		vec3 crossed = cross(NORMAL, halfway);
		float denom = dot(crossed, crossed) + nh * nh * alpha * alpha;
		float distribution = min(alpha * alpha / max(PI * denom * denom, 1e-20), 65504.0);
		float visibility = min(0.5 / max(mix(2.0 * clamped_nl * nv, clamped_nl + nv, alpha), 1e-12), 65504.0);
		float f90 = clamp(dot(f0, vec3(16.5)), METALLIC, 1.0);
		vec3 fresnel = f0 + (vec3(f90) - f0) * town_pow5(1.0 - lh);
		float energy = textureLod(ggx_energy_lut, vec2(nv, 1.0 - ROUGHNESS), 0.0).r;
		vec3 compensation = vec3(1.0) + f0 * (1.0 / max(energy, 0.001) - 1.0);
		SPECULAR_LIGHT += LIGHT_COLOR * ATTENUATION * SPECULAR_AMOUNT
						* (clamped_nl * distribution * visibility) * fresnel * compensation;
	}
}
