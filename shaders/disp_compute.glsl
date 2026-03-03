#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, r32f) uniform image2D disp_tex;
layout(set = 0, binding = 1) uniform sampler2D fp_tex;

layout(push_constant, std430) uniform Params {
    mat2 rotation_mat;
    vec2 center_left;
    vec2 center_right;
    float depth_left;
    float depth_right;
    uint tex_size;
    float downscale_factor;
} params;

void main() {
    uint tex_width = params.tex_size;
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    vec2 uv1 = vec2(float(px) / float(tex_width), float(py) / float(tex_width));
    vec2 uv2 = params.rotation_mat * (params.downscale_factor * (uv1 - params.center_left)) + 0.5;
    vec2 uv3 = params.rotation_mat * (params.downscale_factor * (uv1 - params.center_right)) + 0.5;

    ivec2 pixel = ivec2(px, py);
    vec4 in_color = imageLoad(disp_tex, pixel);

    float intensity1 = texture(fp_tex, uv2).r * params.depth_left;
    float intensity2 = texture(fp_tex, uv3).r * params.depth_right;
    float intensity = max(intensity1, intensity2);
    intensity = max(intensity, in_color.r);
    vec4 out_color = vec4(intensity, 0.0, 0.0, 1.0);
    imageStore(disp_tex, pixel, out_color);
}
