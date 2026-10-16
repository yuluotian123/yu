// 材质的天气层。干燥端点保留原卡通明暗，湿润端点使用介电水膜反射。
uniform float weather_wetness : hint_range(0.0, 1.0) = 0.0;
uniform float weather_puddles : hint_range(0.0, 1.0) = 0.0;
uniform bool weather_puddles_enabled = false;
uniform float weather_puddle_coverage : hint_range(0.0, 0.8) = 0.55;
uniform float weather_puddle_scale = 0.85;
uniform float weather_darkening : hint_range(0.0, 0.5) = 0.24;
uniform float weather_wet_roughness : hint_range(0.02, 0.6) = 0.14;
uniform float weather_puddle_roughness : hint_range(0.02, 0.3) = 0.045;
uniform float weather_wet_specular : hint_range(0.0, 1.0) = 0.6;
uniform float weather_rain = 0.0;
uniform float weather_ripple_strength : hint_range(0.0, 0.3) = 0.14;
uniform float weather_ripple_scale = 1.7;
uniform float weather_ripple_speed = 1.35;
uniform float weather_snow : hint_range(0.0, 1.0) = 0.0;
uniform vec4 weather_snow_color : source_color = vec4(0.91, 0.95, 1.0, 1.0);
uniform float weather_snow_roughness : hint_range(0.4, 1.0) = 0.86;
uniform float weather_time = 0.0;
varying vec3 weather_position;
varying float weather_surface_wet;
varying float weather_puddle_mask;
varying float weather_surface_roughness;

// 薄水膜在完全饱和前就形成；干燥和完全湿润端点仍精确为 0 / 1。
float wet_film_amount(float wetness) {
    return (1.0 - exp(-4.0 * clamp(wetness, 0.0, 1.0))) / (1.0 - exp(-4.0));
}

float wet_hash(vec2 p) {
    // 整数散列在相邻格子的两侧完全一致，避免 sin/fract 放大浮点误差形成切边。
    uvec2 cell = uvec2(ivec2(p));
    uint h = cell.x * 1597334677u ^ cell.y * 3812015801u;
    h = (h ^ (h >> 16u)) * 2246822519u;
    h = (h ^ (h >> 13u)) * 3266489917u;
    return float((h ^ (h >> 16u)) & 16777215u) / 16777216.0;
}

float wet_noise(vec2 p) {
    vec2 cell = floor(p);
    vec2 f = fract(p);
    f = f * f * f * (f * (f * 6.0 - 15.0) + 10.0);
    return mix(mix(wet_hash(cell), wet_hash(cell + vec2(1.0, 0.0)), f.x),
               mix(wet_hash(cell + vec2(0.0, 1.0)), wet_hash(cell + vec2(1.0)), f.x), f.y);
}

#ifdef RT
// Closest-hit stages have no pixel quads. Estimate a surface footprint from the
// camera projection instead; raster materials retain their native derivatives.
float wet_pixel_footprint(vec3 position, vec3 world_normal, mat4 view_matrix,
                          mat4 projection, mat4 inverse_view, vec2 viewport_size) {
    vec4 view_position = view_matrix * vec4(position, 1.0);
    float clip_w = abs((projection * view_position).w);
    vec2 projection_scale = abs(vec2(projection[0][0], projection[1][1]));
    vec2 pixel = 2.0 * clip_w / max(projection_scale * viewport_size, vec2(0.0001));
    vec3 to_camera = projection[3][3] > 0.5
        ? inverse_view[2].xyz : inverse_view[3].xyz - position;
    float facing = max(abs(dot(normalize(world_normal), normalize(to_camera))), 0.08);
    return max(max(pixel.x, pixel.y) / facing, 0.0001);
}
#define WEATHER_PIXEL_FOOTPRINT(p, n) wet_pixel_footprint(p, n, VIEW_MATRIX, PROJECTION_MATRIX, INV_VIEW_MATRIX, VIEWPORT_SIZE)
#else
#define WEATHER_PIXEL_FOOTPRINT(p, n) 0.0
#endif

float snow_pattern(vec2 position) {
    return wet_noise(position * 1.25 + vec2(31.0)) * 0.72
         + wet_noise(position * 4.3 + vec2(9.0)) * 0.28;
}

float puddle_pattern(vec2 p) {
    float noise = wet_noise(p) * 0.7 + wet_noise(p * 2.07 + vec2(17.0)) * 0.3;
    return smoothstep(0.18, 0.82, noise);
}

// 邻格一起采样，涟漪越过格子边界时不会被切成方块。每次落点和间隔均有变化。
vec2 rain_ripple_sample(vec2 position, float world_footprint) {
    if (weather_rain <= 0.0 || weather_ripple_strength <= 0.0) { return vec2(0.0); }
    vec2 p = position * max(weather_ripple_scale, 0.5);
    // 远处环宽小于像素时，单纯放宽包络仍会让正弦法线闪烁。
    // 在分支外计算足迹并滤除高频波形，避免掠射角高光把亚像素波纹放大成噪点。
#ifdef RT
    float footprint = world_footprint * max(weather_ripple_scale, 0.5);
#else
    float footprint = max(length(dFdx(p)), length(dFdy(p)));
#endif
    float visibility = (1.0 - smoothstep(0.035, 0.10, footprint))
                     * exp(-0.5 * pow(footprint * 22.5, 2.0));
    if (visibility <= 0.001) { return vec2(0.0); }
    vec2 cell = floor(p);
    vec2 ripple = vec2(0.0);
    for (int y = -1; y <= 1; y++) {
        for (int x = -1; x <= 1; x++) {
            vec2 neighbor = cell + vec2(float(x), float(y));
            float clock = weather_time * weather_ripple_speed + wet_hash(neighbor + 43.0);
            float age = fract(clock);
            vec2 seed = neighbor + floor(clock) * vec2(7.0, 13.0);
            if (wet_hash(seed + 73.0) > weather_rain) { continue; }
            vec2 offset = vec2(wet_hash(seed), wet_hash(seed + 13.0)) * 0.8 + 0.1;
            vec2 drop = p - neighbor - offset;
            float radius = length(drop);
            float distance_to_ring = radius - age * 0.68;
            float width = max(0.055, footprint * 0.75);
            float ring = sin(distance_to_ring * 45.0) * exp(-abs(distance_to_ring) / width);
            float fade = smoothstep(0.0, 0.08, age) * (1.0 - age) * (1.0 - age);
            ripple += drop / max(radius, 0.001) * ring * fade;
        }
    }
    return ripple * weather_ripple_strength * weather_rain * visibility;
}

// 雪从零星覆盖逐渐连成雪面；朝下/垂直面不积雪，屋顶和树冠随坡度变薄。
float snow_surface_sample(vec3 position, vec3 world_normal, float world_footprint) {
    if (weather_snow <= 0.0) { return 0.0; }
    float slope = smoothstep(0.45, 0.85, world_normal.y);
    float noise = snow_pattern(position.xz);
#ifdef RT
    float pixel = world_footprint;
    float edge = max(0.09, abs(snow_pattern(position.xz + vec2(pixel, 0.0)) - noise)
                         + abs(snow_pattern(position.xz + vec2(0.0, pixel)) - noise));
#else
    float edge = max(0.09, fwidth(noise));
#endif
    float coverage = smoothstep(1.0 - weather_snow - edge, 1.0 - weather_snow + edge, noise);
    return coverage * slope * smoothstep(0.0, 0.12, weather_snow);
}

vec3 snow_surface_color(vec3 position) {
    return weather_snow_color.rgb * (0.95 + 0.05 * wet_noise(position.xz * 12.0));
}

vec3 snow_surface_normal(vec3 position, vec3 normal, float cover) {
    vec2 p = position.xz * 9.0;
    float center = wet_noise(p);
    vec2 grain = vec2(wet_noise(p + vec2(0.12, 0.0)), wet_noise(p + vec2(0.0, 0.12))) - center;
    return normalize(normal + vec3(grain.x, 0.0, grain.y) * cover * 0.7);
}

vec4 wet_surface_sample(vec3 position, vec3 world_normal, float world_footprint) {
    if (weather_wetness <= 0.0 && weather_puddles <= 0.0) { return vec4(0.0); }
    float up = clamp(world_normal.y, 0.0, 1.0);
    float wet = weather_wetness * mix(0.55, 1.0, up);
    float puddle = 0.0;
    vec2 ripple = vec2(0.0);
    if (weather_puddles_enabled && weather_puddles > 0.0) {
        vec2 p = position.xz * max(weather_puddle_scale, 0.01);
        // 两层插值噪声集中在中间；先展开其分布，避免小雨量只命中极少数尖峰。
        float noise = puddle_pattern(p);
        float threshold = 1.0 - weather_puddle_coverage * weather_puddles;
#ifdef RT
        float pixel = world_footprint;
        float pattern_pixel = pixel * max(weather_puddle_scale, 0.01);
        float shore = max(0.06, abs(puddle_pattern(p + vec2(pattern_pixel, 0.0)) - noise)
                              + abs(puddle_pattern(p + vec2(0.0, pattern_pixel)) - noise));
#else
        float shore = max(0.06, fwidth(noise));
#endif
        puddle = smoothstep(threshold - shore, threshold + shore, noise)
                 * smoothstep(0.02, 0.15, weather_puddles) * smoothstep(0.88, 0.98, up)
                 * step(0.0001, weather_puddle_coverage);
        if (puddle > 0.001) {
#ifdef RT
            ripple = rain_ripple_sample(position.xz, pixel);
#else
            ripple = rain_ripple_sample(position.xz, 0.0);
#endif
        }
    }
    return vec4(wet, puddle, ripple);
}

// 与 Godot 原生 PBR 相同的 GGX 分布、可见性及 Schlick Fresnel。
vec3 surface_direct_specular(vec3 normal, vec3 light, vec3 view, vec3 color, float attenuation, float roughness, vec3 f0) {
    float nl = max(dot(normal, light), 0.0);
    float nv = max(dot(normal, view), 0.001);
    vec3 h = normalize(light + view);
    float nh = max(dot(normal, h), 0.0);
    float vh = max(dot(view, h), 0.0);
    float alpha = max(roughness * roughness, 0.001);
    float alpha2 = alpha * alpha;
    float denom = nh * nh * (alpha2 - 1.0) + 1.0;
    float d = min(alpha2 / max(PI * denom * denom, 0.000000000001), 65504.0);
    float visibility = 0.5 / max(mix(2.0 * nl * nv, nl + nv, alpha), 0.00001);
    float f90 = clamp(dot(f0, vec3(16.5)), 0.0, 1.0);
    vec3 fresnel = f0 + (vec3(f90) - f0) * pow(1.0 - vh, 5.0);
    return color * attenuation * nl * d * visibility * fresnel;
}

// 水的 F0=0.02，不把非金属路面改成金属。
vec3 wet_direct_specular(vec3 normal, vec3 light, vec3 view, vec3 color, float attenuation, float roughness) {
    return surface_direct_specular(normal, light, view, color, attenuation, roughness, vec3(0.02));
}
