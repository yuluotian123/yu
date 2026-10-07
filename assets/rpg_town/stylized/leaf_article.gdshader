shader_type spatial;
render_mode cull_disabled, specular_disabled;

// Art direction inspired by 岑立: 风格化树——树叶分析与实现.
// Original Godot implementation: alpha silhouette + volume lighting, no RGB veins.
uniform sampler2D leaf_texture : source_color, filter_linear_mipmap, repeat_disable;

uniform float alpha_cutoff : hint_range(0.2, 0.8) = 0.50;
uniform vec3 lower_color = vec3(0.110, 0.230, 0.150);
uniform vec3 upper_color = vec3(0.270, 0.460, 0.180);
uniform float normal_roundness : hint_range(0.0, 1.0) = 0.68;
uniform float vertex_color_strength : hint_range(0.0, 1.0) = 0.40;
uniform float directional_shadow_lift : hint_range(0.0, 0.25) = 0.12;
uniform float texture_detail : hint_range(0.0, 0.3) = 0.0;
uniform float gradient_strength : hint_range(0.0, 1.0) = 0.60;
uniform float transmission_strength : hint_range(0.0, 0.4) = 0.075;
uniform float wind_strength : hint_range(0.0, 0.1) = 0.018;
uniform float wind_speed : hint_range(0.0, 3.0) = 0.65;

varying float crown_height;
varying float color_variation;

void vertex() {
    // Local centers match the six existing trees in the combined town mesh.
    vec3 centers[6] = vec3[6](
        vec3(-4.9, 3.57, 5.0), vec3(5.5, 3.88, -1.5),
        vec3(2.0, 3.27, 12.3), vec3(8.5, 3.95, -13.5),
        vec3(-8.5, 4.10, -14.0), vec3(-17.0, 4.18, -10.8));
    vec3 center = centers[0];
    float nearest = 100000.0;
    for (int i = 0; i < 6; i++) {
        vec3 offset = VERTEX - centers[i];
        float distance_sq = dot(offset, offset);
        if (distance_sq < nearest) { nearest = distance_sq; center = centers[i]; }
    }
    vec3 radial = VERTEX - center;
    vec3 volume_normal = normalize(radial * vec3(1.0, 0.85, 1.0));
    NORMAL = normalize(mix(NORMAL, volume_normal, normal_roundness));
    crown_height = clamp((radial.y + 1.05) / 2.9, 0.0, 1.0);
    color_variation = sin(VERTEX.x * 2.7 + VERTEX.z * 3.1) * 0.016;
    float sway = sin(TIME * wind_speed + VERTEX.x * 0.8 + VERTEX.z * 0.9);
    VERTEX.xz += vec2(sway, sway * 0.35) * wind_strength * UV.y * UV.y;
}

void fragment() {
    vec4 mask = texture(leaf_texture, UV);
    if (mask.a < alpha_cutoff) { discard; }

    float gradient = mix(0.5, smoothstep(0.02, 0.96, crown_height), gradient_strength);
    vec3 tint = mix(lower_color, upper_color, gradient);
    float detail = dot(mask.rgb, vec3(0.2126, 0.7152, 0.0722));
    // 原顶点色只保留局部变化，避免与树冠渐变相乘后暗面发黑。
    ALBEDO = tint * mix(vec3(1.0), COLOR.rgb, vertex_color_strength)
             * (1.0 + color_variation) * mix(1.0, 0.75 + detail, texture_detail);
    ROUGHNESS = 1.0;
    SPECULAR = 0.0;
    METALLIC = 0.0;

}

void light() {
    float half_lambert = dot(normalize(NORMAL), LIGHT) * 0.5 + 0.5;
    float soft_diffuse = mix(0.22, 1.0, smoothstep(0.10, 0.96, half_lambert));
    vec3 shade = mix(vec3(0.72, 0.88, 1.0), vec3(1.03, 1.02, 0.94), half_lambert);
    // 少量树冠透光只用于日月；点光保留距离衰减，不能照亮范围外的树叶。
    float diffuse_attenuation = LIGHT_IS_DIRECTIONAL
                               ? mix(directional_shadow_lift, 1.0, ATTENUATION) : ATTENUATION;
    vec3 diffuse = shade * soft_diffuse * diffuse_attenuation;
    float back_scatter = pow(clamp(dot(VIEW, -normalize(LIGHT + NORMAL * 0.35)), 0.0, 1.0), 6.0);
    vec3 transmission = vec3(1.0, 0.98, 0.82) * back_scatter * transmission_strength * ATTENUATION;
    DIFFUSE_LIGHT += (diffuse + transmission) * LIGHT_COLOR / PI;
}



