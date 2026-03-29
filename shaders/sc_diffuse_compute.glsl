#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform readonly image2D heightmap_in;
layout(set = 0, binding = 1, rgba32f) uniform image2D heightmap_out;

void main() {
    
}