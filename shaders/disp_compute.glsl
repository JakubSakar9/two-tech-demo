#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, r32f) restrict uniform image2D disp_tex;

layout(push_constant, std430) uniform Params {
    uint tex_size;
    vec2 mouse_pos;
} params;


void main() {
    uint tex_width = params.tex_size;
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);

    vec2 uv = vec2(px / float(tex_width), py / float(tex_width));
    float dist = distance(uv, params.mouse_pos);
    float intensity = (1.0 - clamp(20 * dist, 0.0, 1.0));
    vec4 out_color = vec4(intensity, 0.0, 0.0, 1.0);
    imageStore(disp_tex, pixel, out_color);
}
