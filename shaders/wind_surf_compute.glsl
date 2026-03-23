#[compute]
#version 450

/// Base wind vector at sea level
const vec2 WIND_VEC = vec2(0.3, 0.4);
// Venturi effect strength (very high here for demostration purposes)
const float K_VENTURI = 1.0; // TODO: Pass using push constants
/// Topography effect on wind
const float K_TERRAIN = 0.6;
/// Maximum strength for the wind vector field particle attractor. Used for normalization.
const float MAX_FIELD_STRENGTH = 32.0;
// Offset used to approximate wind shadowing
const float V_FALLOFF_OFFSET = 0.2;
// Multiplier that increases the upwards motion of the wind along slopes
const float VERTICAL_BOOST = 2.0;

layout(local_size_x = 16, local_size_y = 1, local_size_z = 16) in;

layout(set = 0, binding = 0, r32f) uniform readonly image2D heightmap;
layout(std140, binding = 1) buffer WindSSBOOut {
    vec4 wind_vec[ ];
};

vec2 get_a_grad(uvec2 p2, uint size, float a)
{
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
    return vec2(a_px - a_mx, a_pz - a_mz) / 2.0;
}

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint pz = gl_GlobalInvocationID.z;
    uint size = gl_NumWorkGroups.x * gl_WorkGroupSize.x;

    vec3 p = vec3(px, imageLoad(heightmap, ivec2(px, pz)).r, pz);
    float a = p.y;

    vec2 a_grad = get_a_grad(uvec2(px, pz), size, a);

    vec3 n = normalize(vec3(a_grad.x, 1, a_grad.y));
    vec2 n_xz = n.xz;
    vec2 n_xz_p = vec2(n_xz.y, -n_xz.x);

    // Venturi effect
    vec2 w_venturi = (1.0 + K_VENTURI * a) * WIND_VEC;
    // Topographic effect
    if (dot(w_venturi, n_xz_p) < 0) n_xz_p = -n_xz_p;
    vec2 w_topo = w_venturi * (1 - length(n_xz)) + K_TERRAIN * length(w_venturi) * n_xz_p;
    
    // Vertical wind calculation
    float w_vert = dot(w_topo, a_grad);
    float offset = length(w_topo) * V_FALLOFF_OFFSET;
    if (w_vert < 0) {
        w_vert = min(0, w_vert + offset);
    }
    else {
        w_vert = w_vert * VERTICAL_BOOST;
    }

    // Final wind calculation and interpolation
    vec3 w_a = vec3(w_topo.x, w_vert, w_topo.y); // Wind at the terrain altitude
    vec3 w_dir = normalize(w_a);
    float strength = length(w_a);
    float mult = min(1.0, strength / MAX_FIELD_STRENGTH);
    vec3 w = (w_dir * mult + 1.0) / 2.0;
    
    uint idx = pz * size + px;
    wind_vec[idx] = vec4(w, 1.0);
}