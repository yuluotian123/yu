#[compute]
#version 450
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(rgba16f, set = 0, binding = 0) uniform image2D scene_color;
layout(set = 0, binding = 1) uniform sampler2D bloom0;
layout(set = 0, binding = 2) uniform sampler2D bloom1;
layout(set = 0, binding = 3) uniform sampler2D bloom2;
layout(set = 0, binding = 4) uniform sampler2D bloom3;
layout(set = 0, binding = 5) uniform sampler2D bloom4;
layout(set = 0, binding = 6) uniform sampler2D scene_depth;
struct SpritePlane { vec4 bounds; vec4 depth; };
layout(std430, set = 0, binding = 7) readonly buffer CharacterPlanes { SpritePlane sprites[]; };
layout(push_constant, std430) uniform Params {
	vec4 settings; // size.xy, exposure, bloom intensity
	vec4 paint; // integer pixel width, first block origin.xy, visible sprite count
} params;

vec3 film_color(vec3 color, vec2 uv, bool pixelated) {
	vec3 x = clamp(color * params.settings.z, vec3(0.0), vec3(65504.0));
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
	if (pixelated) {
		// A gentle value ramp groups neighbouring cells without changing the hue.
		float value = sqrt(max(result.r, max(result.g, result.b)));
		float stepped = mix(value, round(value * 40.0) / 40.0, 0.65);
		result *= stepped * stepped / max(value * value, 1e-8);
	}
	return result;
}

// Alpha-tested sprites and depth-writing detail particles retain their fine pixels.
// Transparent corners and walls in front still use the background grid.
bool contains_character(ivec2 pixel, ivec2 extent) {
	vec2 ndc_step = 2.0 / params.settings.xy;
	vec2 lo = (vec2(pixel) + 0.5) * ndc_step - 1.0;
	vec2 hi = lo + vec2(extent - 1) * ndc_step;
	for (int i = 0; i < int(params.paint.w); i++) {
		SpritePlane sprite = sprites[i];
		if (any(lessThan(hi, sprite.bounds.xy)) || any(greaterThan(lo, sprite.bounds.zw))) continue;
		for (int y = 0; y < extent.y; y++) {
			for (int x = 0; x < extent.x; x++) {
				vec2 ndc = lo + vec2(x, y) * ndc_step;
				if (any(lessThan(ndc, sprite.bounds.xy)) || any(greaterThan(ndc, sprite.bounds.zw))) continue;
				float expected = dot(sprite.depth.xy, ndc) + sprite.depth.z;
				float actual = texelFetch(scene_depth, pixel + ivec2(x, y), 0).r;
				// Orthographic reverse-Z stays near 1: a percentage allowance would admit walls metres away.
				float tolerance = max(2e-7, abs(expected) * 2e-6) + dot(abs(sprite.depth.xy), ndc_step) + sprite.depth.w;
				if (actual > 0.0 && abs(actual - expected) <= tolerance) return true;
			}
		}
	}
	return false;
}

void main() {
	int width = int(params.paint.x);
	ivec2 cell = ivec2(gl_GlobalInvocationID.xy) * width + ivec2(params.paint.yz);
	ivec2 pixel = max(ivec2(0),cell);
	ivec2 size = ivec2(params.settings.xy);
	if (any(greaterThanEqual(pixel, size))) return;
	ivec2 extent = min(cell + width, size) - pixel;
	if (width > 1 && contains_character(pixel, extent)) {
		for (int y = 0; y < extent.y; y++) {
			for (int x = 0; x < extent.x; x++) {
				ivec2 at = pixel + ivec2(x, y);
				vec4 source = imageLoad(scene_color, at);
				vec2 sample_uv = (vec2(at) + 0.5) / params.settings.xy;
				imageStore(scene_color, at, vec4(film_color(source.rgb, sample_uv, false), source.a));
			}
		}
		return;
	}
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
	vec3 result = film_color(original.rgb, uv, width > 1);
	// Leave linear output in the HDR buffer. Godot's Linear tonemapper performs the one sRGB conversion.
	for (int y=0; y<extent.y; y++)
		for (int x=0; x<extent.x; x++)
			imageStore(scene_color,pixel+ivec2(x,y),vec4(result,original.a));
}
