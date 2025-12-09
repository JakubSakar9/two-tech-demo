shader_type spatial;
render_mode unshaded;

uniform sampler2D depth_texture : source_color, hint_depth_texture;
uniform sampler2D last_texture : source_color, hint_default_black;

uniform float feet_altitude = 0;
uniform float snow_height = 1.0;
uniform float patch_offset = 0.0f;
uniform float patch_range = 100.0;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float raw_depth = texture(depth_texture, SCREEN_UV).x;
	float altitude = patch_offset + (1.0 - raw_depth) * patch_range;
	float displacement = (snow_height + feet_altitude - altitude) / snow_height;
	
	vec3 last_rgb = texture(last_texture, SCREEN_UV).rgb;
	float last_displacement = last_rgb.r;
	if (last_displacement < displacement) {
		ALBEDO = vec3(displacement, dFdxCoarse(displacement), dFdyCoarse(displacement));
	}
	else {
		ALBEDO = vec3(last_rgb.r, dFdxCoarse(last_rgb.x), dFdyCoarse(last_rgb.y));
	}
}

void light() {
	DIFFUSE_LIGHT = ALBEDO;
}
