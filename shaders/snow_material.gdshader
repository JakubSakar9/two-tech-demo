shader_type spatial;
render_mode diffuse_lambert;

uniform sampler2D height_map: hint_default_black;
uniform sampler2D displacement_tex: hint_default_black;
uniform float snow_height = 1.0f;
uniform float patch_height = 1.0f;

void vertex() {
	vec2 uv = vec2(UV.x, -UV.y);
	float disp = texture(displacement_tex, uv).r * snow_height;
	float height = texture(height_map, UV).r * patch_height;
	VERTEX.y = height - disp;

	vec2 eps = vec2(0.02, 0.0);
	float disp_mx = texture(displacement_tex, uv - eps * 0.01).r * snow_height;
	float disp_px = texture(displacement_tex, uv + eps * 0.01).r * snow_height;
	float disp_mz = texture(displacement_tex, vec2(uv.x - eps.y, uv.x + eps.x) * 0.01).r * snow_height;
	float disp_pz = texture(displacement_tex, vec2(uv.x + eps.y, uv.x - eps.x) * 0.01).r * snow_height;

	vec3 normal = normalize(vec3(disp_px - disp_mx, 2.0 * eps.x * snow_height, disp_pz - disp_mz));
    NORMAL = normal;
}

void fragment() {
	// Called for every pixel the material is visible on.
	vec2 uv = vec2(UV.x, -UV.y);
	ALBEDO = vec3(1.0, 1.0, 1.0);
	//ALBEDO = vec3(1.0 -  0.5 * texture(displacement_tex, uv).r);
}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
