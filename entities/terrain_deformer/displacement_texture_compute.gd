class_name DisplacementTextureCompute
extends Object

const SHADER_PATH = "res://shaders/displacement_texture.glsl"

var _compute_shader: ComputeHelper
var _input_image_uniform: ImageUniform
var _last_image_uniform: ImageUniform
var _combined_image_uniform: SharedImageUniform

func init(input_image: Image, default_combined: Image) -> void:
	_compute_shader = ComputeHelper.create(SHADER_PATH)
	_input_image_uniform = ImageUniform.create(input_image)
	_last_image_uniform = ImageUniform.create(default_combined)
	_combined_image_uniform = SharedImageUniform.create(_last_image_uniform)
	_compute_shader.add_uniform_array([_input_image_uniform, _last_image_uniform, _combined_image_uniform])
	

func compute_texture(input_image: Image, dims: Vector2i) -> ImageTexture:
	_input_image_uniform.update_image(input_image)
	_last_image_uniform.update_image(_combined_image_uniform.get_image())
	_compute_shader.run(Vector3i(dims.x, dims.y, 1), [])
	ComputeHelper.sync()
	var converted_image: Image = _combined_image_uniform.get_image()
	converted_image.convert(Image.FORMAT_RGBA8)
	#converted_image.save_png("res://assets/textures/debug_output.png")
	return ImageTexture.create_from_image(converted_image)
