#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, r32f) uniform image2D disp_tex;
layout(set = 0, binding = 1) uniform sampler2D fp_tex;

layout(push_constant, std430) uniform Params {
    mat2 rotation_mat;
    uint tex_size;
    float carve_depth;
    vec2 sprite_center;
    vec2 texture_offset;
    bool flip_sprite;
} params;

const float downscale_factor = 64.0;

void main() {
    uint tex_width = params.tex_size;
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    vec2 uv1 = vec2(float(px) / float(tex_width), float(py) / float(tex_width));
    vec2 uv2 = params.rotation_mat * (downscale_factor * (uv1 - params.sprite_center)) + 0.5;
    if (params.flip_sprite) {
        uv2 = vec2(-uv2.x, uv2.y);
    }

    ivec2 pixel = ivec2(px, py);
    vec4 in_color = imageLoad(disp_tex, pixel + ivec2(params.tex_size * params.texture_offset));

    float intensity = texture(fp_tex, uv2).r * params.carve_depth;
    intensity = max(intensity, in_color.r);
    vec4 out_color = vec4(intensity, 0.0, 0.0, 1.0);
    imageStore(disp_tex, pixel, out_color);
}
