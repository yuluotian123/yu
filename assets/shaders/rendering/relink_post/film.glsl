#[compute]
#version 450
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(rgba16f, set = 0, binding = 0) uniform image2D scene_color;
layout(set = 0, binding = 1) uniform sampler2D bloom0;
layout(set = 0, binding = 2) uniform sampler2D bloom1;
layout(set = 0, binding = 3) uniform sampler2D bloom2;
layout(set = 0, binding = 4) uniform sampler2D bloom3;
layout(set = 0, binding = 5) uniform sampler2D bloom4;
layout(push_constant, std430) uniform Params {
	vec4 settings; // size.xy, exposure, bloom intensity
} params;

void main() {
	ivec2 pixel = ivec2(gl_GlobalInvocationID.xy);
	if (any(greaterThanEqual(pixel, ivec2(params.settings.xy)))) return;
	vec2 uv = (vec2(pixel) + 0.5) / params.settings.xy;
	vec4 original = imageLoad(scene_color, pixel);
	vec3 x = clamp(original.rgb * params.settings.z, vec3(0.0), vec3(65504.0));
	vec3 film = (x * (0.15 * x + 0.05) + 0.004) / (x * (0.15 * x + 0.50) + 0.060) - 0.066667;
	vec3 glow = vec3(0.0);
	if (params.settings.w > 0.0) {
		glow = textureLod(bloom0, uv, 0.0).rgb * 0.10
			+ textureLod(bloom1, uv, 0.0).rgb * 0.20
			+ textureLod(bloom2, uv, 0.0).rgb * 0.40
			+ textureLod(bloom3, uv, 0.0).rgb * 0.20
			+ textureLod(bloom4, uv, 0.0).rgb * 0.10;
	}
	vec3 result = clamp(film * 1.379064 + glow * params.settings.w, vec3(0.0), vec3(1.0));
	// Leave linear output in the HDR buffer. Godot's Linear tonemapper performs the one sRGB conversion.
	imageStore(scene_color, pixel, vec4(result, original.a));
}
