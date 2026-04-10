#[compute]
#version 450

const float K_WSLOPE = 0.5; // Highest slope enabling wind advection

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform readonly image2D heightmap_in;
layout(set = 0, binding = 1, rgba32f) uniform image2D heightmap_out;
layout(set = 0, binding = 2, rgba32f) uniform readonly image2D wind_surface;

layout(push_constant, std430) uniform Params {
    float max_wind_speed;   // Determines the multiplier for the wind velocity values sampled from the texture (stored velocities have length of less than one)
    float step_multiplier;  // Wind vector multiplier for this advection step, determines the influence of wind on powdery snow transfer
} params;

void advect_lossy() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec2 w = (imageLoad(wind_surface, pixel).xz - vec2(0.5)) * 2.0 * params.max_wind_speed * params.step_multiplier;
    float w_len = length(w);
    ivec2 pixel_src = ivec2(vec2(pixel) - w);
    vec4 h = imageLoad(heightmap_in, pixel);
    vec4 h_src = imageLoad(heightmap_in, pixel_src);
    float a = h.r;
    float a_src = h_src.r;
    float r_in  = clamp((K_WSLOPE - (a - a_src) / w_len) / K_WSLOPE, 0.0, 1.0);
    h.b = h_src.b * r_in;
    imageStore(heightmap_out, pixel, h);
}

void advect_lossless() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec2 w = (imageLoad(wind_surface, pixel).xz - vec2(0.5)) * 2.0 * params.max_wind_speed * params.step_multiplier;
    float w_len = length(w);
    vec4 h = imageLoad(heightmap_in, pixel);
    float a = h.r + h.g + h.b;
    vec2 src_f = vec2(pixel) - w;
    ivec2 src00 = ivec2(floor(src_f));
    ivec2 src10 = src00 + ivec2(1, 0);
    ivec2 src01 = src00 + ivec2(0, 1);
    ivec2 src11 = src00 + ivec2(1, 1);
    vec2 frac_src = fract(src_f);
    vec3 rgb00 = imageLoad(heightmap_in, src00).rgb;
    vec3 rgb10 = imageLoad(heightmap_in, src10).rgb;
    vec3 rgb01 = imageLoad(heightmap_in, src01).rgb;
    vec3 rgb11 = imageLoad(heightmap_in, src11).rgb;
    vec3 rgb0 = mix(rgb00, rgb10, frac_src.x);
    vec3 rgb1 = mix(rgb01, rgb11, frac_src.x);
    vec3 h_src = mix(rgb0, rgb1, frac_src.y);
    float a_src = h_src.r + h_src.g + h_src.b;

    vec2 mir_f = vec2(pixel) + w;
    ivec2 mir00 = ivec2(floor(mir_f));
    ivec2 mir10 = mir00 + ivec2(1, 0);
    ivec2 mir01 = mir00 + ivec2(0, 1);
    ivec2 mir11 = mir00 + ivec2(1, 1);
    vec2 frac_mir = fract(mir_f);
    float b00 = imageLoad(heightmap_in, mir00).r;
    float b10 = imageLoad(heightmap_in, mir10).r;
    float b01 = imageLoad(heightmap_in, mir01).r;
    float b11 = imageLoad(heightmap_in, mir11).r;
    float b0 = mix(b00, b10, frac_mir.x);
    float b1 = mix(b01, b11, frac_mir.x);
    float a_mir = mix(b0, b1, frac_mir.y);

    float r_in  = clamp((K_WSLOPE - (a - a_src) / w_len) / K_WSLOPE, 0.0, 1.0);
    float r_out = clamp((K_WSLOPE - (a_mir - a) / w_len) / K_WSLOPE, 0.0, 1.0);
    h.b *= (1.0 - r_out);
    h.b += h_src.y * r_in;
    imageStore(heightmap_out, pixel, h);
}

void main() {
    advect_lossless();
}