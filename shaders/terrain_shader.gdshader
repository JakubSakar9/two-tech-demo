shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;
//render_mode unshaded;

const float HEIGHTMAP_RANGE = 768.0;
const float DETAIL_SIZE = 0.02;

uniform vec4 albedo;
uniform sampler2D height_map: source_color, filter_linear_mipmap;
uniform sampler2D glitter_map: source_color, filter_nearest;
uniform sampler2D displacement_map: source_color, filter_linear_mipmap ;
uniform vec2 chunk_origin = vec2(0.0);
uniform float snow_height = 1.0f;
uniform float max_height = 5.0f;
uniform float displacement_map_range = 64.0;

varying vec2 terrain_uv;
varying vec2 disp_uv;
varying vec3 debug_color;
varying float disp_flatness;
varying float displacement;
varying vec3 normal_height;

void vertex() {
	vec4 transformedPosition = MODEL_MATRIX * vec4(VERTEX, 1.0);
	transformedPosition = transformedPosition / transformedPosition.w;
	vec2 unshifted_uv = vec2(transformedPosition.x, transformedPosition.z);
	vec2 shifted_uv = unshifted_uv - chunk_origin;
	terrain_uv = shifted_uv / HEIGHTMAP_RANGE - vec2(0.5);
	
	disp_uv = unshifted_uv / displacement_map_range - vec2(0.5);

	float height = texture(height_map, terrain_uv).r * max_height;
	displacement = texture(displacement_map, disp_uv).r + DETAIL_SIZE * texture(glitter_map, disp_uv).r;
	float snow_offset = (1.0 - displacement) * snow_height;
	VERTEX.y = height + snow_offset;
	
	vec2 detail_uv = disp_uv * 10.0;
	
	 float eps_h = 0.001;
	 vec3 height_px = vec3(VERTEX.x + eps_h * HEIGHTMAP_RANGE, texture(height_map, terrain_uv + vec2(eps_h, 0)).x * max_height, VERTEX.z);
	 vec3 height_mx = vec3(VERTEX.x - eps_h * HEIGHTMAP_RANGE, texture(height_map, terrain_uv - vec2(eps_h, 0)).x * max_height, VERTEX.z);
	 vec3 height_py = vec3(VERTEX.x, texture(height_map, terrain_uv + vec2(0, eps_h)).x * max_height, VERTEX.z + eps_h * HEIGHTMAP_RANGE);
	 vec3 height_my = vec3(VERTEX.x, texture(height_map, terrain_uv - vec2(0, eps_h)).x * max_height, VERTEX.z - eps_h * HEIGHTMAP_RANGE);
	 normal_height = -normalize(cross(height_px - height_mx, height_py - height_my));
	
	//float eps_d = 0.0005;
	//
	 //vec2 duvx = vec2(eps_d, 0);
	 //vec2 duvy = vec2(0, eps_d);
	 //vec3 disp_px = vec3(VERTEX.x + eps_h * displacement_map_range, VERTEX.y - snow_height * texture(displacement_map, disp_uv + duvx).r + DETAIL_SIZE * texture(glitter_map, detail_uv + duvx).r, VERTEX.z);
	 //vec3 disp_mx = vec3(VERTEX.x - eps_h * displacement_map_range, VERTEX.y - snow_height * texture(displacement_map, disp_uv - duvx).r - DETAIL_SIZE * texture(glitter_map, detail_uv - duvx).r, VERTEX.z);
	 //vec3 disp_py = vec3(VERTEX.x, VERTEX.y - snow_height * texture(displacement_map, disp_uv + duvy).r + DETAIL_SIZE * texture(glitter_map, detail_uv + duvy).r, VERTEX.z + eps_h * displacement_map_range);
	 //vec3 disp_my = vec3(VERTEX.x, VERTEX.y - snow_height * texture(displacement_map, disp_uv - duvy).r - DETAIL_SIZE * texture(glitter_map, detail_uv - duvy).r, VERTEX.z - eps_h * displacement_map_range);
	 //vec3 normal_disp = -normalize(cross(disp_px - disp_mx, disp_py - disp_my));
	//
	 //disp_flatness = dot(normal_disp, vec3(0.0, 1.0, 0.0));
	//
	 //float dot_diff = disp_flatness - dot(normal_height, vec3(0.0, 1.0, 0.0));
	//
	 //if (dot_diff > 0.1) {
	 	//NORMAL = normal_disp;
	 //}
	 //else if (dot_diff < -0.1) {
	 	//NORMAL = normal_height;
	 //}
	 //else {
	 	//float t = (dot_diff + 0.1) / 0.2;
	 	//NORMAL = mix(normal_height, normal_disp, t);
	 //}
	//NORMAL = normal_height;
	
	//NORMAL = (VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	
	debug_color = vec3(1.0);
}

void fragment() {
	 vec2 detail_uv = disp_uv * 10.0;
	
	 float eps_h = 0.001;
	 //vec3 height_px = vec3(VERTEX.x + eps_h * HEIGHTMAP_RANGE, texture(height_map, terrain_uv + vec2(eps_h, 0)).x * max_height, VERTEX.z);
	 //vec3 height_mx = vec3(VERTEX.x - eps_h * HEIGHTMAP_RANGE, texture(height_map, terrain_uv - vec2(eps_h, 0)).x * max_height, VERTEX.z);
	 //vec3 height_py = vec3(VERTEX.x, texture(height_map, terrain_uv + vec2(0, eps_h)).x * max_height, VERTEX.z + eps_h * HEIGHTMAP_RANGE);
	 //vec3 height_my = vec3(VERTEX.x, texture(height_map, terrain_uv - vec2(0, eps_h)).x * max_height, VERTEX.z - eps_h * HEIGHTMAP_RANGE);
	 //vec3 normal_height = -normalize(cross(height_px - height_mx, height_py - height_my));
	
	float eps_d = 0.0005;
	
	 vec2 duvx = vec2(eps_d, 0);
	 vec2 duvy = vec2(0, eps_d);
	 vec3 disp_px = vec3(VERTEX.x + eps_h * displacement_map_range, VERTEX.y - snow_height * texture(displacement_map, disp_uv + duvx).r + DETAIL_SIZE * texture(glitter_map, detail_uv + duvx).r, VERTEX.z);
	 vec3 disp_mx = vec3(VERTEX.x - eps_h * displacement_map_range, VERTEX.y - snow_height * texture(displacement_map, disp_uv - duvx).r - DETAIL_SIZE * texture(glitter_map, detail_uv - duvx).r, VERTEX.z);
	 vec3 disp_py = vec3(VERTEX.x, VERTEX.y - snow_height * texture(displacement_map, disp_uv + duvy).r + DETAIL_SIZE * texture(glitter_map, detail_uv + duvy).r, VERTEX.z + eps_h * displacement_map_range);
	 vec3 disp_my = vec3(VERTEX.x, VERTEX.y - snow_height * texture(displacement_map, disp_uv - duvy).r - DETAIL_SIZE * texture(glitter_map, detail_uv - duvy).r, VERTEX.z - eps_h * displacement_map_range);
	 vec3 normal_disp = -normalize(cross(disp_px - disp_mx, disp_py - disp_my));
	
	 disp_flatness = dot(normal_disp, vec3(0.0, 1.0, 0.0));
	
	 float dot_diff = disp_flatness - dot(normal_height, vec3(0.0, 1.0, 0.0));
	
	 if (dot_diff > 0.1) {
	 	NORMAL = normal_disp;
	 }
	 else if (dot_diff < -0.1) {
	 	NORMAL = normal_height;
	 }
	 else {
	 	float t = (dot_diff + 0.1) / 0.2;
	 	NORMAL = mix(normal_height, normal_disp, t);
	 }
	//NORMAL = normal_height;
	
	NORMAL = (VIEW_MATRIX * vec4(NORMAL, 0.0)).xyz;
	
	ALBEDO = 0.8 * debug_color;
}

void light() {
	float fresnel = 1.0 - dot(VIEW, NORMAL);
	float fresnel2 = fresnel * fresnel;
	float fresnel3 = fresnel * fresnel2;
	float fresnel4 = fresnel2 * fresnel2;
	float glitter = clamp(texture(glitter_map, 100.0 * terrain_uv).r, 0.4, 1.0);
	vec3 H = normalize(LIGHT + VIEW);
	float rim_specular = glitter * fresnel2 * fresnel3 / 3.0 + fresnel4 * fresnel4 / 4.0;
	SPECULAR_LIGHT = vec3(rim_specular * disp_flatness);
	DIFFUSE_LIGHT = 0.5 * ALBEDO * dot(LIGHT, NORMAL) * disp_flatness * displacement;
}
