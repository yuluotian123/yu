#[compute]
#version 450
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
layout(rgba16f, set = 0, binding = 0) uniform image2D scene_color;
layout(set = 0, binding = 1) uniform sampler2D scene_depth;
layout(set = 0, binding = 2) uniform sampler3D near_lut;
layout(set = 0, binding = 3) uniform sampler3D far_lut;
layout(push_constant, std430) uniform Params {
	vec4 settings; // size.xy, exposure, captured exposure
	vec4 ranges;
	vec4 options; // LUT strength, grade sky
	mat4 inverse_projection;
} params;

// Event 24905: PQ is the LUT's coordinate encoding, not the display output encoding.
vec3 encode_pq(vec3 hdr) {
	vec3 p = pow(max(hdr * 0.01, vec3(0.0)), vec3(0.1593017578125));
	return pow((vec3(0.8359375) + 18.8515625 * p) / (vec3(1.0) + 18.6875 * p), vec3(78.84375));
}

void main() {
	ivec2 pixel = ivec2(gl_GlobalInvocationID.xy);
	if (any(greaterThanEqual(pixel, ivec2(params.settings.xy)))) return;
	float z = texelFetch(scene_depth, pixel, 0).r;
	if (z <= 0.000001 && params.options.y < 0.5) return;
	vec2 uv = (vec2(pixel) + 0.5) / params.settings.xy;
	vec4 position = params.inverse_projection * vec4(uv * 2.0 - 1.0, z, 1.0);
	float depth = -position.z / position.w;
	vec4 original = imageLoad(scene_color, pixel);
	vec3 exposed = max(original.rgb * params.settings.z, vec3(0.0));
	vec3 coord = encode_pq(exposed / params.settings.w);
	// The source uses normalized coordinates directly; do not add an extra half-texel correction.
	vec3 grade;
	float weight;
	if (depth <= params.ranges.y) {
		grade = textureLod(near_lut, coord, 0.0).rgb;
		weight = 1.0 - smoothstep(params.ranges.x, params.ranges.y, depth);
	} else {
		grade = textureLod(far_lut, coord, 0.0).rgb;
		weight = smoothstep(params.ranges.z, params.ranges.w, depth);
	}
	// Captured LUT data is scene-linear HDR, with values well above 1.
	vec3 result = mix(exposed, grade * params.settings.w, weight * params.options.x);
	imageStore(scene_color, pixel, vec4(result / params.settings.z, original.a));
}
