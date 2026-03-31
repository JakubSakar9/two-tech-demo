#[compute]
#version 450

const float K_MELT = 0.05;      // Snow melting rate (meters per a degree above melting point)
const float T0_MELT = 274.0;    // Snow melting point in Kelvins

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform image2D heightmap;

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 h_vec = imageLoad(heightmap, pixel);
    h_vec.g -= K_MELT * max(0.0, h_vec.a - T0_MELT);
    h_vec.g = max(0.0, h_vec.g);
    imageStore(heightmap, pixel, h_vec);   
}