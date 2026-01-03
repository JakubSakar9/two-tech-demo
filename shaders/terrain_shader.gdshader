shader_type spatial;
render_mode blend_mix, depth_draw_opaque, cull_back, diffuse_burley, specular_schlick_ggx;
//render_mode unshaded;

const float HEIGHTMAP_RANGE = 768.0;
const float CELL_SIZE = 256.0;

uniform vec4 albedo;
uniform sampler2D height_map: source_color, filter_linear_mipmap;
uniform sampler2D normal_map: hint_normal, filter_linear_mipmap;
uniform sampler2D displacement_tex: hint_default_black, repeat_disable;
uniform ivec2 cell_coordinates = ivec2(0, 0);
uniform vec2 chunk_origin = vec2(0.0);
uniform float snow_height = 1.0f;
uniform float max_height = 5.0f;

varying vec2 terrain_uv;
varying vec2 disp_uv;


void vertex() {
	vec4 transformedPosition = MODEL_MATRIX * vec4(VERTEX, 1.0);
	transformedPosition = transformedPosition / transformedPosition.w;
	vec2 shifted_uv = vec2(transformedPosition.x - chunk_origin.x, transformedPosition.z - chunk_origin.y);
	terrain_uv = shifted_uv / float(HEIGHTMAP_RANGE) - vec2(0.5);
	disp_uv = shifted_uv / float(CELL_SIZE / 4.0);
	disp_uv = vec2(disp_uv.x, -disp_uv.y) + vec2(0.5);
	float height = texture(height_map, terrain_uv).r * max_height;
	float snow_offset = (1.0 - texture(displacement_tex, disp_uv).r) * snow_height;
	VERTEX.y = height + snow_offset;

	vec2 eps = vec2(0.02, 0.0);
	float disp_mx = texture(displacement_tex, disp_uv - eps * 0.01).r * snow_height;
	float disp_px = texture(displacement_tex, disp_uv + eps * 0.01).r * snow_height;
	float disp_mz = texture(displacement_tex, vec2(disp_uv.x - eps.y, disp_uv.x + eps.x) * 0.01).r * snow_height;
	float disp_pz = texture(displacement_tex, vec2(disp_uv.x + eps.y, disp_uv.x - eps.x) * 0.01).r * snow_height;

	vec3 normal = normalize(vec3(disp_px - disp_mx, 2.0 * eps.x * snow_height, disp_pz - disp_mz));
    NORMAL = normal;
}

void fragment() {
	//ALBEDO = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	NORMAL_MAP = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	float displacement = texture(displacement_tex, disp_uv).r;

	ALBEDO = vec3(2.0); //- vec3(0.2) * displacement;
	//ALBEDO = vec3(disp_uv.x, disp_uv.y, 0.5);
}
