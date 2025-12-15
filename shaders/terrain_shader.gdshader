shader_type spatial;
render_mode specular_schlick_ggx;

const float HEIGHTMAP_RANGE = 768.0;
const float CELL_SIZE = 256.0;

uniform vec4 albedo : source_color;
uniform sampler2D height_map: source_color;
uniform sampler2D normal_map: hint_normal;
//uniform ivec2 cell_coordinates = ivec2(0, 0);
uniform vec2 chunk_origin = vec2(0.0);
uniform float snow_height = 1.0f;
uniform float max_height = 5.0f;


void vertex() {
	vec4 transformedPosition = MODEL_MATRIX * vec4(VERTEX, 1.0);
	transformedPosition = transformedPosition / transformedPosition.w;
	//vec2 uv = vec2(transformedPosition.x, transformedPosition.z) / HEIGHTMAP_RANGE - vec2(0.5);
	UV = vec2(transformedPosition.x - chunk_origin.x, transformedPosition.z - chunk_origin.y) / HEIGHTMAP_RANGE - vec2(0.5);
	//float height = texture(height_map, uv).r * max_height;
	float height = texture(height_map, UV).r * max_height;
	VERTEX.y = height;
	NORMAL = 2.0 * texture(normal_map, UV).xzy - vec3(1.0);
	NORMAL.z *= -1.0;
}

void fragment() {
	ALBEDO = albedo.xyz;
}
