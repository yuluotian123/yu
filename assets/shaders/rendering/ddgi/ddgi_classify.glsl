#[compute]
#version 460
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;
struct Ray { vec4 radiance_distance; vec4 direction_backface; };
layout(set = 0, binding = 0, std430) readonly buffer Rays { Ray rays[]; };
layout(rgba32f, set = 0, binding = 1) uniform image2D probe_states;
layout(rg32ui, set = 0, binding = 2) uniform readonly uimage2D probe_epochs;
layout(push_constant, std430) uniform Params { vec4 origin_spacing; uvec4 counts_rays; vec4 sky_distance; uvec4 batch; vec4 options; } params;
void main() {
	uint slot = gl_GlobalInvocationID.x;
	if (slot >= params.batch.y) return;
	uint probe = (params.batch.x + slot) % (params.counts_rays.x * params.counts_rays.y * params.counts_rays.z);
	ivec2 coordinate = ivec2(probe % (params.counts_rays.x * params.counts_rays.z), probe / (params.counts_rays.x * params.counts_rays.z));
	vec4 state = imageLoad(probe_states, coordinate);
	// A settled probe only needs reclassification when traced geometry changes.
	if (state.w > .5 && imageLoad(probe_epochs, coordinate).y == floatBitsToUint(params.options.z)) return;
	uint backfaces = 0;
	float front_distance = params.sky_distance.w;
	float back_distance = params.sky_distance.w;
	vec3 front_direction = vec3(0.0), back_direction = vec3(0.0);
	uint fixed_count = min(32u, params.counts_rays.w);
	for (uint ray_id = 0; ray_id < fixed_count; ray_id++) {
		Ray ray = rays[slot * params.counts_rays.w + ray_id];
		if (ray.direction_backface.w > 0.5) {
			backfaces++;
			if (ray.radiance_distance.w < back_distance) { back_distance = ray.radiance_distance.w; back_direction = ray.direction_backface.xyz; }
		} else if (ray.radiance_distance.w < front_distance) {
			front_distance = ray.radiance_distance.w; front_direction = ray.direction_backface.xyz;
		}
	}
	float clearance = params.origin_spacing.w * 0.12;
	bool inside = float(backfaces) / float(fixed_count) > 0.25;
	vec3 shift = vec3(0.0);
	if (inside) shift = back_direction * (back_distance + clearance);
	else if (front_distance < clearance) shift = -front_direction * (clearance - front_distance);
	vec3 relocated = clamp(state.xyz + shift, vec3(-0.45 * params.origin_spacing.w), vec3(0.45 * params.origin_spacing.w));
	bool moved = length(relocated - state.xyz) > params.origin_spacing.w * 0.005;
	// Moved probes are invalid for this update; only the next trace uses their new position.
	imageStore(probe_states, coordinate, vec4(relocated, (!inside && !moved) ? 1.0 : 0.0));
}
