#[compute]
#version 450

const float SC0 = 0.5;          // Base critical slope
const float K_SC = 0.05;        // Temperature influence on the critical slope
const float T0_POW = 263.0;     // Lowest temperature influencing the critical slope
const float K_SLOPE = 3.0;      // Multiplier affecting strength of slope influence

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform image2D heightmap;

layout(push_constant, std430) uniform Params {
    float a_max;        // Max. terrain altitude
    float a0_precip;    // Altitude at which it starts snowing
    float sh_max;       // Max. snow height for the current precipitation phase
    float k_pow;        // Ratio of snow reduced by slopes that becomes powdery
} params;

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
    float dx = sqrt((a_px - a) * (a_px - a) + (a - a_mx) * (a - a_mx)) / 2.0;
    float dz = sqrt((a_pz - a) * (a_pz - a) + (a - a_mz) * (a - a_mz)) / 2.0;
    return length(vec2(dx, dz));
}

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 h_vec = imageLoad(heightmap, pixel);
    float a = h_vec.r;
    float d = (params.sh_max / (params.a_max - params.a0_precip)) * (a - params.a0_precip);

    float sc =  SC0 + K_SC * max(T0_POW - h_vec.a, 0.0);
    float slope_multiplier = K_SLOPE * (sc - get_slope(uvec2(px, py), a));
    float d_new = d * clamp(slope_multiplier, 0.0, 1.0);
    h_vec.g += d_new;
    h_vec.b += params.k_pow * (d - d_new);
    imageStore(heightmap, pixel, h_vec);
}