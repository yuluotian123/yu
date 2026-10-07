shader_type spatial;
render_mode cull_disabled;

// 底色保留原模型配色。纹理均值与细节混合，减弱照片式碎纹理；不改变 UV。
uniform vec4 base_color : source_color = vec4(1.0);
uniform bool use_texture = false;
uniform sampler2D albedo_texture : source_color, filter_linear_mipmap_anisotropic, repeat_enable;
uniform vec3 texture_average = vec3(0.4);
uniform float texture_detail : hint_range(0.0, 1.0) = 0.62;
uniform bool use_vertex_color = false;
uniform float color_saturation : hint_range(0.0, 2.0) = 1.02;
// 保留明暗色块，再混入连续的绘画式体积过渡。
uniform float band_softness : hint_range(0.01, 0.2) = 0.14;
uniform float painterly_blend : hint_range(0.0, 1.0) = 0.65;
uniform float light_wrap : hint_range(0.0, 0.5) = 0.12;
uniform vec3 shadow_tint = vec3(0.72, 0.82, 1.0);
uniform vec3 middle_tint = vec3(1.0, 0.99, 0.97);
uniform vec3 light_tint = vec3(1.03, 1.01, 0.97);
uniform vec3 emission_color = vec3(0.0);
// 木石保持哑光；金属材质单独开启反射和宽高光。
uniform float surface_roughness : hint_range(0.2, 1.0) = 1.0;
uniform float surface_metallic : hint_range(0.0, 1.0) = 0.0;
uniform float specular_strength : hint_range(0.0, 1.0) = 0.0;

void fragment() {
    vec3 color = base_color.rgb;
    if (use_texture) color *= mix(texture_average, texture(albedo_texture, UV).rgb, texture_detail);
    if (use_vertex_color) color *= COLOR.rgb;
    float grey = dot(color, vec3(0.2126, 0.7152, 0.0722));
    ALBEDO = clamp(mix(vec3(grey), color, color_saturation), vec3(0.0), vec3(1.0));
    ROUGHNESS = surface_roughness;
    SPECULAR = specular_strength;
    METALLIC = surface_metallic;
    EMISSION = emission_color;
    if (!FRONT_FACING) NORMAL = -NORMAL;
}

void light() {
    float facing = clamp((dot(NORMAL, LIGHT) + light_wrap) / (1.0 + light_wrap), 0.0, 1.0);
    float middle = smoothstep(0.30 - band_softness, 0.30 + band_softness, facing);
    float bright = smoothstep(0.72 - band_softness, 0.72 + band_softness, facing);
    vec3 tone = mix(shadow_tint * 0.24, middle_tint * 0.62, middle);
    tone = mix(tone, light_tint, bright);
    float volume = smoothstep(0.0, 1.0, facing);
    vec3 painted = mix(shadow_tint, light_tint, volume) * mix(0.24, 1.0, volume);
    tone = mix(tone, painted, painterly_blend);
    // 保留真实阴影和灯光衰减，夜晚不靠固定自发光抬亮。
    DIFFUSE_LIGHT += tone * LIGHT_COLOR * ATTENUATION / PI;
    if (specular_strength > 0.0) {
        vec3 half_vector = normalize(LIGHT + VIEW);
        float exponent = mix(96.0, 12.0, surface_roughness);
        float lobe = pow(max(dot(NORMAL, half_vector), 0.0), exponent);
        float highlight = smoothstep(0.25, 0.8, lobe);
        vec3 reflection_color = mix(vec3(0.04), ALBEDO, surface_metallic);
        SPECULAR_LIGHT += reflection_color * LIGHT_COLOR * ATTENUATION * highlight * specular_strength / PI;
    }
}
