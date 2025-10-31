#version 450

layout(binding = 0) uniform UniformBufferObject {
    mat4 model;
    // mat4 rotation;
    mat4 pv;
} u_ubo;

layout(location = 0) in vec3 in_position;
layout(location = 1) in vec3 in_normal;
layout(location = 2) in vec2 in_uv;

layout(location = 0) out vec3 frag_position;
layout(location = 1) out vec3 frag_normal;
layout(location = 2) out vec2 frag_uv;

void main() {
    mat4 pvm = u_ubo.pv * u_ubo.model;
    gl_Position = pvm * vec4(in_position, 1.0);
    frag_position = in_position;
    // frag_normal = (u_ubo.rotation * vec4(in_normal, 1.0)).xyz;
    frag_normal = in_normal;
    frag_uv = in_uv;
}