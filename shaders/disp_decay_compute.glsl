#[compute]
#version 450

layout(local_size_x = 8, local_size_y = 8, local_size_z = 1) in;

layout(set = 0, binding = 0, r32f) uniform image2D disp_tex;

layout(push_constant, std430) uniform Params {
    float decay_factor;
} params;

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 in_color = imageLoad(disp_tex, pixel);
    float intensity = in_color.r * params.decay_factor;
    vec4 out_color = vec4(intensity, 0.0, 0.0, 1.0);
    imageStore(disp_tex, pixel, out_color);
}
