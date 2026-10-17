#[compute]
#version 460
#extension GL_EXT_ray_query : require
layout(local_size_x = 64, local_size_y = 1, local_size_z = 1) in;
layout(set = 0, binding = 0) uniform accelerationStructureEXT scene;
#include "res://assets/shaders/weather/surface_patterns.gdshaderinc"
#include "res://assets/shaders/rendering/ddgi/ddgi_material.glslinc"
#include "res://assets/shaders/rendering/ddgi/ddgi_sky.glslinc"
#include "res://assets/shaders/rendering/ddgi/ddgi_emissive.glslinc"
struct Ray { vec4 radiance_distance; vec4 direction_backface; };
layout(set = 0, binding = 1, std430) readonly buffer Triangles { Triangle triangles[]; };
layout(set = 0, binding = 2, std430) readonly buffer Instances { uvec4 instances[]; };
layout(set = 0, binding = 3, std430) readonly buffer Materials { Material materials[]; };
layout(set = 0, binding = 4) uniform sampler2DArray albedo_textures;
layout(set = 0, binding = 5, std430) writeonly buffer Rays { Ray rays[]; };
layout(rgba32f, set = 0, binding = 6) uniform readonly image2D probe_states;
struct Light { vec4 direction; vec4 radiance; };
layout(set = 0, binding = 7, std430) readonly buffer Lights { Light lights[]; };
layout(set = 0, binding = 8) uniform sampler2D environment_sky;
layout(set = 0, binding = 9, std430) readonly buffer SkyData {
	vec4 basis_x; vec4 basis_y; vec4 basis_z; vec4 options; // enabled, normalization, UV border, unused
} sky;
layout(set = 0, binding = 10) uniform sampler2DArray material_data_textures;
layout(rg32ui, set = 0, binding = 11) uniform readonly uimage2D probe_epochs;
layout(set = 0, binding = 12, std430) readonly buffer Emitters { uvec4 emitter_count; EmissiveTriangle emitters[]; };
layout(set = 0, binding = 13, std430) writeonly buffer EmissiveSamples { EmissiveSample emissive_samples[]; };
layout(push_constant, std430) uniform Params {
	vec4 origin_spacing;
	uvec4 counts_rays;
	vec4 sky_distance;
	uvec4 batch; // first probe, count, sample rotation, light count
	vec4 options; // history, CPU lighting epoch bits, visibility epoch bits, commit
} params;

vec2 hit_uv(Triangle triangle, Material material, vec3 weights) {
	vec2 uv = triangle.uv01.xy * weights.x + triangle.uv01.zw * weights.y + triangle.uv2.xy * weights.z;
	return uv * material.uv_transform.xy + material.uv_transform.zw;
}

vec4 hit_texel(Material material, vec2 uv) {
	if (material.options.z > .5) {
		if (material.options.w < 0.5) {
			vec2 half_texel = vec2(0.5) / vec2(textureSize(albedo_textures, 0).xy);
			uv = clamp(uv, half_texel, vec2(1.0) - half_texel);
		}
		return textureLod(albedo_textures, vec3(uv, material.options.y), 0.0);
	}
	return vec4(1.0);
}

vec3 hit_color(Material material, Triangle triangle, vec3 weights, vec4 texel) {
	vec3 vertex_color = (triangle.ca * weights.x + triangle.cb * weights.y + triangle.cc * weights.z).rgb;
	int kind = int(material.detail_vertex_kind_orm.z);
	vec3 color = material.albedo.rgb;
	if (kind < 2) {
		if (material.options.z > .5) color *= mix(material.average_saturation.rgb, texel.rgb, material.detail_vertex_kind_orm.x);
		if (material.detail_vertex_kind_orm.y > .5) color *= vertex_color;
		return clamp(mix(vec3(dot(color, vec3(.2126, .7152, .0722))), color, material.average_saturation.w), 0.0, 1.0);
	}
	if (kind == 2) {
		color = material.options.z > .5 ? texel.rgb : vertex_color;
		float green = smoothstep(.005, .07, color.g - color.r) * smoothstep(.005, .055, color.g - color.b);
		color = mix(color, gi_palette(color), material.foliage.x * green);
		return clamp(mix(vec3(dot(color, vec3(.2126, .7152, .0722))), color, material.foliage.y) * material.foliage.z, 0.0, 1.0);
	}
	vec3 local = (triangle.a * weights.x + triangle.b * weights.y + triangle.c * weights.z).xyz;
	float height = clamp((local.y - gi_crown_center(local).y + 1.05) / 2.9, 0.0, 1.0);
	float gradient = mix(.5, smoothstep(.02, .96, height), material.lower.w);
	return mix(material.lower.rgb, material.upper.rgb, gradient) * mix(vec3(1.0), vertex_color, material.upper.w)
		* (1.0 + sin(local.x * 2.7 + local.z * 3.1) * .016)
		* mix(1.0, .75 + dot(texel.rgb, vec3(.2126, .7152, .0722)), material.foliage.x);
}

vec3 sky_radiance(vec3 direction) {
	return ddgi_sky_radiance(environment_sky, direction, params.sky_distance.rgb,
		mat3(sky.basis_x.xyz, sky.basis_y.xyz, sky.basis_z.xyz), sky.options);
}

bool accepted_candidate(rayQueryEXT query) {
	uint instance_id = rayQueryGetIntersectionInstanceCustomIndexEXT(query, false);
	Material material = materials[instances[instance_id].y];
	vec2 bary = rayQueryGetIntersectionBarycentricsEXT(query, false);
	Triangle triangle = triangles[instances[instance_id].x + rayQueryGetIntersectionPrimitiveIndexEXT(query, false)];
	return material.albedo.a * hit_texel(material, hit_uv(triangle, material, vec3(1.0 - bary.x - bary.y, bary))).a >= material.options.x;
}

bool occluded(vec3 origin, vec3 direction, float max_distance) {
	rayQueryEXT query;
	rayQueryInitializeEXT(query, scene, gl_RayFlagsTerminateOnFirstHitEXT, 255, origin, 0.002, direction, max_distance);
	while (rayQueryProceedEXT(query)) {
		if (rayQueryGetIntersectionTypeEXT(query, false) == gl_RayQueryCandidateIntersectionTriangleEXT && accepted_candidate(query))
			rayQueryConfirmIntersectionEXT(query);
	}
	return rayQueryGetIntersectionTypeEXT(query, true) != gl_RayQueryCommittedIntersectionNoneEXT;
}

void main() {
	uint ray_id = gl_GlobalInvocationID.x;
	uint slot = gl_GlobalInvocationID.y;
	bool emissive_pass = params.options.w > 2.5;
	uint sample_count_total = emissive_pass ? emitter_count.x * 4u : params.counts_rays.w;
	if (ray_id >= sample_count_total || slot >= params.batch.y) return;
	uint probe = (params.batch.x + slot) % (params.counts_rays.x * params.counts_rays.y * params.counts_rays.z);
	uvec3 index = uvec3(probe % params.counts_rays.x, probe / (params.counts_rays.x * params.counts_rays.z), (probe / params.counts_rays.x) % params.counts_rays.z);
	vec4 state = imageLoad(probe_states, ivec2(index.x + index.z * params.counts_rays.x, index.y));
	bool visibility_pass = params.options.w > 1.5 && !emissive_pass;
	if (visibility_pass && (state.w < .5 || imageLoad(probe_epochs, ivec2(index.x + index.z * params.counts_rays.x, index.y)).y == floatBitsToUint(params.options.z))) return;
	vec3 position = params.origin_spacing.xyz + vec3(index) * params.origin_spacing.w + state.xyz;
	if (emissive_pass) {
		uint address = slot * sample_count_total + ray_id;
		emissive_samples[address] = EmissiveSample(vec4(0.0), vec4(0.0), vec4(0.0), vec4(0.0), vec4(0.0));
		if (state.w < .5) return;
		EmissiveTriangle source = emitters[ray_id / 4u];
		vec3 a = source.a.xyz, b = source.b.xyz, c = source.c.xyz;
		vec3 ab = (a + b) * .5, ac = (a + c) * .5, bc = (b + c) * .5;
		uint quarter = ray_id % 4u;
		if (quarter == 0u) { b = ab; c = ac; }
		else if (quarter == 1u) { a = ab; c = bc; }
		else if (quarter == 2u) { a = ac; b = bc; }
		else { a = ab; b = bc; c = ac; }
		vec3 normal = normalize(cross(c - a, b - a));
		vec3 center = (a + b + c) / 3.0;
		vec3 delta = center - position;
		float distance_to_light = length(delta);
		if (dot(normal, position - center) <= .0001 || distance_to_light <= .004 || distance_to_light > params.sky_distance.w) return;
		if (occluded(position, delta / distance_to_light, distance_to_light - .002)) return;
		Material material = materials[uint(source.a.w)];
		vec3 radiance = material.emission_metallic.rgb;
		if (material.detail_vertex_kind_orm.z > .5) radiance *= 1.0 - gi_snow(center, normal, material.weather.z);
		vec3 va = normalize(a - position), vb = normalize(b - position), vc = normalize(c - position);
		emissive_samples[address] = EmissiveSample(vec4(radiance / 3.14159265359, 0.0), vec4(va, 0.0), vec4(vb, 0.0), vec4(vc, 0.0),
			vec4(ddgi_triangle_integral(va, vb, vc), 0.0));
		return;
	}
	// Fixed full-sphere rays stabilize classification; only radiance samples rotate.
	bool fixed_ray = visibility_pass || ray_id < 32u;
	uint sample_id = fixed_ray ? ray_id : ray_id - 32u;
	uint sample_count = visibility_pass ? params.counts_rays.w : (fixed_ray ? min(32u, params.counts_rays.w) : params.counts_rays.w - 32u);
	float y = 1.0 - 2.0 * (float(sample_id) + 0.5) / float(sample_count);
	float angle = float(sample_id) * 2.39996322973 + (fixed_ray ? 0.0 : float(params.batch.z % 1024u) * 0.754877666);
	vec3 direction = vec3(cos(angle) * sqrt(max(0.0, 1.0 - y * y)), y, sin(angle) * sqrt(max(0.0, 1.0 - y * y)));
	rayQueryEXT query;
	rayQueryInitializeEXT(query, scene, 0, 255, position, 0.002, direction, params.sky_distance.w);
	while (rayQueryProceedEXT(query)) {
		if (rayQueryGetIntersectionTypeEXT(query, false) == gl_RayQueryCandidateIntersectionTriangleEXT && accepted_candidate(query))
			rayQueryConfirmIntersectionEXT(query);
	}
	if (visibility_pass) {
		float distance_to_hit = rayQueryGetIntersectionTypeEXT(query, true) == gl_RayQueryCommittedIntersectionTriangleEXT
			? rayQueryGetIntersectionTEXT(query, true) : params.sky_distance.w;
		uint address = slot * params.counts_rays.w + ray_id;
		rays[address].radiance_distance = vec4(0.0, 0.0, 0.0, distance_to_hit);
		rays[address].direction_backface = vec4(direction, 0.0);
		return;
	}
	vec3 radiance = sky_radiance(direction);
	float distance_to_hit = params.sky_distance.w;
	float backface = 0.0;
	if (rayQueryGetIntersectionTypeEXT(query, true) == gl_RayQueryCommittedIntersectionTriangleEXT) {
		uint instance_id = rayQueryGetIntersectionInstanceCustomIndexEXT(query, true);
		uint primitive_id = rayQueryGetIntersectionPrimitiveIndexEXT(query, true);
		Triangle triangle = triangles[instances[instance_id].x + primitive_id];
		mat4x3 transform = rayQueryGetIntersectionObjectToWorldEXT(query, true);
		vec3 a = transform * triangle.a;
		vec3 b = transform * triangle.b;
		vec3 c = transform * triangle.c;
		// Godot meshes use clockwise front faces.
		vec3 normal = normalize(cross(c - a, b - a)) * sign(determinant(mat3(transform)));
		backface = dot(normal, direction) > 0.0 ? 1.0 : 0.0;
		distance_to_hit = rayQueryGetIntersectionTEXT(query, true);
		Material material = materials[instances[instance_id].y];
		vec2 bary = rayQueryGetIntersectionBarycentricsEXT(query, true);
		vec3 weights = vec3(1.0 - bary.x - bary.y, bary);
		vec2 uv = hit_uv(triangle, material, weights);
		vec3 albedo = hit_color(material, triangle, weights, hit_texel(material, uv));
		int kind = int(material.detail_vertex_kind_orm.z);
		vec3 hit = position + direction * distance_to_hit;
		vec3 emission = material.emission_metallic.rgb;
		float metallic = material.emission_metallic.w;
		if (kind > 0) {
			vec3 local_normal = (triangle.na * weights.x + triangle.nb * weights.y + triangle.nc * weights.z).xyz;
			if (kind >= 2) {
				vec3 local = (triangle.a * weights.x + triangle.b * weights.y + triangle.c * weights.z).xyz;
				float roundness = kind == 3 ? material.foliage.y : material.foliage.w;
				vec3 radial = (local - gi_crown_center(local)) * vec3(1.0, .85, 1.0);
				local_normal = mix(local_normal, normalize(radial + vec3(1e-7)), roundness);
			}
			normal = normalize(transpose(mat3(rayQueryGetIntersectionWorldToObjectEXT(query, true))) * local_normal);
			if (kind >= 2) {
				backface = 0.0; // Thin leaf cards are two-sided, not solid probe interiors.
				if (dot(normal, direction) > 0.0) normal = -normal;
			}
			float snow = gi_snow(hit, normal, material.weather.z);
			vec2 wet = gi_wet(hit, normal, material) * (1.0 - snow);
			albedo *= kind == 1 ? (1.0 - wet.x * material.weather.w) * (1.0 - wet.y * .35) : 1.0 - wet.x * material.weather.w * .6;
			albedo = mix(albedo, material.snow_color.rgb * (.95 + .05 * wet_noise(hit.xz * 12.0)), snow);
			emission *= 1.0 - snow;
			if (material.detail_vertex_kind_orm.w >= 0.0) {
				metallic *= textureLod(material_data_textures, vec3(uv, material.detail_vertex_kind_orm.w), 0.0).b;
			}
			metallic *= (1.0 - wet.y) * (1.0 - snow);
		}
		radiance = vec3(0.0);
		if (backface < 0.5) {
			// Opaque emitters use deterministic area integration, avoiding rare bright ray hits.
			radiance = material.options.x < 0.0 ? vec3(0.0) : emission;
			for (uint light_id = 0; light_id < params.batch.w; light_id++) {
				vec3 light_direction = lights[light_id].direction.xyz;
				float cosine = max(dot(normal, light_direction), 0.0);
				if (cosine > 0.0 && !occluded(hit + normal * 0.005, light_direction, params.sky_distance.w))
					radiance += albedo * (1.0 - metallic) * lights[light_id].radiance.rgb * (cosine / 3.14159265359);
			}
		}
	}
	uint address = slot * params.counts_rays.w + ray_id;
	rays[address].radiance_distance = vec4(max(radiance, vec3(0.0)), distance_to_hit);
	rays[address].direction_backface = vec4(direction, backface);
}
