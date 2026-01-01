shader_type spatial;
render_mode unshaded;

uniform sampler2D depth_texture : source_color, hint_depth_texture;
uniform sampler2D last_texture : source_color, hint_default_black;
uniform sampler2D height_map : source_color, filter_linear_mipmap;

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
	float terrain_height = texture(height_map, SCREEN_UV / 6.0 + vec2(0.5 - 1.0/6.0)).r * max_terrain_height;
	float displacement = (snow_height + terrain_height - altitude) / snow_height;
	
	//ALBEDO = vec3(terrain_height);
	vec3 last_rgb = texture(last_texture, SCREEN_UV).rgb;
	float last_displacement = last_rgb.r;
	if (last_displacement < displacement) {
		ALBEDO = vec3(displacement, dFdxCoarse(displacement), dFdyCoarse(displacement));
	}
	else {
		ALBEDO = vec3(last_rgb.r, dFdxCoarse(last_rgb.x), dFdyCoarse(last_rgb.x));
	}
}

void light() {
	DIFFUSE_LIGHT = ALBEDO;
}
