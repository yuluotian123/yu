#[compute]
#version 460
layout(local_size_x = 32) in;
layout(set = 0, binding = 0) uniform sampler2D environment_sky;
layout(set = 0, binding = 1, std430) readonly buffer SkyData {
	vec4 basis_x; vec4 basis_y; vec4 basis_z; vec4 options;
} sky;
layout(set = 0, binding = 2, std430) buffer TemporalData {
	uvec4 control; // radiance epoch, CPU lighting epoch, initialized, sky active
	vec4 metrics; // sky relative change, discontinuity, unused, unused
	vec4 previous_sky[32];
} temporal;
layout(rg32ui, set = 0, binding = 3) uniform writeonly uimage2D probe_epochs;
layout(rgba32f, set = 0, binding = 4) uniform readonly image2D probe_states;
layout(push_constant, std430) uniform Params {
	vec4 origin_spacing; uvec4 counts_rays; vec4 sky_distance; uvec4 batch;
	vec4 options; // history, CPU lighting epoch bits, visibility epoch bits, commit
} params;
#include "res://assets/shaders/rendering/ddgi/ddgi_sky.glslinc"
shared float changes[32];
shared float energies[32];

void main() {
	uint id = gl_GlobalInvocationID.x;
	if (params.options.w > .5) {
		if (id >= params.batch.y) return;
		uint probe = (params.batch.x + id) % (params.counts_rays.x * params.counts_rays.y * params.counts_rays.z);
		ivec2 tile = ivec2(probe % (params.counts_rays.x * params.counts_rays.z), probe / (params.counts_rays.x * params.counts_rays.z));
		bool valid = imageLoad(probe_states, tile).w > .5;
		imageStore(probe_epochs, tile, uvec4(temporal.control.x, valid ? floatBitsToUint(params.options.z) : 0u, 0, 0));
		return;
	}
	// Compare world radiance, never exposure normalization or a sky bake revision.
	float y = 1.0 - 2.0 * (float(id) + .5) / 32.0;
	float angle = float(id) * 2.39996322973;
	vec3 direction = vec3(cos(angle) * sqrt(1.0 - y * y), y, sin(angle) * sqrt(1.0 - y * y));
	vec3 current = ddgi_sky_radiance(environment_sky, direction, params.sky_distance.rgb,
		mat3(sky.basis_x.xyz, sky.basis_y.xyz, sky.basis_z.xyz), sky.options);
	vec3 previous = temporal.previous_sky[id].rgb;
	changes[id] = dot(current - previous, current - previous);
	energies[id] = max(dot(current, current), dot(previous, previous));
	temporal.previous_sky[id] = vec4(current, 0.0);
	barrier();
	if (id == 0) {
		float change = 0.0, energy = 0.0;
		for (int i = 0; i < 32; i++) { change += changes[i]; energy += energies[i]; }
		float relative = sqrt(change / max(energy, 1e-10));
		uint cpu_epoch = floatBitsToUint(params.options.y);
		uint sky_active = uint(sky.options.x > .5);
		bool reset = temporal.control.z == 0u || temporal.control.y != cpu_epoch
			|| temporal.control.w != sky_active || relative > .3;
		if (reset) temporal.control.x++;
		temporal.control.yzw = uvec3(cpu_epoch, 1, sky_active);
		temporal.metrics = vec4(relative, reset ? 1.0 : 0.0, 0.0, 0.0);
	}
}
