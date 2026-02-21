#version 450

layout(location = 0) in vec3 frag_Normal;
layout(location = 1) in vec2 frag_TexCoord;

layout(location = 0) out vec4 out_Color;

void main()
{
    out_Color = vec4(frag_TexCoord.x, frag_TexCoord.y, 1.0, 1.0);
}