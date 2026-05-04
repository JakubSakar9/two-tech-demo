shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

const int DISP_COUNT = 25;
const float HEIGHTMAP_RANGE = 768.0;
const float DETAIL_SIZE = 0.01;
const float JITTER_STRENGTH = 1.0;

uniform float roughness : hint_range(0.0, 1.0);
uniform sampler2D texture_roughness : hint_roughness_r, filter_linear_mipmap, repeat_enable;

uniform sampler2D height_map: source_color, filter_linear_mipmap;
uniform sampler2D glitter_map: source_color, filter_nearest;
uniform sampler2D grass_texture: filter_linear_mipmap_anisotropic;
uniform sampler2D rock_texture: filter_linear_mipmap_anisotropic;
uniform sampler2D[DISP_COUNT] displacement_maps: source_color, filter_linear_mipmap, repeat_disable;

uniform bool[DISP_COUNT] used_maps;
uniform vec2 chunk_origin = vec2(0.0);
uniform float displacement_map_range = 64.0;
uniform float near_fadeout_distance = 96.0;
uniform float far_fadeout_distance = 128.0;
uniform float displacement_darkening = 0.0;
uniform float rock_fade_start = 11.0;
uniform float rock_fade_end = 12.0;
uniform int disp_row = 5;

varying vec2 terrain_uv;
varying vec2 disp_uv;
varying vec3 debug_color;
varying float disp_flatness;
varying float displacement;
varying float height;
varying float snowiness;

void vertex() {
	vec4 transformedPosition = MODEL_MATRIX * vec4(VERTEX, 1.0);
	transformedPosition = transformedPosition / transformedPosition.w;
	vec2 unshifted_uv = vec2(transformedPosition.x, transformedPosition.z);
	vec2 shifted_uv = unshifted_uv - chunk_origin;
	terrain_uv = shifted_uv / HEIGHTMAP_RANGE - vec2(0.5);
	
	disp_uv = unshifted_uv / displacement_map_range + vec2(0.5);
	vec2 rdisp_uv = mod(disp_uv, vec2(1.0)) + vec2(1.0);
	rdisp_uv = mod(rdisp_uv, vec2(1.0));
	ivec2 high_vec = ivec2(1000000 * disp_row);
	ivec2 chunk = (ivec2(floor(disp_uv)) + high_vec + disp_row / 2) % disp_row;
	int tidx = chunk.y * disp_row + chunk.x;
	
	vec4 height_val = texture(height_map, terrain_uv);
	height = height_val.r;
	float snow_height = height_val.g + height_val.b;
	
	float displacement_v = 0.0;
	if (used_maps[tidx] && length(VERTEX) <= far_fadeout_distance) {
		displacement_v = texture(displacement_maps[tidx], rdisp_uv).r;
	}
	
	float snow_offset = (1.0 - displacement_v) * snow_height;
	VERTEX.y = height + snow_offset;
	
	debug_color = vec3(1.0);
}

// Bicubic interpolation implementation taken from http://www.java-gaming.org/index.php?topic=35123.0
vec4 cubic(float v){
    vec4 n = vec4(1.0, 2.0, 3.0, 4.0) - v;
    vec4 s = n * n * n;
    float x = s.x;
    float y = s.y - 4.0 * s.x;
    float z = s.z - 4.0 * s.y + 6.0 * s.x;
    float w = 6.0 - x - y - z;
    return vec4(x, y, z, w) * (1.0/6.0);
}

vec4 textureBicubic(sampler2D sampler, vec2 texCoords){

   vec2 texSize = vec2(textureSize(sampler, 0));
   vec2 invTexSize = 1.0 / texSize;
   
   texCoords = texCoords * texSize - 0.5;

   
    vec2 fxy = fract(texCoords);
    texCoords -= fxy;

    vec4 xcubic = cubic(fxy.x);
    vec4 ycubic = cubic(fxy.y);

    vec4 c = texCoords.xxyy + vec2 (-0.5, +1.5).xyxy;
    
    vec4 s = vec4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
    vec4 offset = c + vec4 (xcubic.yw, ycubic.yw) / s;
    
    offset *= invTexSize.xxyy;
    
    vec4 sample0 = texture(sampler, offset.xz);
    vec4 sample1 = texture(sampler, offset.yz);
    vec4 sample2 = texture(sampler, offset.xw);
    vec4 sample3 = texture(sampler, offset.yw);

    float sx = s.x / (s.x + s.y);
    float sy = s.z / (s.z + s.w);

    return mix(
       mix(sample3, sample2, sx), mix(sample1, sample0, sx)
    , sy);
}

void fragment() {
	vec2 detail_uv = disp_uv * 10.0;
	float eps_h = 0.001;
	float eps_d = 0.0005;
	
	vec2 rdisp_uv = mod(disp_uv, vec2(1.0)) + vec2(1.0);
	rdisp_uv = mod(rdisp_uv, vec2(1.0));
	
	ivec2 high_vec = ivec2(1000000 * disp_row);
	ivec2 chunk = (ivec2(floor(disp_uv)) + high_vec + disp_row / 2) % disp_row;
	int tidx = chunk.y * disp_row + chunk.x;
	vec4 height_val = textureBicubic(height_map, terrain_uv);
	//vec4 height_val = texture(height_map, terrain_uv);
	
	float snow_height = height_val.g + height_val.b;
	
	float v_distance = length(VERTEX);
	vec2 duvx = vec2(eps_d, 0);
	vec2 duvy = vec2(0, eps_d);
	vec3 height_px = vec3(VERTEX.x + eps_h * HEIGHTMAP_RANGE, texture(height_map, terrain_uv + vec2(eps_h, 0)).x, VERTEX.z);
	vec3 height_mx = vec3(VERTEX.x - eps_h * HEIGHTMAP_RANGE, texture(height_map, terrain_uv - vec2(eps_h, 0)).x, VERTEX.z);
	vec3 height_py = vec3(VERTEX.x, texture(height_map, terrain_uv + vec2(0, eps_h)).x, VERTEX.z + eps_h * HEIGHTMAP_RANGE);
	vec3 height_my = vec3(VERTEX.x, texture(height_map, terrain_uv - vec2(0, eps_h)).x, VERTEX.z - eps_h * HEIGHTMAP_RANGE);
	vec3 normal_height = -normalize(cross(height_px - height_mx, height_py - height_my));
	
	displacement = 0.0;
	vec3 normal_disp = vec3(0.0);
	if (used_maps[tidx] && v_distance <= far_fadeout_distance) {
		displacement = texture(displacement_maps[tidx], rdisp_uv).r;
		vec3 disp_px = vec3(VERTEX.x + eps_h * displacement_map_range, VERTEX.y - snow_height * texture(displacement_maps[tidx], rdisp_uv + duvx).r + DETAIL_SIZE * texture(glitter_map, detail_uv + duvx).r, VERTEX.z);
		vec3 disp_mx = vec3(VERTEX.x - eps_h * displacement_map_range, VERTEX.y - snow_height * texture(displacement_maps[tidx], rdisp_uv - duvx).r + DETAIL_SIZE * texture(glitter_map, detail_uv - duvx).r, VERTEX.z);
		vec3 disp_py = vec3(VERTEX.x, VERTEX.y - snow_height * texture(displacement_maps[tidx], rdisp_uv + duvy).r + DETAIL_SIZE * texture(glitter_map, detail_uv + duvy).r, VERTEX.z + eps_h * displacement_map_range);
		vec3 disp_my = vec3(VERTEX.x, VERTEX.y - snow_height * texture(displacement_maps[tidx], rdisp_uv - duvy).r + DETAIL_SIZE * texture(glitter_map, detail_uv - duvy).r, VERTEX.z - eps_h * displacement_map_range);
		normal_disp = -normalize(cross(disp_px - disp_mx, disp_py - disp_my));
	}
	else {
		vec3 disp_px = vec3(VERTEX.x + eps_h * displacement_map_range, VERTEX.y + DETAIL_SIZE * texture(glitter_map, detail_uv + duvx).r, VERTEX.z);
		vec3 disp_mx = vec3(VERTEX.x - eps_h * displacement_map_range, VERTEX.y + DETAIL_SIZE * texture(glitter_map, detail_uv - duvx).r, VERTEX.z);
		vec3 disp_py = vec3(VERTEX.x, VERTEX.y + DETAIL_SIZE * texture(glitter_map, detail_uv + duvy).r, VERTEX.z + eps_h * displacement_map_range);
		vec3 disp_my = vec3(VERTEX.x, VERTEX.y + DETAIL_SIZE * texture(glitter_map, detail_uv - duvy).r, VERTEX.z - eps_h * displacement_map_range);
		normal_disp = -normalize(cross(disp_px - disp_mx, disp_py - disp_my));
	}
	
	disp_flatness = dot(normal_disp, vec3(0.0, 1.0, 0.0));
	float dot_diff = disp_flatness - dot(normal_height, vec3(0.0, 1.0, 0.0));
	
	if (dot_diff > 0.05) {
		NORMAL = normal_height;
	}
	else if (dot_diff < -0.1) {
		NORMAL = normal_disp;
	}
	else {
		float t = (dot_diff + 0.1) / 0.15;
		NORMAL = mix(normal_disp, normal_height, t);
	}
	
	float fade_t = clamp((v_distance - near_fadeout_distance) / (far_fadeout_distance - near_fadeout_distance), 0.0, 1.0);
	NORMAL = mix(NORMAL, normal_height, fade_t);
	
	vec3 v = normalize(-VERTEX);
	float jitter = texture(glitter_map, 4.0 * detail_uv).r;
	NORMAL = mix(NORMAL, v, JITTER_STRENGTH * pow(jitter, 8.0));
	NORMAL = (VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	
	float snow_var = -0.6 * texture(glitter_map, detail_uv).r;
	snow_height -= 0.8 * displacement * snow_height;
	snowiness = clamp(snow_height / 0.02 + snow_var, 0.0, 1.0);
	float grassiness = 1.0 - clamp((height - rock_fade_start) / (rock_fade_end - rock_fade_start), 0.0, 1.0);
	grassiness *= min(1.0, 1.2 * pow(dot(vec3(0.0, 1.0, 0.0), normal_height), 16.0));
	vec3 grass_color = texture(grass_texture, detail_uv).rgb;
	vec3 rock_color = texture(rock_texture, detail_uv).rgb;
	vec3 ground_color = mix(rock_color, grass_color, grassiness);
	float detail = 0.9 + 0.1 * texture(glitter_map, 40.0 * disp_uv).r;
	float albedo_l = 0.92;
	albedo_l += 0.07 * texture(texture_roughness, 4.0 * disp_uv).r;
	ALBEDO = mix(ground_color, vec3(albedo_l), snowiness);
	
	vec4 roughness_texture_channel = vec4(1.0, 0.0, 0.0, 0.0);
	float roughness_snow = 1.0 - (dot(texture(texture_roughness, rdisp_uv), roughness_texture_channel) + 3.0) / 4.0;
	ROUGHNESS = mix(0.9, roughness_snow, snowiness);
	
	float fresnel = 1.0 - dot(VIEW, NORMAL);
	float fresnel2 = fresnel * fresnel;
	float fresnel3 = fresnel * fresnel2;
	float fresnel4 = fresnel2 * fresnel2;
	float glitter = pow(texture(glitter_map, 30.0 * disp_uv).r, 10.0);
	float rim_specular = glitter * fresnel2 * fresnel3 / 3.0 + fresnel4 * fresnel4 / 4.0;
	SPECULAR = 0.5 * (1.0 - ROUGHNESS) * rim_specular;
}

//void light() {
	//float fresnel = 1.0 - dot(VIEW, NORMAL);
	//float fresnel2 = fresnel * fresnel;
	//float fresnel3 = fresnel * fresnel2;
	//float fresnel4 = fresnel2 * fresnel2;
	//float glitter = pow(texture(glitter_map, 30.0 * disp_uv).r, 10.0);
	//float rim_specular = glitter * fresnel2 * fresnel3 / 3.0 + fresnel4 * fresnel4 / 4.0;
	//float disp_ao = (1.0 - displacement_darkening * displacement);
	//SPECULAR_LIGHT = 0.5 * (1.0 - ROUGHNESS) * vec3(rim_specular);
	//vec3 snow_diffuse = 0.4 * ALBEDO * dot(LIGHT, NORMAL) * disp_flatness * disp_ao;
	//vec3 ground_diffuse = ALBEDO * dot(LIGHT, NORMAL);
	//DIFFUSE_LIGHT = mix(ground_diffuse, snow_diffuse, snowiness);
//}