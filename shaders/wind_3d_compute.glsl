#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 1, local_size_z = 16) in;

layout(set = 0, binding = 0, r32f) uniform readonly image2D heightmap;

layout(std140, set = 0, binding = 1) readonly buffer WindSSBOIn {
    vec4 surf_vec[ ];
};
layout(std140, set = 0, binding = 2) buffer WindSSBOOut {
    vec4 wind_vec[ ];
};

layout(push_constant, std430) uniform Params {
    vec2 w_base;        // Base wind velocity
    float k_venturi;    // Venturi effect strength
    float k_topo;       // Topographic effect strength
    float w_max;        // Max. wind speed
    float a_max;        // Max. terrain altitude
    float k_sky;        // Controls how much space is reserved for the velocity field above the maximum altitude
} params;

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    uint pz = gl_GlobalInvocationID.z;
    uint size = gl_NumWorkGroups.x * gl_WorkGroupSize.x;
    uint y_layers = gl_NumWorkGroups.y * gl_WorkGroupSize.y;
    float y_m = float(y_layers);
    uint idx2d = size * pz + px;
    uint idx3d = size * y_layers * pz + size * py + px;

    // Maximum velocity computation
    vec2 w_max_venturi = (1.0 + params.k_venturi * params.a_max) * params.w_base;
    w_max_venturi = w_max_venturi / params.w_max;
    vec3 w_max = normalize(vec3(w_max_venturi.x, 0.0, w_max_venturi.y));
    w_max = 0.5 * w_max + vec3(0.5);

    // float y = y_m - 1.0 - float(py);
    float y = float(py);
    float a = imageLoad(heightmap, ivec2(px, pz)).r;
    float y_l = y_m * a / ((1.0 + params.k_sky) * params.a_max);
    float y_f = floor(y_l - 0.5);
    vec3 w = vec3(0.5);
    vec3 w_surf = surf_vec[idx2d].xyz;
    if (y >= y_f) {
        w = mix(w_max, w_surf, (y_m - y - 0.5) / (y_m - y_l));
    }
    wind_vec[idx3d] = vec4(w, 1.0);
}