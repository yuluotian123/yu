shader_type spatial;
render_mode unshaded, cull_back, depth_draw_never, fog_disabled;

// Cloud.shader by xinyangaa, commit ef1bd4c. Fragment equations retain the source behavior.
uniform sampler2D cloud_map : source_color, filter_linear_mipmap, repeat_disable;
uniform sampler2D noise_map : filter_linear_mipmap, repeat_enable;
uniform vec4 noise_map_st = vec4(0.3, 0.3, 0.0, 0.0);
uniform float noise_time = 0.0;
uniform float cloud_sdf = 0.003;
uniform vec4 cloud_color_a = vec4(0.5050285, 0.70413554, 0.9150943, 1.0);
uniform vec4 cloud_color_b = vec4(0.9858491, 0.9863516, 1.0, 1.0);
uniform vec4 cloud_color_c = vec4(0.8632076, 0.9483539, 1.0, 1.0);
uniform vec4 cloud_color_d = vec4(1.0);
uniform vec4 cloud_edge_color = vec4(1.0);
uniform vec3 sun_direction = vec3(-0.109381, 0.965926, 0.23457);
uniform vec3 moon_direction = vec3(0.109381, -0.965926, -0.23457);
uniform float sun_moon = 0.0;

// Godot bridge: match the scene sky's projection and reversed-Z far depth.
uniform float sky_custom_fov = 65.0;
uniform float sky_pitch = -0.57595865;
varying vec3 position_ws;
varying vec2 source_uv;
varying vec2 source_noise_uv;

void vertex() {
    position_ws = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    source_uv = UV2;
    source_noise_uv = (UV2 * noise_map_st.xy + noise_map_st.zw) * noise_map_st.xy + noise_map_st.zw + vec2(noise_time * 0.05);
    // The reference scene camera is at (0, -14, 0). Anchor its sky composition to the game camera.
    vec3 direction = position_ws - vec3(0.0, -14.0, 0.0);
    float c = cos(sky_pitch);
    float s = sin(sky_pitch);
    direction = vec3(direction.x, c * direction.y - s * direction.z, s * direction.y + c * direction.z);
    vec3 view_direction = mat3(VIEW_MATRIX) * direction;
    if (sky_custom_fov > 0.0) {
        float f = 1.0 / tan(radians(sky_custom_fov) * 0.5);
        POSITION = vec4(view_direction.x * f * VIEWPORT_SIZE.y / VIEWPORT_SIZE.x, view_direction.y * f * sign(PROJECTION_MATRIX[1][1]), 0.0, -view_direction.z);
    } else {
        POSITION = PROJECTION_MATRIX * vec4(view_direction, 1.0);
    }
    // Godot uses reversed Z. Keep clouds behind level geometry without writing depth.
    POSITION.z = (CLIP_SPACE_FAR + 0.000001) * POSITION.w;
}

void fragment() {
    vec3 light_direction = mix(sun_direction, moon_direction, sun_moon);
    float weight = pow(clamp(dot(normalize(position_ws), light_direction), 0.0, 1.0), 2.0);
    vec3 color_ab = mix(cloud_color_a.rgb, cloud_color_b.rgb, weight);
    vec3 color_cd = mix(cloud_color_c.rgb, cloud_color_d.rgb, weight);
    float uv_disturbance = texture(noise_map, source_noise_uv).b * 0.03;
    vec4 base_map = texture(cloud_map, source_uv + vec2(uv_disturbance));
    float opacity = smoothstep(clamp(cloud_sdf - 0.08, 0.0, 1.5), cloud_sdf, base_map.b) * base_map.a;
    vec3 color = mix(color_ab, color_cd, base_map.r) + cloud_edge_color.rgb * base_map.g * weight;
    ALBEDO = color;
    ALPHA = opacity;
}
