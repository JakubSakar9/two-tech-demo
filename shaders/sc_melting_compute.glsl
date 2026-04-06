#[compute]
#version 450

const float SNOW_LIMIT = 0.5; // Highest possible snow value, used to prevent extreme snow spikes

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform image2D heightmap;

layout(push_constant, std430) uniform Params {
    float k_melt;   // Snow melting rate (meters per a degree above melting point)
    float t0_melt;  // Snow melting rate in kelvins
} params;

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 h_vec = imageLoad(heightmap, pixel);
    float old_pow = h_vec.b;
    h_vec.b -= params.k_melt * max(0.0, h_vec.a - params.t0_melt);
    h_vec.b = max(0.0, h_vec.b);
    h_vec.g += old_pow - h_vec.b;
    h_vec.g -= params.k_melt * max(0.0, h_vec.a - params.t0_melt);
    h_vec.g = max(0.0, h_vec.g);
    h_vec.g = min(SNOW_LIMIT, h_vec.g);
    h_vec.b = min(SNOW_LIMIT - h_vec.g, h_vec.b);
    imageStore(heightmap, pixel, h_vec);   
}