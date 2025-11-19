shader_type spatial;

uniform sampler2D depth_texture : hint_depth_texture;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float depth = texture(depth_texture, SCREEN_UV).x;
	float depthG = float(int(65535.0 * depth) % 256) / 255.0;
	float depthR = float(int(255.0 * depth)) / 255.0;
	ALBEDO.rgb = vec3(depthR, depthG, 1.0);
}

void light() {
	DIFFUSE_LIGHT = ALBEDO;
}
