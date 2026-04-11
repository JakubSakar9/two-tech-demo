#[compute]
#version 450

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, r8) uniform image2D disp_tex;
layout(set = 0, binding = 1) uniform sampler2D fp_tex;
layout(std140, binding = 2) buffer FootprintData {
    vec4 fp_data[ ];
};

layout(push_constant, std430) uniform Params {
    ivec2 chunk;
    uint tex_size;
    int fp_count;
    float downscale_factor;
} params;

void main() {
    uint tex_width = params.tex_size;
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 in_color = imageLoad(disp_tex, pixel);
    vec2 uv1 = vec2(float(px) / float(tex_width), float(py) / float(tex_width));
    float intensity = in_color.r;

    for (uint i = 0; i < params.fp_count; i++) {
        float a = fp_data[i].a;
        mat2 rotmat = mat2(cos(a), -sin(a), sin(a), cos(a));
        vec2 center = fp_data[i].rg - vec2(params.chunk);
        vec2 uv2 = rotmat * (params.downscale_factor * (uv1 - center)) + 0.5;
        intensity = max(intensity, texture(fp_tex, uv2).r * fp_data[i].b);
    }
    vec4 out_color = vec4(intensity, 0.0, 0.0, 1.0);
    imageStore(disp_tex, pixel, out_color);
}
