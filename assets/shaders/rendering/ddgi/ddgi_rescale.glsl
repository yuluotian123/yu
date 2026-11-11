#[compute]
#version 460
layout(local_size_x = 8, local_size_y = 8) in;
layout(rgba16f, set = 0, binding = 0) uniform image2D irradiance_atlas;
layout(push_constant, std430) uniform Params { vec4 scale; } params;
void main() {
    ivec2 pixel = ivec2(gl_GlobalInvocationID.xy);
    if (any(greaterThanEqual(pixel, imageSize(irradiance_atlas)))) return;
    vec4 value = imageLoad(irradiance_atlas, pixel);
    imageStore(irradiance_atlas, pixel, vec4(value.rgb * params.scale.x, value.a));
}
