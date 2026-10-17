// Shared by spatial receivers and the GPU validation sampler. Stores irradiance / PI.
vec2 ddgi_oct_encode(vec3 direction) {
	direction /= max(abs(direction.x) + abs(direction.y) + abs(direction.z), 1e-7);
	vec2 encoded = direction.xy;
	if (direction.z < 0.0) encoded = (1.0 - abs(encoded.yx)) * vec2(encoded.x >= 0.0 ? 1.0 : -1.0, encoded.y >= 0.0 ? 1.0 : -1.0);
	return encoded * 0.5 + 0.5;
}

vec2 ddgi_atlas_uv(ivec3 index, ivec3 counts, vec3 direction, int resolution) {
	ivec2 tile = ivec2(index.x + index.z * counts.x, index.y);
	vec2 size = vec2(float(counts.x * counts.z), float(counts.y)) * float(resolution + 2);
	return (vec2(tile * (resolution + 2)) + vec2(1.0) + ddgi_oct_encode(direction) * float(resolution)) / size;
}

vec4 ddgi_sample(sampler2D irradiance_tex, sampler2D distance_tex, sampler2D state_tex,
		vec3 origin, float spacing, ivec3 counts, vec3 position, vec3 normal, vec3 view_direction,
		float normal_bias, float view_bias) {
	vec3 grid = (position - origin) / spacing;
	// Outside the volume preserves the receiver's ambient fallback.
	if (any(lessThan(grid, vec3(0.0))) || any(greaterThan(grid, vec3(counts - ivec3(1))))) return vec4(0.0);
	ivec3 base = clamp(ivec3(floor(grid)), ivec3(0), counts - ivec3(2));
	vec3 fraction = clamp(grid - vec3(base), vec3(0.0), vec3(1.0));
	vec3 biased_position = position + normal * normal_bias + view_direction * view_bias;
	vec3 sum = vec3(0.0);
	float weight_sum = 0.0;
	for (int corner = 0; corner < 8; corner++) {
		ivec3 bit = ivec3(corner & 1, (corner >> 1) & 1, (corner >> 2) & 1);
		ivec3 index = base + bit;
		vec4 state = texelFetch(state_tex, ivec2(index.x + index.z * counts.x, index.y), 0);
		if (state.w < 0.5) continue;
		vec3 probe_position = origin + vec3(index) * spacing + state.xyz;
		vec3 delta = biased_position - probe_position;
		float distance_to_probe = max(length(delta), 1e-5);
		vec3 direction = delta / distance_to_probe;
		vec2 moments = textureLod(distance_tex, ddgi_atlas_uv(index, counts, direction, 16), 0.0).rg;
		float variance = max(moments.y - moments.x * moments.x, spacing * spacing * 0.0001);
		float difference = max(distance_to_probe - moments.x, 0.0);
		float visibility = variance / (variance + difference * difference);
		visibility = visibility * visibility * visibility;
		vec3 trilinear = mix(vec3(1.0) - fraction, fraction, vec3(bit));
		float facing = max(0.05, pow(max(0.0, dot(-direction, normal) * 0.5 + 0.5), 2.0));
		float weight = max(trilinear.x * trilinear.y * trilinear.z, 1e-6) * facing * max(visibility, 1e-5);
		// Crush weak visibility to prevent normalization from restoring occluded probes.
		if (visibility < 0.2) weight *= visibility * visibility / 0.04;
		sum += textureLod(irradiance_tex, ddgi_atlas_uv(index, counts, normal, 8), 0.0).rgb * weight;
		weight_sum += weight;
	}
	if (weight_sum < 1e-7) return vec4(0.0, 0.0, 0.0, 1.0);
	return vec4(sum / weight_sum, 1.0);
}
