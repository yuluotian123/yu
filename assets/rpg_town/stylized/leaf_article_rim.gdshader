shader_type spatial;
render_mode unshaded, blend_add, depth_draw_never, cull_disabled, shadows_disabled, fog_disabled;

// Separate overlay: the base pass retains opaque cutout depth and shadows.
uniform sampler2D leaf_texture : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D scene_depth : hint_depth_texture, filter_nearest, repeat_disable;
uniform float alpha_cutoff = 0.50;
uniform float rim_width_pixels : hint_range(0.0, 6.0) = 2.5;
uniform float rim_depth_threshold : hint_range(0.1, 3.0) = 1.0;
uniform float rim_strength : hint_range(0.0, 0.3) = 0.055;
uniform vec3 rim_tint = vec3(1.0, 0.92, 0.50);
uniform float wind_strength = 0.018;
uniform float wind_speed = 0.65;

void vertex() {
    vec3 centers[6] = vec3[6](
        vec3(-4.9, 3.57, 5.0), vec3(5.5, 3.88, -1.5),
        vec3(2.0, 3.27, 12.3), vec3(8.5, 3.95, -13.5),
        vec3(-8.5, 4.10, -14.0), vec3(-17.0, 4.18, -10.8));
    vec3 center = centers[0];
    float nearest = 100000.0;
    for (int i = 0; i < 6; i++) {
        float distance_sq = dot(VERTEX - centers[i], VERTEX - centers[i]);
        if (distance_sq < nearest) { nearest = distance_sq; center = centers[i]; }
    }
    NORMAL = normalize(mix(NORMAL, normalize((VERTEX-center)*vec3(1.0,0.85,1.0)), 0.12));
    float sway = sin(TIME * wind_speed + VERTEX.x * 0.8 + VERTEX.z * 0.9);
    VERTEX.xz += vec2(sway, sway * 0.35) * wind_strength * UV.y * UV.y;
}

float linear_depth(vec2 screen_uv, mat4 inverse_projection) {
    float depth = textureLod(scene_depth, screen_uv, 0.0).r;
    #if CURRENT_RENDERER == RENDERER_COMPATIBILITY
        vec3 ndc = vec3(screen_uv, depth) * 2.0 - 1.0;
    #else
        vec3 ndc = vec3(screen_uv * 2.0 - 1.0, depth);
    #endif
    vec4 position = inverse_projection * vec4(ndc, 1.0);
    return -position.z / position.w;
}

void fragment() {
    if (texture(leaf_texture, UV).a < alpha_cutoff) { discard; }
    vec2 outward = normalize(NORMAL.xy + vec2(0.0001));
    vec2 rim_uv = clamp(SCREEN_UV + outward * rim_width_pixels / VIEWPORT_SIZE,
                        vec2(0.001), vec2(0.999));
    float gap = linear_depth(rim_uv, INV_PROJECTION_MATRIX) + VERTEX.z;
    float depth_edge = smoothstep(rim_depth_threshold, rim_depth_threshold + 0.7, gap);
    float silhouette = pow(1.0 - clamp(dot(normalize(NORMAL), VIEW), 0.0, 1.0), 3.0);
    // Upper-facing leaf edges receive a restrained warm painted highlight.
    vec3 world_normal = normalize((INV_VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz);
    float upper_edge = smoothstep(-0.35, 0.5, world_normal.y);
    ALBEDO = rim_tint * rim_strength * depth_edge * silhouette * upper_edge;
}


