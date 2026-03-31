#[compute]
#version 450

const float K_SUN = 0.5;    // Temperature increase per one hour of direct sunlight
const float K_T = 0.5;      // Temperature decrease per meter of altitude
const float T0 = 279.0;    // Temperature at sea level in Kelvin

const uint N_HOURS = 13;

const vec3[] I_VECS = vec3[13](
    vec3( 0.00959898,  0.99993527,  0.00610861),
    vec3(-0.19079076,  0.96799648,  0.16303897),
    vec3(-0.37811029,  0.87028633,  0.31564904),
    vec3(-0.53943952,  0.7132669 ,  0.44749897),
    vec3(-0.66385069,  0.50755218,  0.54926592),
    vec3(-0.74284792,  0.26719764,  0.61382603),
    vec3(-0.77064904,  0.00874311,  0.63719983),
    vec3(-0.74588071, -0.25029218,  0.61726476),
    vec3(-0.66996166, -0.49243587,  0.55557023),
    vec3(-0.5481508 , -0.70076032,  0.45658041),
    vec3(-0.38864864, -0.86142934,  0.32694301),
    vec3(-0.20214694, -0.96342199,  0.17593946),
    vec3(-0.00174504, -0.99983585,  0.01803409)
);

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform image2D heightmap;

vec3 surf_normal(uvec2 p2, float a) {
    uint size = gl_NumWorkGroups.x * gl_WorkGroupSize.x;

    float a_mx = a;
    float a_px = a;
    float a_mz = a;
    float a_pz = a;

    if (p2.x > 0) {
        a_mx = imageLoad(heightmap, ivec2(p2.x - 1, p2.y)).r;
    }
    if (p2.x < size - 1) {
        a_px = imageLoad(heightmap, ivec2(p2.x + 1, p2.y)).r;
    }
    if (p2.y > 0) {
        a_mz = imageLoad(heightmap, ivec2(p2.x, p2.y - 1)).r;
    }
    if (p2.y < size - 1) {
        a_pz = imageLoad(heightmap, ivec2(p2.x, p2.y + 1)).r;
    }

    return normalize(vec3(a_px - a_mx, 2, a_pz - a_mz));    
}

void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 h_vec = imageLoad(heightmap, pixel);
    vec3 n = surf_normal(uvec2(px, py), h_vec.r);
    float I = 0.0;
    for (uint i = 0; i < N_HOURS; i++) {
        I += max(0.0, dot(n, -I_VECS[i]));
    }
    I *= K_SUN;
    h_vec.a = T0 - K_T * h_vec.r + I;
    // Debug normalization:
    // h_vec.a = (h_vec.a - 255.0) / 30.0;
    imageStore(heightmap, pixel, h_vec);
}