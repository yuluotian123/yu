shader_type spatial;
render_mode cull_disabled, specular_disabled;

// Shared art direction for textured tree leaves and vertex-colored flowers.
uniform bool use_leaf_texture = false;
uniform sampler2D leaf_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform float alpha_cutoff : hint_range(0.1, 0.9) = 0.52;
uniform float palette_strength : hint_range(0.0, 1.0) = 0.72;
uniform float color_saturation : hint_range(0.0, 1.8) = 1.02;
uniform float albedo_value : hint_range(0.4, 1.2) = 0.92;
uniform float canopy_normal_blend : hint_range(0.0, 1.0) = 0.76;
uniform float band_softness : hint_range(0.01, 0.25) = 0.08;
uniform float light_wrap : hint_range(0.0, 0.6) = 0.28;
uniform vec3 shadow_tint = vec3(0.68, 0.85, 0.88);
uniform vec3 middle_tint = vec3(0.97, 1.02, 0.97);
uniform vec3 light_tint = vec3(1.03, 1.02, 0.97);
uniform float painterly_blend : hint_range(0.0, 1.0) = 0.65;

vec3 garden_palette(vec3 color) {
    float brightness = sqrt(max(color.r, max(color.g, color.b)));
    vec3 deep_green = vec3(0.028, 0.12, 0.073);
    vec3 leaf_green = vec3(0.11, 0.29, 0.058);
    vec3 sun_green = vec3(0.27, 0.48, 0.17);
    vec3 painted = mix(deep_green, leaf_green, smoothstep(0.18, 0.55, brightness));
    return mix(painted, sun_green, smoothstep(0.55, 0.89, brightness));
}

void vertex() {
    if (use_leaf_texture) {
        // The six baked tree crowns share one mesh. Broad crown normals give
        // cohesive painted light masses instead of lighting each tiny card.
        vec3 centers[6] = vec3[6](
            vec3(-4.9, 3.57, 5.0), vec3(5.5, 3.88, -1.5),
            vec3(2.0, 3.27, 12.3), vec3(8.5, 3.95, -13.5),
            vec3(-8.5, 4.10, -14.0), vec3(-17.0, 4.18, -10.8));
        vec3 center = centers[0];
        float closest = 100000.0;
        for (int i = 0; i < 6; i++) {
            float distance_sq = dot(VERTEX - centers[i], VERTEX - centers[i]);
            if (distance_sq < closest) { closest = distance_sq; center = centers[i]; }
        }
        vec3 volume_normal = normalize((VERTEX - center) * vec3(1.0, 0.85, 1.0));
        NORMAL = normalize(mix(NORMAL, volume_normal, canopy_normal_blend));
    }
}

void fragment() {
    vec3 base = COLOR.rgb;
    if (use_leaf_texture) {
        vec4 leaf = texture(leaf_texture, UV);
        // Opaque cutout keeps depth, shadows and overlapping foliage stable.
        if (leaf.a < alpha_cutoff) {
            discard;
        }
        base = leaf.rgb;
    }
    float is_green = smoothstep(0.005, 0.07, base.g - base.r)
                   * smoothstep(0.005, 0.055, base.g - base.b);
    base = mix(base, garden_palette(base), palette_strength * is_green);
    float grey = dot(base, vec3(0.2126, 0.7152, 0.0722));
    base = mix(vec3(grey), base, color_saturation);
    ALBEDO = clamp(base * albedo_value, vec3(0.0), vec3(1.0));
    ROUGHNESS = 1.0;
    METALLIC = 0.0;
    SPECULAR = 0.0;
    if (!FRONT_FACING && !use_leaf_texture) {
        NORMAL = -NORMAL;
    }
}

void light() {
    float facing = clamp((dot(NORMAL, LIGHT) + light_wrap) / (1.0 + light_wrap), 0.0, 1.0);
    float middle = smoothstep(0.38 - band_softness, 0.38 + band_softness, facing);
    float bright = smoothstep(0.83 - band_softness, 0.83 + band_softness, facing);
    vec3 tone = mix(shadow_tint * 0.30, middle_tint * 0.65, middle);
    tone = mix(tone, light_tint, bright);
    float volume = smoothstep(0.0, 1.0, facing);
    vec3 painted = mix(shadow_tint, light_tint, volume) * mix(0.30, 1.0, volume);
    tone = mix(tone, painted, painterly_blend);
    DIFFUSE_LIGHT += tone * LIGHT_COLOR * ATTENUATION / PI;
}
