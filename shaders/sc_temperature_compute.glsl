#[compute]
#version 450

const float K_SUN = 0.5;    // Temperature increase per one hour of direct sunlight
const float K_T = 0.5;      // Temperature decrease per meter of altitude
const float T0 = 279.0;    // Temperature at sea level in Kelvin

// >>> cds_c
// array([[ 0.00959898,  0.99993527,  0.00610861],
//        [-0.19079076,  0.96799648,  0.16303897],
//        [-0.37811029,  0.87028633,  0.31564904],
//        [-0.53943952,  0.7132669 ,  0.44749897],
//        [-0.66385069,  0.50755218,  0.54926592],
//        [-0.74284792,  0.26719764,  0.61382603],
//        [-0.77064904,  0.00874311,  0.63719983],
//        [-0.74588071, -0.25029218,  0.61726476],
//        [-0.66996166, -0.49243587,  0.55557023],
//        [-0.5481508 , -0.70076032,  0.45658041],
//        [-0.38864864, -0.86142934,  0.32694301],
//        [-0.20214694, -0.96342199,  0.17593946],
//        [-0.00174504, -0.99983585,  0.01803409]])

layout(local_size_x = 16, local_size_y = 16, local_size_z = 1) in;

layout(set = 0, binding = 0, rgba32f) uniform image2D heightmap;


void main() {
    uint px = gl_GlobalInvocationID.x;
    uint py = gl_GlobalInvocationID.y;
    ivec2 pixel = ivec2(px, py);
    vec4 h_vec = imageLoad(heightmap, pixel);
    h_vec.a = T0 - K_T * h_vec.x;
    imageStore(heightmap, pixel, h_vec);
}