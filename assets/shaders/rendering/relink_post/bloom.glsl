#[compute]
#version 450
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(set = 0, binding = 0) uniform sampler2D source_color;
layout(rgba16f, set = 0, binding = 1) uniform writeonly image2D destination;
layout(push_constant, std430) uniform Params {
	vec4 settings; // destination size.xy, mode (0 extract, 1 downsample, 2/3 blur), exposure
	vec4 bloom; // threshold, floor
} params;

vec3 film(vec3 x) {
	return max(((x * (0.15 * x + 0.05) + 0.004) / (x * (0.15 * x + 0.50) + 0.060) - 0.066667) * 1.379064, vec3(0.0));
}

void main() {
	ivec2 pixel = ivec2(gl_GlobalInvocationID.xy);
	if (any(greaterThanEqual(pixel, ivec2(params.settings.xy)))) return;
	vec2 uv = (vec2(pixel) + 0.5) / params.settings.xy;
	vec2 texel = 1.0 / vec2(textureSize(source_color, 0));
	int mode = int(params.settings.z);
	vec3 color;
	if (mode < 2) {
		// Four bilinear samples reproduce the observed downsample structure.
		color = (textureLod(source_color, uv + texel * vec2(-0.5, -0.5), 0.0).rgb
			+ textureLod(source_color, uv + texel * vec2(0.5, -0.5), 0.0).rgb
			+ textureLod(source_color, uv + texel * vec2(-0.5, 0.5), 0.0).rgb
			+ textureLod(source_color, uv + texel * vec2(0.5, 0.5), 0.0).rgb) * 0.25;
		if (mode == 0) {
			vec3 hdr = max(color * params.settings.w, vec3(0.0));
			float brightness = max(hdr.r, max(hdr.g, hdr.b));
			float selection = smoothstep(params.bloom.x * 0.75, params.bloom.x * 1.25, brightness);
			// Tone the glow before blurring; final glow is added AFTER the film curve, as in event 31660.
			color = film(hdr) * max(selection, params.bloom.y);
		}
	} else {
		vec2 direction = mode == 2 ? vec2(texel.x, 0.0) : vec2(0.0, texel.y);
		// Event 31204: nine bilinear taps. Normalize its 1.40828 gain for the artist's intensity control.
		const float weights[5] = float[5](0.591721058, 0.358538479, 0.048491772, 0.001243023, 0.000005719);
		const float offsets[5] = float[5](0.0, 1.339243650, 3.174285412, 5.079845905, 7.034445286);
		color = textureLod(source_color, uv, 0.0).rgb * weights[0];
		for (int i = 1; i < 5; ++i) {
			color += (textureLod(source_color, uv + direction * offsets[i], 0.0).rgb
				+ textureLod(source_color, uv - direction * offsets[i], 0.0).rgb) * weights[i];
		}
		color /= 1.408279044;
	}
	imageStore(destination, pixel, vec4(color, 1.0));
}
