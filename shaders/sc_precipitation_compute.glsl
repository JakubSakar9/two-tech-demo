#[compute]
#version 450

const float A_MAX = 48.0;       // Max. terrain altitude
const float A0_PRECIP = 8.0;    // Altitude at which it starts snowing
const float SH_MAX = 0.3;       // Max. snow height
const float K_SC = 0.7;         // Cosine of the critical snow angle

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform image2D heightmap;

float get_slope(uvec2 p2, float a) {
    uint size = gl_NumWorkGroups.x * gl_WorkGroupSize.x;

    float a_mx = a;
    float a_px = a;
    float a_mz = a;
    float a_pz = a;

    if (p2.x > 0) {
        a_mx = imageLoad(heightmap, ivec2(p2.x - 1, p2.y)).r;
    }
    if (p2.x < size - 1) {
        a_px = imageLoad(heightmap, ivec2(p2.x + 1, p2.y)).r;
    }
    if (p2.y > 0) {
        a_mz = imageLoad(heightmap, ivec2(p2.x, p2.y - 1)).r;
    }
    if (p2.y < size - 1) {
        a_pz = imageLoad(heightmap, ivec2(p2.x, p2.y + 1)).r;
    }
    float dx = max(a_px - a, a - a_mx);
    float dz = max(a_pz - a, a - a_mz);
    return length(vec2(dx, dz));
}

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 h_vec = imageLoad(heightmap, pixel);
    float a = h_vec.r;
    float d = (SH_MAX / (A_MAX - A0_PRECIP)) * (a - A0_PRECIP);
    float slope_multiplier = 2.5 * (K_SC - get_slope(uvec2(px, py), a));
    d *= clamp(slope_multiplier, 0.0, 1.0);
    d = max(0.0, d);
    h_vec.g = d;
    imageStore(heightmap, pixel, h_vec);
}