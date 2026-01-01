shader_type spatial;
render_mode specular_schlick_ggx, diffuse_lambert;
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
	terrain_uv = vec2(transformedPosition.x - chunk_origin.x, transformedPosition.z - chunk_origin.y) / float(HEIGHTMAP_RANGE) - vec2(0.5);
	disp_uv = vec2(transformedPosition.x - chunk_origin.x, transformedPosition.z - chunk_origin.y) / float(CELL_SIZE / 4.0) - vec2(-0.5, 0.5);
	disp_uv = vec2(disp_uv.x, -disp_uv.y);
	float height = texture(height_map, terrain_uv).r * max_height;
	float snow_offset = (1.0 - texture(displacement_tex, disp_uv).r) * snow_height;
	VERTEX.y = height + snow_offset;
}

void fragment() {
	//ALBEDO = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	NORMAL_MAP = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	float displacement = texture(displacement_tex, disp_uv).r;
	ALBEDO = vec3(1.0) - vec3(0.5) * displacement;
	//ALBEDO = vec3(disp_uv.x, disp_uv.y, 0.5);
}
