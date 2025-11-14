#version 450

layout(location = 0) in vec3 frag_position;
layout(location = 1) in vec3 frag_normal;
layout(location = 2) in vec2 frag_uv;

layout(location = 0) out vec4 out_color;

void main()
{
    // out_color = vec4((frag_normal + vec3(1.0, 0.0, 1.0)) / 2.0, 1.0);
    out_color = vec4((frag_normal + 1) / 2.0, 1.0);
    // out_color = vec4(frag_normal, 1.0);
    // out_color = vec4(1.0, 1.0, 0.0, 1.0);
}