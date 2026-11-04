// Opt-in per instance. Fade only the surface approaching the orthographic near plane;
// use a stable ordered pattern, and leave actual shadow casting untouched.
instance uniform float camera_near_fade_distance : instance_index(8) = 0.0;
// Timed player occlusion and camera intrusion hide every surface of the object together,
// including back faces; retain shadow casting and GI geometry.
instance uniform float camera_solid_hide : instance_index(11) = 0.0;
bool camera_occlusion_discard(vec3 view_position, vec2 screen_pixel, bool shadow_pass, mat4 projection, mat4 view) {
	if (shadow_pass) return false;
	bool orthographic = projection[3][3] > 0.5;
	float coverage = 1.0-clamp(camera_solid_hide,0.0,1.0);
	if (camera_near_fade_distance > 0.0 && orthographic) {
		coverage = min(coverage,smoothstep(min(0.25,camera_near_fade_distance*0.5),camera_near_fade_distance,-view_position.z));
	}
	if (coverage >= 1.0) return false;
	const float bayer[16] = float[16](0.,8.,2.,10.,12.,4.,14.,6.,3.,11.,1.,9.,15.,7.,13.,5.);
	ivec2 pixel = ivec2(screen_pixel) % 4;
	return coverage <= (bayer[pixel.y*4+pixel.x]+0.5)/16.0;
}
