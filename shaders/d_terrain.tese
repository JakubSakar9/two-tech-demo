#version 450

layout (triangles, equal_spacing, cw) in;

layout(location = 0) in vec3 tese_Normal[];
layout(location = 1) in vec2 tese_TexCoord[];

layout(location = 0) out vec3 frag_Position;
layout(location = 1) out vec3 frag_Normal;
layout(location = 2) out vec2 frag_TexCoord;

layout(binding = 0) uniform UniformBufferObject {
    mat4 model;
    // mat4 rotation;
    mat4 view;
    mat4 proj;
} u_ubo;

void main()
{
    float u = gl_TessCoord.x;
    float v = gl_TessCoord.y;
    float w = gl_TessCoord.z;

    // Position computation
    // TODO: Add displacement from a texture
    float displacement = 0;
    vec4 p0 = gl_in[0].gl_Position;
    vec4 p1 = gl_in[1].gl_Position;
    vec4 p2 = gl_in[2].gl_Position;
    vec4 p = p0 * u + p1 * v + p2 * w;
    p.y -= displacement;
    frag_Position = p.xyz / p.w;
    gl_Position = u_ubo.proj * u_ubo.view * p;
    
    // Normal computation
    vec3 n0 = tese_Normal[0];
    vec3 n1 = tese_Normal[1];
    vec3 n2 = tese_Normal[2];
    vec3 n = n0 * u + n1 * v + n2 * w;
    frag_Normal = n;

    // Tex coord computation
    vec2 tc0 = tese_TexCoord[0];
    vec2 tc1 = tese_TexCoord[1];
    vec2 tc2 = tese_TexCoord[2];
    frag_TexCoord = tc0 * u + tc1 * v + tc2 * w;
}