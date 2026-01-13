shader_type spatial;
render_mode unshaded;

uniform sampler2D depth_texture : source_color, hint_depth_texture;
uniform sampler2D last_texture : source_color, hint_default_black;
uniform sampler2D height_map : source_color, filter_linear_mipmap;

uniform vec2 terrain_uv_offset = vec2(0.0);
uniform float snow_height = 1.0;
uniform float patch_offset = 0.0f;
uniform float patch_range = 100.0;
uniform float max_terrain_height = 5.0;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float raw_depth = texture(depth_texture, SCREEN_UV).x;
	float altitude = patch_offset + (1.0 - raw_depth) * patch_range;
	vec2 terrain_uv = SCREEN_UV - vec2(0.5);
	terrain_uv = vec2(terrain_uv.x, -terrain_uv.y);
	terrain_uv = terrain_uv / 12.0 - vec2(0.5) + terrain_uv_offset;
	float terrain_height = texture(height_map, terrain_uv).r * max_terrain_height;
	float displacement = (terrain_height - altitude) / snow_height;

	//ALBEDO = vec3(terrain_height);
	vec3 last_rgb = texture(last_texture, SCREEN_UV).rgb;
	float last_displacement = last_rgb.r;
	//ALBEDO = vec3(terrain_height / max_terrain_height, altitude / max_terrain_height, 0.0);
	if (last_displacement < displacement) {
		//ALBEDO = vec3(displacement, texture(height_map, terrain_uv).r, 0);
		ALBEDO = vec3(displacement, 0, 0);
	}
	else {
		//ALBEDO = vec3(last_rgb.r, texture(height_map, terrain_uv).r, 0);
		ALBEDO = vec3(last_rgb.r, 0, 0);
	}
	//ALBEDO = vec3(altitude / 32.0);
}

void light() {
	DIFFUSE_LIGHT = ALBEDO;
}
