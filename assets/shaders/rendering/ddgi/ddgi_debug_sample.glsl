#[compute]
#version 460
layout(local_size_x = 64) in;
layout(set = 0, binding = 0) uniform sampler2D irradiance_tex;
layout(set = 0, binding = 1) uniform sampler2D distance_tex;
layout(set = 0, binding = 2) uniform sampler2D state_tex;
struct Sample { vec4 position; vec4 normal; };
layout(set = 0, binding = 3, std430) readonly buffer Samples { Sample samples[]; };
layout(set = 0, binding = 4, std430) writeonly buffer Results { vec4 results[]; };
layout(push_constant, std430) uniform Params { vec4 origin_spacing; uvec4 counts_samples; vec4 biases; } params;
#include "ddgi_sampling.gdshaderinc"
void main() {
	uint id = gl_GlobalInvocationID.x;
	if (id >= params.counts_samples.w) return;
	results[id] = ddgi_sample(irradiance_tex, distance_tex, state_tex, params.origin_spacing.xyz, params.origin_spacing.w,
		ivec3(params.counts_samples.xyz), samples[id].position.xyz, normalize(samples[id].normal.xyz), vec3(0.0), params.biases.x, 0.0);
}
