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

layout(binding = 1) uniform usampler2D u_displacedPatches;

void main(){
    gl_out[gl_InvocationID].gl_Position = gl_in[gl_InvocationID].gl_Position;
    tese_Normal[gl_InvocationID]  = tesc_Normal[gl_InvocationID];
    tese_TexCoord[gl_InvocationID] = tesc_TexCoord[gl_InvocationID];

    // NOTE: This naive approach will unfortunately lead to holes a better solution would be to use Image2D
    uvec4 patchValue = texture(u_displacedPatches, (tesc_TexCoord[0] + tesc_TexCoord[1] + tesc_TexCoord[2]) / 3.0);
    if (patchValue.x > 127)
    {
        gl_TessLevelOuter[0] = 64;
        gl_TessLevelOuter[1] = 64;
        gl_TessLevelOuter[2] = 64;

        gl_TessLevelInner[0] = 64;
    }
    else
    {
        gl_TessLevelOuter[0] = 1;
        gl_TessLevelOuter[1] = 1;
        gl_TessLevelOuter[2] = 1;

        gl_TessLevelInner[0] = 1;
    }

}