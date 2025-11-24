shader_type spatial;

uniform sampler2D depth_texture : hint_depth_texture;

void vertex() {
	// Called for every vertex the material is visible on.
}

void fragment() {
	float depth = texture(depth_texture, SCREEN_UV).x;
	float depthR = float(int(255.0 * depth)) / 255.0;
	float depthG = float(int(65535.0 * (depth - depthR))) / 255.0;
	float depthB = float(int(16777215.0 * (depth - depthG)) % 65536) / 255.0;
	ALBEDO.rgb = vec3(depthR, depthG, depthB);
}

void light() {
	DIFFUSE_LIGHT = ALBEDO;
}
