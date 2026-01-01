shader_type spatial;
render_mode specular_schlick_ggx, diffuse_lambert;
//render_mode unshaded;

const float HEIGHTMAP_RANGE = 768.0;
const float CELL_SIZE = 256.0;

uniform vec4 albedo;
uniform sampler2D height_map: source_color, filter_linear_mipmap;
uniform sampler2D normal_map: hint_normal, filter_linear_mipmap;
uniform ivec2 cell_coordinates = ivec2(0, 0);
uniform vec2 chunk_origin = vec2(0.0);
uniform float snow_height = 1.0f;
uniform float max_height = 5.0f;

varying vec3 world_normal;
varying vec2 terrain_uv;


void vertex() {
	vec4 transformedPosition = MODEL_MATRIX * vec4(VERTEX, 1.0);
	transformedPosition = transformedPosition / transformedPosition.w;
	terrain_uv =  vec2(transformedPosition.x - chunk_origin.x, transformedPosition.z - chunk_origin.y) / float(HEIGHTMAP_RANGE) - vec2(0.5);
	float height = texture(height_map, terrain_uv).r * max_height;
	VERTEX.y = height;
	//NORMAL = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	//NORMAL.z *= -1.0;
	//NORMAL.y *= 8.0 / max_height;
	//NORMAL = normalize(NORMAL);
}

void fragment() {
	//ALBEDO = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	NORMAL_MAP = 2.0 * texture(normal_map, terrain_uv).xzy - vec3(1.0);
	ALBEDO = albedo.xyz;
}
