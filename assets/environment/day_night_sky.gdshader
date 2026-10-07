shader_type sky;
// Clouds use the author's scene mesh and separate spatial material.

uniform vec3 day_zenith : source_color = vec3(0.025, 0.15, 0.65);
uniform vec3 day_horizon : source_color = vec3(0.12, 0.58, 1.0);
uniform vec3 day_ground : source_color = vec3(0.30, 0.66, 0.88);
uniform vec3 sunset_horizon : source_color = vec3(1.0, 0.44, 0.22);
uniform vec3 night_zenith : source_color = vec3(0.07, 0.11, 0.29);
uniform vec3 night_horizon : source_color = vec3(0.20, 0.25, 0.44);
uniform vec3 sun_direction = vec3(-0.109381, 0.965926, 0.23457);
uniform vec3 moon_direction = vec3(0.109381, -0.965926, -0.23457);
uniform float daylight = 1.0;
uniform float twilight = 0.0;
uniform float sun_radius = 0.015708;
uniform float moon_radius = 0.026180;
uniform float moon_visibility = 0.0;
uniform float stars_visibility = 0.0;
uniform float star_intensity = 1.0;
uniform float star_rotation = 0.0;
uniform float moon_light = 0.0;
uniform float horizon_haze : hint_range(0.0, 1.0) = 0.45;
uniform float horizon_softness : hint_range(0.05, 0.8) = 0.3;
uniform float mie_strength : hint_range(0.0, 1.0) = 0.18;
uniform float mie_anisotropy : hint_range(0.0, 0.95) = 0.76;

float hash21(vec2 p) {
    return fract(sin(dot(p, vec2(127.1, 311.7))) * 43758.5453);
}

// 将天球切平面映射到像素空间再正交化：正交镜头与画面边缘都保持圆盘。
// 保留切平面的朝向，让月相亮面仍指向太阳。
vec2 round_disc_uv(vec2 tangent_uv) {
    vec2 dx = dFdx(tangent_uv);
    vec2 dy = dFdy(tangent_uv);
    float determinant = dx.x * dy.y - dx.y * dy.x;
    if (abs(determinant) < 0.0000001) { return tangent_uv; }
    vec2 pixels = vec2(dy.y * tangent_uv.x - dy.x * tangent_uv.y, -dx.y * tangent_uv.x + dx.x * tangent_uv.y) / determinant;
    vec2 axis_x = normalize(dx);
    vec2 axis_y = vec2(-axis_x.y, axis_x.x) * sign(determinant);
    return (axis_x * pixels.x + axis_y * pixels.y) * sqrt(abs(determinant));
}

// Art-directed scattering approximation: Rayleigh angular response plus a bounded
// Henyey-Greenstein forward lobe. The palette remains authored, rather than a planet LUT.
vec3 atmosphere(vec3 eye) {
    float mu = clamp(dot(eye, sun_direction), -1.0, 1.0);
    float altitude = sqrt(eye.y * eye.y + 0.0004) - 0.02;
    float air_mass = exp(-altitude / max(horizon_softness, 0.05));
    vec3 horizon = mix(night_horizon, day_horizon, daylight);
    // Broad pale blue near the horizon, saturated blue above it.
    vec3 haze_tint = mix(night_horizon, mix(day_horizon, vec3(0.72, 0.83, 0.94), 0.48), daylight);
    horizon = mix(horizon, haze_tint, horizon_haze);
    float sun_side = pow(max(dot(normalize(vec3(eye.x, 0.001, eye.z)),
        normalize(vec3(sun_direction.x, 0.001, sun_direction.z))), 0.0), 3.0);
    horizon = mix(horizon, sunset_horizon, twilight * (0.2 + sun_side * 0.65));
    vec3 zenith = mix(night_zenith, day_zenith, daylight);
    float rayleigh = 0.75 * (1.0 + mu * mu);
    zenith *= mix(1.0, rayleigh, 0.16 * daylight);
    vec3 upper = mix(zenith, horizon, air_mass);
    // The lower hemisphere remains close to the horizon's radiance. No abs(y)^p cusp
    // or dark ground hemisphere meeting the sky along a sharp equatorial line.
    vec3 lower = mix(horizon, mix(night_horizon * 0.8, day_ground, daylight),
        smoothstep(0.04, 0.85, -eye.y) * 0.22);
    vec3 color = mix(lower, upper, smoothstep(-0.025, 0.025, eye.y));
    float g = clamp(mie_anisotropy, 0.0, 0.95);
    float mie = pow(1.0 - g, 3.0) / pow(max(1.0 + g * g - 2.0 * g * mu, 0.001), 1.5);
    vec3 sun_tint = mix(vec3(1.0, 0.82, 0.62), sunset_horizon, twilight);
    return color + sun_tint * mie * mie_strength * daylight * (0.4 + air_mass * 0.6);
}

void sky() {
    vec3 eye = normalize(EYEDIR);
    if (AT_CUBEMAP_PASS) {
        // 环境光采样天空；天体直射不重复烘入 IBL。
        COLOR = atmosphere(eye);
    } else {
    COLOR = atmosphere(eye);

    // 天体直射由 DirectionalLight3D 提供，避免烘入 IBL 后重复照明。
    if (!AT_CUBEMAP_PASS) {
        float horizon_mask = smoothstep(-0.005, 0.02, eye.y);
        float sun_dot = dot(eye, sun_direction);
        vec3 sun_right = normalize(cross(vec3(0.0, 1.0, 0.0), sun_direction));
        vec3 sun_up = cross(sun_direction, sun_right);
        vec2 sun_uv = round_disc_uv(vec2(dot(eye, sun_right), dot(eye, sun_up)) / sin(sun_radius));
        float sun_radius2 = dot(sun_uv, sun_uv);
        float sun_edge = max(fwidth(sun_radius2), 0.002);
        float disc = (1.0 - smoothstep(1.0 - sun_edge, 1.0 + sun_edge, sun_radius2)) * step(0.0, sun_dot);
        float halo = pow(max(sun_dot, 0.0), 512.0) * 0.15;
        COLOR += vec3(1.0, 0.89, 0.70) * (disc * 16.0 + halo) * daylight * horizon_mask;

        vec3 moon_right = normalize(cross(vec3(0.0, 1.0, 0.0), moon_direction));
        vec3 moon_up = cross(moon_direction, moon_right);
        vec2 lunar_uv = round_disc_uv(vec2(dot(eye, moon_right), dot(eye, moon_up)) / sin(moon_radius));
        float radius2 = dot(lunar_uv, lunar_uv);
        float edge = max(fwidth(radius2), 0.002);
        float moon_mask = (1.0 - smoothstep(1.0 - edge, 1.0 + edge, radius2)) * step(0.0, dot(eye, moon_direction)) * moon_visibility * horizon_mask;
        if (moon_mask > 0.0) {
            // 朝向观察者的球面，与实际太阳方向点乘，得到连续且有方向的盈亏。
            vec3 normal = moon_right * lunar_uv.x + moon_up * lunar_uv.y - moon_direction * sqrt(max(0.0, 1.0 - radius2));
            float illumination = max(dot(normal, sun_direction), 0.0);
            float maria = 0.82 + 0.12 * sin(lunar_uv.x * 17.0 + sin(lunar_uv.y * 11.0)) * sin(lunar_uv.y * 21.0);
            vec3 lunar_color = vec3(0.82, 0.86, 0.94) * maria * illumination * 8.0;
            COLOR = mix(COLOR, COLOR * 0.08 + lunar_color, moon_mask);
        }

        // 使用立方体投影，避免经纬网投影在天顶把星点拉成长线。
        float cs = cos(star_rotation);
        float sn = sin(star_rotation);
        vec3 star_eye = vec3(cs * eye.x - sn * eye.z, eye.y, sn * eye.x + cs * eye.z);
        vec3 axis = abs(star_eye);
        vec2 face_uv;
        float face;
        if (axis.x >= axis.y && axis.x >= axis.z) {
            face_uv = star_eye.zy / axis.x;
            face = star_eye.x > 0.0 ? 0.0 : 1.0;
        } else if (axis.y >= axis.z) {
            face_uv = star_eye.xz / axis.y;
            face = star_eye.y > 0.0 ? 2.0 : 3.0;
        } else {
            face_uv = star_eye.xy / axis.z;
            face = star_eye.z > 0.0 ? 4.0 : 5.0;
        }
        vec2 star_uv = (face_uv * 0.5 + 0.5) * 256.0;
        vec2 cell = floor(star_uv) + vec2(face * 263.0, face * 181.0);
        float seed = hash21(cell);
        vec2 offset = vec2(hash21(cell + 17.0), hash21(cell + 43.0)) * 0.6 + 0.2;
        float distance_to_star = length(fract(star_uv) - offset);
        float pixel = clamp(length(fwidth(star_uv)), 0.01, 0.7);
        float star = (1.0 - smoothstep(0.02, 0.08 + pixel * 0.35, distance_to_star)) * step(0.995, seed);
        COLOR += mix(vec3(0.68, 0.80, 1.0), vec3(1.0, 0.86, 0.68), hash21(cell + 71.0)) * star * 20.0 * stars_visibility * star_intensity * horizon_mask * (1.0 - moon_mask);
    }
    }
}
