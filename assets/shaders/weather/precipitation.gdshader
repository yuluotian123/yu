shader_type spatial;
render_mode cull_disabled, depth_draw_never;

uniform float weather_intensity : hint_range(0.0, 1.0) = 0.0;
uniform bool snow = false;
uniform float rain_opacity : hint_range(0.0, 1.0) = 0.42;

void fragment() {
    vec2 p = UV * 2.0 - 1.0;
    float mask = snow ? 1.0 - smoothstep(0.25, 1.0, length(p))
        : exp(-p.x * p.x * 5.0 - p.y * p.y * 2.5) * (1.0 - smoothstep(0.65, 1.0, length(p)));
    ALBEDO = snow ? vec3(0.92, 0.96, 1.0) : vec3(0.65, 0.80, 0.94);
    ROUGHNESS = 0.9;
    ALPHA = mask * weather_intensity * (snow ? 0.95 : rain_opacity);
}
