#[compute]
#version 450

// Invocations in the (x, y, z) dimension
layout (local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(binding = 0, rgba32f) readonly uniform image2D u_frameDisplacement;
layout(binding = 1, r32f) readonly uniform image2D u_lastDisplacement;
layout(binding = 2, r32f) writeonly restrict uniform image2D u_displacement;

// The code we want to execute in each invocation
void main() {
    ivec2 texelCoords = ivec2(gl_GlobalInvocationID.xy);
    vec4 sourceDisplacement = imageLoad(u_frameDisplacement, texelCoords);
    vec4 lastValue = imageLoad(u_lastDisplacement, texelCoords);
    if (sourceDisplacement.r > 0.1)
    {
        // float lastValue = imageLoad(u_displacement, texelCoords);
        imageStore(u_displacement, texelCoords, sourceDisplacement);
    }
    else
    {
        imageStore(u_displacement, texelCoords, lastValue);
    }
}