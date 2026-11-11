#[compute]
#version 460
layout(local_size_x = 64) in;
layout(rg32ui, set = 0, binding = 0) uniform writeonly uimage2D probe_epochs;
layout(set = 0, binding = 1, std430) readonly buffer Invalidations { uint dirty[]; };
layout(push_constant, std430) uniform Params { vec4 origin_spacing; uvec4 counts_rays; vec4 sky_distance; uvec4 batch; vec4 options; } params;
void main() {
    uint id = gl_GlobalInvocationID.x;
    uint width = params.counts_rays.x * params.counts_rays.z;
    if (id >= width * params.counts_rays.y || dirty[id] == 0u) return;
    imageStore(probe_epochs, ivec2(id % width, id / width), uvec4(0u));
}
