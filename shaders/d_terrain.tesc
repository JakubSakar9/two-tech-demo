#version 450

layout(vertices = 3) out;

layout(location = 0) in vec3 tesc_Normal[];
layout(location = 1) in vec2 tesc_TexCoord[];

layout(location = 0) out vec3 tese_Normal[];
layout(location = 1) out vec2 tese_TexCoord[];

// layout(binding = 0) uniform UniformBufferObject {
//     mat4 model;
//     // mat4 rotation;
//     mat4 view;
//     mat4 proj;
// } u_ubo;

void main(){
    gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
    tese_Normal[gl_InvocationID]  = tesc_Normal[gl_InvocationID];
    tese_TexCoord[gl_InvocationID] = tesc_TexCoord[gl_InvocationID];

    gl_TessLevelOuter[0] = 1;
    gl_TessLevelOuter[1] = 1;
    gl_TessLevelOuter[2] = 1;

    gl_TessLevelInner[0] = 1;

}