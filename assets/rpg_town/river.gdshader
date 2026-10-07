shader_type spatial;
render_mode cull_back, diffuse_burley, specular_schlick_ggx;
uniform sampler2D water_tex : source_color, filter_linear_mipmap, repeat_enable;
void fragment() {
    vec2 flow = UV * vec2(0.4, 0.8);
    flow.x += TIME * 0.017;
    flow.y += sin(UV.x * 1.9 + TIME * 0.53) * 0.045;
    vec2 atlas_uv = vec2(0.06, 0.415) + fract(flow) * vec2(0.88, 0.16);
    vec3 painted = texture(water_tex, atlas_uv).rgb;
    ALBEDO = mix(vec3(0.018, 0.15, 0.18), painted, 0.62);
    ROUGHNESS = 0.28;
    METALLIC = 0.12;
    SPECULAR = 0.45;
    NORMAL_MAP = vec3(0.5 + 0.055 * sin(UV.x * 13.0 + TIME), 0.5 + 0.04 * cos(UV.y * 12.0 - TIME * 0.7), 1.0);
    NORMAL_MAP_DEPTH = 0.35;
}
