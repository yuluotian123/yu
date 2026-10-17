#[compute]
#version 460
layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;
#include "res://assets/shaders/rendering/ddgi/ddgi_emissive.glslinc"
struct Ray { vec4 radiance_distance; vec4 direction_backface; };
layout(set = 0, binding = 0, std430) readonly buffer Rays { Ray rays[]; };
layout(rgba16f, set = 0, binding = 1) uniform image2D irradiance_atlas;
layout(rg32f, set = 0, binding = 2) uniform image2D distance_atlas;
layout(rgba32f, set = 0, binding = 3) uniform readonly image2D probe_states;
layout(rg32ui, set = 0, binding = 4) uniform readonly uimage2D probe_epochs;
layout(set = 0, binding = 5, std430) readonly buffer TemporalData {
	uvec4 control; vec4 metrics; vec4 previous_sky[32];
} temporal;
layout(set = 0, binding = 6, std430) readonly buffer VisibilityRays { Ray visibility_rays[]; };
layout(set = 0, binding = 7, std430) readonly buffer EmissiveSamples { EmissiveSample emissive_samples[]; };
layout(set = 0, binding = 8, std430) readonly buffer SamplingLayout { uvec4 sample_layout; };
layout(push_constant, std430) uniform Params { vec4 origin_spacing; uvec4 counts_rays; vec4 sky_distance; uvec4 batch; vec4 options; } params;

vec3 texel_direction(ivec2 texel, int resolution) {
	// Mirrored octahedral seam padding, including the four corners.
	texel -= ivec2(1);
	if (texel.x < 0) { texel.x = 0; texel.y = resolution - 1 - texel.y; }
	else if (texel.x >= resolution) { texel.x = resolution - 1; texel.y = resolution - 1 - texel.y; }
	if (texel.y < 0) { texel.y = 0; texel.x = resolution - 1 - texel.x; }
	else if (texel.y >= resolution) { texel.y = resolution - 1; texel.x = resolution - 1 - texel.x; }
	vec2 oct = (vec2(texel) + vec2(0.5)) / float(resolution) * 2.0 - 1.0;
	vec3 direction = vec3(oct, 1.0 - abs(oct.x) - abs(oct.y));
	if (direction.z < 0.0) direction.xy = (1.0 - abs(direction.yx)) * vec2(direction.x >= 0.0 ? 1.0 : -1.0, direction.y >= 0.0 ? 1.0 : -1.0);
	return normalize(direction);
}
void main() {
	ivec2 texel = ivec2(gl_GlobalInvocationID.xy);
	uint slot = gl_GlobalInvocationID.z;
	if (any(greaterThanEqual(texel, ivec2(18))) || slot >= params.batch.y) return;
	uint probe = (params.batch.x + slot) % (params.counts_rays.x * params.counts_rays.y * params.counts_rays.z);
	ivec2 tile = ivec2(probe % (params.counts_rays.x * params.counts_rays.z), probe / (params.counts_rays.x * params.counts_rays.z));
	bool valid = imageLoad(probe_states, tile).w > 0.5;
	uvec2 epochs = imageLoad(probe_epochs, tile).xy;
	ivec2 distance_pixel = tile * 18 + texel;
	vec3 distance_direction = texel_direction(texel, 16);
	vec3 distance_sum = vec3(0.0);
	vec3 irradiance_sum = vec3(0.0);
	float irradiance_weight = 0.0;
	bool write_irradiance = all(lessThan(texel, ivec2(10)));
	vec3 irradiance_direction = write_irradiance ? texel_direction(texel, 8) : vec3(0.0);
	for (uint ray_id = 0; ray_id < params.counts_rays.w; ray_id++) {
		Ray ray = rays[slot * params.counts_rays.w + ray_id];
		if (write_irradiance && (params.counts_rays.w <= 32u || ray_id >= 32u)) {
			float weight = max(dot(irradiance_direction, ray.direction_backface.xyz), 0.0);
			irradiance_sum += ray.radiance_distance.rgb * weight;
			irradiance_weight += weight;
		}
	}
	float history = params.options.x;
	if (write_irradiance) {
		ivec2 pixel = tile * 10 + texel;
		vec4 previous = imageLoad(irradiance_atlas, pixel);
		history *= previous.a > 0.5 && valid && epochs.x == temporal.control.x ? 1.0 : 0.0;
		vec3 value = irradiance_sum / max(irradiance_weight, 1e-6);
		uint count = sample_layout.y;
		for (uint id = 0; valid && id < count; id++) {
			EmissiveSample sample_data = emissive_samples[slot * count + id];
			if (max(sample_data.radiance.r, max(sample_data.radiance.g, sample_data.radiance.b)) <= 0.0) continue;
			value += sample_data.radiance.rgb * ddgi_emissive_form_factor(sample_data, irradiance_direction);
		}
		imageStore(irradiance_atlas, pixel, vec4(mix(value, previous.rgb, history), valid ? 1.0 : 0.0));
	}
	// Visibility is geometry data: fixed rays, rebuilt only after geometry/relocation changes.
	if (!valid) imageStore(distance_atlas, distance_pixel, vec4(0.0));
	else if (epochs.y != floatBitsToUint(params.options.z)) {
		uint count = sample_layout.x;
		for (uint id = 0; id < count; id++) {
			Ray ray = visibility_rays[slot * count + id];
			float weight = pow(max(dot(distance_direction, ray.direction_backface.xyz), 0.0), 32.0);
			float distance_to_hit = ray.radiance_distance.w;
			distance_sum += vec3(distance_to_hit, distance_to_hit * distance_to_hit, 1.0) * weight;
		}
		imageStore(distance_atlas, distance_pixel, vec4(distance_sum.xy / max(distance_sum.z, 1e-6), 0.0, 0.0));
	}
}
