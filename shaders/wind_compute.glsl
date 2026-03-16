#[compute]
#version 450

const vec3 WIND_VEC = vec3(0.3, 0.0, 0.4);
const float K_VENTURI = 1.0;
const float MAX_FIELD_STRENGTH = 64.0;

layout(local_size_x = 16, local_size_y = 1, local_size_z = 16) in;

layout(set = 0, binding = 0, r32f) uniform readonly image2D heightmap;
layout(std140, binding = 1) buffer WindSSBOOut {
    vec4 wind_vec[ ];
};

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint pz = gl_GlobalInvocationID.z;
    uint size = gl_NumWorkGroups.x * gl_WorkGroupSize.x;
    uint idx = pz * size + px;

    ivec3 pixel = ivec3(px, 0, pz);
    float a = imageLoad(heightmap, ivec2(px, pz)).r;
    vec3 w_venturi = (1.0 + K_VENTURI * a) * WIND_VEC;
    
    vec3 w_dir = normalize(w_venturi);
    float strength = length(w_venturi);
    float mult = min(1.0, strength / MAX_FIELD_STRENGTH);
    vec3 w = (w_dir * mult + 1.0) / 2.0;
    
    wind_vec[idx] = vec4(w, 1.0);
}