shader_type spatial;
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;

// Opaque painted water: shoreline distance is authored in UV.y, so foam also
// works off screen and has no screen/depth buffer or transparency dependency.
uniform vec4 deep_color : source_color = vec4(0.075, 0.245, 0.29, 1.0);
uniform vec4 middle_color : source_color = vec4(0.18, 0.43, 0.43, 1.0);
uniform vec4 shallow_color : source_color = vec4(0.42, 0.64, 0.56, 1.0);
uniform vec4 reflection_color : source_color = vec4(0.12, 0.32, 0.33, 1.0);
uniform vec4 ripple_color : source_color = vec4(0.57, 0.76, 0.69, 1.0);
uniform vec4 foam_color : source_color = vec4(0.83, 0.87, 0.70, 1.0);
uniform float flow_speed : hint_range(0.0, 1.0) = 0.22;
uniform float river_width = 4.8;
uniform float wave_height : hint_range(0.0, 0.05) = 0.008;
uniform float foam_width : hint_range(0.01, 0.2) = 0.045;
uniform float ripple_strength : hint_range(0.0, 1.0) = 0.43;
uniform float reflection_strength : hint_range(0.0, 1.0) = 0.32;
uniform float surface_roughness : hint_range(0.1, 1.0) = 0.58;
varying vec3 river_position;

float hash21(vec2 p) {
	return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

float painted_noise(vec2 p) {
	vec2 cell = floor(p);
	vec2 f = fract(p);
	f = f * f * (3.0 - 2.0 * f);
	return mix(mix(hash21(cell), hash21(cell + vec2(1.0, 0.0)), f.x),
		mix(hash21(cell + vec2(0.0, 1.0)), hash21(cell + vec2(1.0)), f.x), f.y);
}

void vertex() {
	vec3 world = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
	float shore_fade = smoothstep(0.0, 0.12, UV.y);
	VERTEX.y += sin(world.x * 2.3 + world.z * 4.0 - TIME * 0.8) * wave_height * shore_fade;
	river_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
	// Spatial stepping keeps strokes crisp without reducing the game's resolution.
	vec2 p = floor(river_position.xz * 36.0) / 36.0;
	float t = TIME * flow_speed;
	vec2 flow = p - vec2(t, t * 0.07);
	float broad = painted_noise(flow * vec2(0.72, 2.4));
	float brush = painted_noise(flow * vec2(2.6, 8.5) + vec2(8.1, 2.3));
	float bands = floor(clamp(broad * 0.78 + brush * 0.22, 0.0, 0.999) * 4.0) / 3.0;
	float bank_distance = UV.y * river_width;
	float shallow = 1.0 - smoothstep(0.02, 1.35, bank_distance);
	vec3 painted = mix(deep_color.rgb, middle_color.rgb, bands * 0.74 + 0.18);
	painted = mix(painted, shallow_color.rgb, shallow * 0.68);

	// Broad, broken woodland-colored reflection patches follow the same flow.
	float reflection = painted_noise(vec2(p.x * 1.25 + sin(p.y * 8.0 + t) * 0.07, p.y * 0.38));
	reflection = smoothstep(0.47, 0.61, reflection) * (1.0 - shallow * 0.65);
	painted = mix(painted, reflection_color.rgb, reflection * reflection_strength);

	float ripple = abs(sin(p.y * 24.0 + broad * 5.2 + t * 1.6));
	float broken_stroke = smoothstep(0.48, 0.65, painted_noise(flow * vec2(1.7, 9.0)));
	float line = (1.0 - smoothstep(0.035, 0.13, ripple)) * broken_stroke;
	painted = mix(painted, ripple_color.rgb, line * ripple_strength);

	// Two narrow, irregular foam ribbons; the first leaves a contact shadow.
	float scallop = sin(p.x * 4.1 + t * 0.6) * 0.022 + (brush - 0.5) * 0.04;
	float foam_a = 1.0 - smoothstep(foam_width * 0.45, foam_width, abs(bank_distance - 0.12 - scallop));
	float foam_b = 1.0 - smoothstep(foam_width * 0.30, foam_width * 0.7, abs(bank_distance - 0.31 - scallop * 1.5));
	float foam_break = smoothstep(0.24, 0.51, painted_noise(vec2(p.x * 4.0 - t * 0.2, 2.7)));
	float foam = max(foam_a * mix(0.18, 0.76, foam_break), foam_b * foam_break * 0.55);
	painted *= mix(0.62, 1.0, smoothstep(0.0, 0.10, bank_distance));
	painted = mix(painted, foam_color.rgb, foam);
	ALBEDO = painted;
	ROUGHNESS = surface_roughness;
	SPECULAR = 0.24;
	METALLIC = 0.0;
	float nx = cos(p.x * 2.3 + p.y * 4.0 - TIME * 0.8) * 0.018;
	float nz = sin(p.y * 13.0 + p.x * 1.4 - t) * 0.025;
	NORMAL = normalize((VIEW_MATRIX * vec4(normalize(vec3(nx, 1.0, nz)), 0.0)).xyz);
}

void light() {
	float facing = dot(NORMAL, LIGHT);
	float lit_band = smoothstep(0.1, 0.22, facing);
	vec3 diffuse = mix(vec3(0.28, 0.39, 0.43), vec3(0.92, 0.98, 0.89), lit_band);
	float shadow = LIGHT_IS_DIRECTIONAL ? mix(0.32, 1.0, ATTENUATION) : ATTENUATION;
	DIFFUSE_LIGHT += LIGHT_COLOR * diffuse * shadow / PI;
}
