shader_type spatial;
render_mode cull_disabled, alpha_to_coverage;
#include "res://assets/shaders/weather/wet_surface.gdshaderinc"

uniform sampler2D grass_atlas : source_color,filter_linear_mipmap_anisotropic,repeat_disable;
uniform float alpha_cutoff = 0.45;
uniform float wind_strength = 0.038;
uniform float orthographic_camera_pullback = 0.0;
uniform float directional_shadow_lift : hint_range(0.0,0.3) = 0.0;
varying vec4 tuft_data;
varying float tuft_height;

void vertex() {
	tuft_data = INSTANCE_CUSTOM;
	tuft_height = COLOR.a;
	vec3 origin = MODEL_MATRIX[3].xyz;
	// Orthographic framing can move the camera back without changing the visible scale.
	vec3 cull_origin = CAMERA_POSITION_WORLD;
	if (PROJECTION_MATRIX[3][3] > 0.5) cull_origin -= INV_VIEW_MATRIX[2].xyz*orthographic_camera_pullback;
	float distance_to_camera = length(origin-cull_origin);
	VERTEX.y *= 1.0-smoothstep(28.0,36.0,distance_to_camera);
	float gust = sin(origin.x*.23+origin.z*.17+TIME*.8);
	gust += .3*sin(origin.x*.73-origin.z*.61+TIME*1.36);
	float stem_scale = min(1.0,length(MODEL_MATRIX[1].xyz));
	float bend = gust*wind_strength*stem_scale*tuft_height*tuft_height*(1.0-weather_snow*.8);
	// Yaw-only placement keeps X/Z scales equal; horizontal sway needs no inverse.
	vec3 world_sway = vec3(.94,0.0,.34)*bend;
	VERTEX += transpose(mat3(MODEL_MATRIX))*world_sway/dot(MODEL_MATRIX[0].xyz,MODEL_MATRIX[0].xyz);
	weather_position = (MODEL_MATRIX*vec4(VERTEX,1.0)).xyz;
}

void fragment() {
	vec4 tuft = texture(grass_atlas,UV);
	// MSAA must cover the texture silhouette too, or moving thin blades sparkle.
	ALPHA = tuft.a;
	ALPHA_SCISSOR_THRESHOLD = alpha_cutoff;
	ALPHA_ANTIALIASING_EDGE = alpha_cutoff*.6;
	ALPHA_TEXTURE_COORDINATE = UV*vec2(textureSize(grass_atlas,0));
	ALBEDO = mix(tuft_data.rgb,tuft.rgb*tuft_data.a,smoothstep(0.04,.87,tuft_height)*.60);
	if (!FRONT_FACING) { NORMAL = -NORMAL; }
	vec3 world_normal = normalize((INV_VIEW_MATRIX*vec4(NORMAL,0.0)).xyz);
	float snow = snow_surface_sample(weather_position,world_normal);
	float wet = weather_wetness*(1.0-snow);
	ALBEDO *= 1.0-wet*weather_darkening*wet_darkening_strength;
	weather_surface_wet = wet*wet_film_strength;
	// Broad tuft normals only support a soft, rough leaf sheen.
	ROUGHNESS = mix(1.0,max(.62,wet_roughness_min),weather_surface_wet);
	SPECULAR = .24*weather_surface_wet;
	if (snow > 0.0) {
		ALBEDO = mix(ALBEDO,snow_surface_color(weather_position),snow);
		ROUGHNESS = mix(ROUGHNESS,weather_snow_roughness,snow);
	}
	weather_surface_roughness = ROUGHNESS;
}

void light() {
	float facing = clamp((dot(NORMAL,LIGHT)+.22)/1.22,0.0,1.0);
	vec3 tone = mix(vec3(.32,.42,.39),vec3(.76,.80,.62),smoothstep(.33,.43,facing));
	tone = mix(tone,vec3(1.04,1.00,.84),smoothstep(.75,.85,facing));
	float lift = mix(0.26,directional_shadow_lift,smoothstep(0.0,0.65,tuft_height));
	float shade = LIGHT_IS_DIRECTIONAL ? mix(lift,1.0,ATTENUATION) : ATTENUATION;
	DIFFUSE_LIGHT += tone*LIGHT_COLOR*shade/PI;
	if (weather_surface_wet > 0.0) {
		SPECULAR_LIGHT += wet_direct_specular(NORMAL,LIGHT,VIEW,LIGHT_COLOR,ATTENUATION,weather_surface_roughness)*weather_surface_wet*.15;
	}
}
