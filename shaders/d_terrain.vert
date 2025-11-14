#version 450

layout(location = 0) in vec3 in_Position;
layout(location = 1) in vec3 in_Normal;
layout(location = 2) in vec2 in_TexCoord;

layout(location = 0) out vec3 tesc_Normal;
layout(location = 1) out vec2 tesc_TexCoord;

// layout(location = 0) out vec3 frag_Position;
// layout(location = 1) out vec3 frag_Normal;
// layout(location = 2) out vec2 frag_TexCoord;


layout(binding = 0) uniform UniformBufferObject {
    mat4 model;
    // mat4 rotation;
    mat4 view;
    mat4 proj;
} u_ubo;

void main() {
    gl_Position = u_ubo.model * vec4(in_Position, 1.0);
    tesc_Normal = in_Normal;
    tesc_TexCoord = in_TexCoord;
    // vec4 p = u_ubo.model * vec4(in_Position, 1.0);
    // frag_Position = p.xyz;
    // // frag_Normal = in_Normal;
    // frag_Normal = in_Position;
    // frag_TexCoord = in_TexCoord;
    // gl_Position = u_ubo.proj * u_ubo.view * p;
}