shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_disabled;
#include "res://assets/shaders/weather/wet_surface.gdshaderinc"

// StandardMaterial3D 的附加雪层，原来的贴图与材质完整保留在下面。
void vertex() {
	VERTEX += NORMAL * 0.001;
	weather_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	if (!FRONT_FACING) { NORMAL = -NORMAL; }
	vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
	float snow = snow_surface_sample(weather_position, world_normal);
	if (snow <= 0.001) { discard; }
	ALBEDO = snow_surface_color(weather_position);
	ALPHA = snow;
	ROUGHNESS = weather_snow_roughness;
	SPECULAR = 0.28;
	NORMAL = normalize((VIEW_MATRIX * vec4(snow_surface_normal(weather_position, world_normal, snow), 0.0)).xyz);
}
