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
	vec4 paint; // integer pixel width, first block origin.xy
} params;

void main() {
	int width = int(params.paint.x);
	ivec2 cell = ivec2(gl_GlobalInvocationID.xy) * width + ivec2(params.paint.yz);
	ivec2 pixel = max(ivec2(0),cell);
	ivec2 size = ivec2(params.settings.xy);
	if (any(greaterThanEqual(pixel, size))) return;
	ivec2 extent = min(cell + width, size) - pixel;
	vec2 uv = (vec2(pixel) + vec2(extent)*0.5) / params.settings.xy;
	// Weight similar samples around the cell centre to keep silhouettes and thin props legible.
	// Each invocation reads only its own cell before broadcasting the final colour.
	vec4 original = imageLoad(scene_color, pixel + extent / 2);
	if (width > 1) {
		vec3 center = original.rgb;
		vec3 sum = vec3(0.0);
		float total = 0.0;
		float alpha = 0.0;
		for (int y = 0; y < extent.y; ++y) {
			for (int x = 0; x < extent.x; ++x) {
				vec4 sample_color = imageLoad(scene_color, pixel + ivec2(x, y));
				vec3 delta = abs(sample_color.rgb - center) / (max(sample_color.rgb, center) + vec3(0.02));
				float weight = 1.0 - 0.9 * smoothstep(0.12, 0.55, max(delta.r, max(delta.g, delta.b)));
				sum += sample_color.rgb * weight;
				total += weight;
				alpha += sample_color.a;
			}
		}
		original = vec4(sum / total, alpha / float(extent.x * extent.y));
	}
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
	if (width > 1) {
		// A gentle value ramp groups neighbouring cells without changing the hue.
		float value = sqrt(max(result.r, max(result.g, result.b)));
		float stepped = mix(value, round(value * 40.0) / 40.0, 0.65);
		result *= stepped * stepped / max(value * value, 1e-8);
	}
	// Leave linear output in the HDR buffer. Godot's Linear tonemapper performs the one sRGB conversion.
	for (int y=0; y<extent.y; y++)
		for (int x=0; x<extent.x; x++)
			imageStore(scene_color,pixel+ivec2(x,y),vec4(result,original.a));
}
