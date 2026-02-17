shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;

const float HEIGHTMAP_RANGE = 768.0;

uniform vec4 albedo;
uniform sampler2D height_map: source_color, filter_linear_mipmap;
uniform sampler2D glitter_map: source_color, filter_nearest;
//uniform sampler2D normal_map: hint_normal, source_color, filter_linear_mipmap;
uniform vec2 chunk_origin = vec2(0.0);
uniform float snow_height = 1.0f;
uniform float max_height = 5.0f;

varying vec2 terrain_uv;
varying vec3 debug_color;


void vertex() {
	vec4 transformedPosition = MODEL_MATRIX * vec4(VERTEX, 1.0);
	transformedPosition = transformedPosition / transformedPosition.w;
	vec2 unshifted_uv = vec2(transformedPosition.x, transformedPosition.z);
	vec2 shifted_uv = unshifted_uv - chunk_origin;
	terrain_uv = shifted_uv / HEIGHTMAP_RANGE - vec2(0.5);

	float height = texture(height_map, terrain_uv).r * max_height;
	float displacement = 0.0; // TODO: Add displacement to the terrain
	float snow_offset = (1.0 - displacement) * snow_height;
	VERTEX.y = height + snow_offset;

	float eps = 0.001;
	vec3 vert_px = vec3(VERTEX.x + eps * HEIGHTMAP_RANGE, texture(height_map, terrain_uv + vec2(eps, 0)).x * max_height, VERTEX.y);
	vec3 vert_mx = vec3(VERTEX.x - eps * HEIGHTMAP_RANGE, texture(height_map, terrain_uv - vec2(eps, 0)).x * max_height, VERTEX.y);
	vec3 vert_py = vec3(VERTEX.x, texture(height_map, terrain_uv + vec2(0, eps)).x * max_height, VERTEX.y + eps * HEIGHTMAP_RANGE);
	vec3 vert_my = vec3(VERTEX.x, texture(height_map, terrain_uv - vec2(0, eps)).x * max_height, VERTEX.y - eps * HEIGHTMAP_RANGE);
	vec3 normal = -normalize(cross(vert_px - vert_mx, vert_py - vert_my));
	NORMAL = normal;
	//vec3 normal = texture(normal_map, terrain_uv).xyz * 2.0 - 1.0; // Not working
    //NORMAL = normalize(vec3(normal.x, normal.y * max_height, normal.z));
	debug_color = vec3(1.0);
}

void fragment() {
	ALBEDO = vec3(1.0 * debug_color);
	EMISSION = vec3(0.1, 0.1, 0.1);
}

void light() {
	float fresnel = 1.0 - dot(VIEW, NORMAL);
	float fresnel2 = fresnel * fresnel;
	float fresnel3 = fresnel * fresnel2;
	float fresnel4 = fresnel2 * fresnel2;
	float glitter = clamp(texture(glitter_map, 100.0 * terrain_uv).r, 0.4, 1.0);
	vec3 H = normalize(LIGHT + VIEW);
	float rim_specular = glitter * fresnel2 * fresnel3 / 2.0 + fresnel4 * fresnel4 / 4.0;
	SPECULAR_LIGHT = vec3(rim_specular);
	DIFFUSE_LIGHT = 0.5 * ALBEDO * dot(LIGHT, NORMAL);
}
