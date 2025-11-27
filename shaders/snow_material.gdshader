shader_type spatial;
render_mode diffuse_lambert;

uniform sampler2D displacement_tex: hint_default_black;
uniform float snow_height = 1.0f;

void vertex() {
	VERTEX.y -= texture(displacement_tex, UV).r * snow_height;
}

void fragment() {
	// Called for every pixel the material is visible on.

}

//void light() {
//	// Called for every pixel for every light affecting the material.
//	// Uncomment to replace the default light processing function with this one.
//}
