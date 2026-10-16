shader_type spatial;
// Native Toon suppresses dielectric sky reflections. Paving needs the PBR branch.
render_mode cull_disabled, diffuse_burley, specular_schlick_ggx;
#include "res://assets/shaders/weather/wet_surface.gdshaderinc"
#include "res://assets/shaders/town/town_surface.gdshaderinc"
