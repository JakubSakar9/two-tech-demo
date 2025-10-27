@tool
extends CompositorEffect
class_name TerrainRenderer

var rd: RenderingDevice
var pipeline: RID
var shader: RID
var vertex_position_buffer: RID
var vertex_color_buffer: RID
var vertex_array: RID
var clear_colors: Array[Color]
var image_texture: RID
var depth_texture: RID
var screen_buffer: RID
var vertex_format: int

const VERTEX_POSITION_SIZE: int = 2 * 4
const VERTEX_COLOR_SIZE: int = 3 * 4


func _initialize_rendering(renderscene_buffers: RenderSceneBuffersRD):
	rd = RenderingServer.get_rendering_device()
	
	_compile_shader()
	_create_vertex_format()
	_create_vertex_array()
	
	var rasterization_state = _create_rasterization_state()
	
	# Initializes a new multisample state for the rendering pipeline.
	var multisample_state = RDPipelineMultisampleState.new()
	# Multisampling is disabled
	multisample_state.enable_sample_shading = false
	multisample_state.sample_count = RenderingDevice.TEXTURE_SAMPLES_1
	multisample_state.min_sample_shading = 1.0;
	
	# Initializes a depth-stencil state for the rendering pipeline.
	# The depth test is disabled.
	var stencil_state = RDPipelineDepthStencilState.new()
	stencil_state.enable_depth_test = false
	
	var color_blend_state = _create_color_blend_state()

	# Initialize the frame buffer for rendering
	image_texture = renderscene_buffers.get_color_texture()
	depth_texture = renderscene_buffers.get_depth_texture()
	screen_buffer = rd.framebuffer_create([image_texture, depth_texture])

	# Get the framebuffer format
	var fb_format = rd.framebuffer_get_format(screen_buffer)

	pipeline = rd.render_pipeline_create(
		shader, fb_format, vertex_format, rd.RENDER_PRIMITIVE_TRIANGLES,
		rasterization_state, multisample_state, stencil_state, color_blend_state, 
		0, 0, []
	)
	clear_colors = [Color(0.2, 0.2, 0.2, 1.0)]
	

func _init():
	effect_callback_type = EFFECT_CALLBACK_TYPE_POST_TRANSPARENT
	

func _notification(what: int) -> void:
	match what:
		NOTIFICATION_PREDELETE:
			if !rd:
				return
			if shader.is_valid():
				rd.free_rid(shader)
			if vertex_array.is_valid():
				rd.free_rid(vertex_array)
			if vertex_position_buffer.is_valid():
				rd.free_rid(vertex_position_buffer)
			if vertex_color_buffer.is_valid():
				rd.free_rid(vertex_color_buffer)
			if rd.framebuffer_is_valid(screen_buffer):
				rd.free_rid(screen_buffer)
			

func _render_callback(callback_type: int, render_data: RenderData) -> void:
	if callback_type == EFFECT_CALLBACK_TYPE_POST_TRANSPARENT:
		var rsb : RenderSceneBuffersRD = render_data.get_render_scene_buffers()

		if rsb:
			var size = rsb.get_internal_size()
			if size.x == 0 and size.y == 0:
				return
			if !rd:
				_initialize_rendering(rsb)
			else:
				_create_framebuffers(rsb)

			rd.draw_command_begin_label("Draw a triangle", Color(1.0, 1.0, 1.0, 1.0))

			var draw_list = rd.draw_list_begin(screen_buffer, RenderingDevice.DRAW_CLEAR_COLOR_0, clear_colors)
			rd.draw_list_bind_render_pipeline(draw_list, pipeline)
			rd.draw_list_bind_vertex_array(draw_list, vertex_array)
			rd.draw_list_draw(draw_list, false, 1, 0)
			rd.draw_list_end()

			rd.draw_command_end_label()


func _compile_shader() -> void:
	var vs_file: FileAccess = FileAccess.open("res://shaders/d_terrain.vert", FileAccess.READ)
	var fs_file: FileAccess = FileAccess.open("res://shaders/d_terrain.frag", FileAccess.READ)
	var shader_source: RDShaderSource = RDShaderSource.new()
	shader_source.language = RenderingDevice.SHADER_LANGUAGE_GLSL
	shader_source.source_vertex = vs_file.get_as_text()
	shader_source.source_fragment = fs_file.get_as_text()
	var shader_spirv: RDShaderSPIRV = rd.shader_compile_spirv_from_source(shader_source)
	shader = rd.shader_create_from_spirv(shader_spirv)
	

func _create_vertex_format() -> void:
	var vertex_atr = RDVertexAttribute.new()
	vertex_atr.format = RenderingDevice.DATA_FORMAT_R32G32_SFLOAT
	vertex_atr.frequency = RenderingDevice.VERTEX_FREQUENCY_VERTEX
	vertex_atr.location = 0
	vertex_atr.offset = 0
	vertex_atr.stride = VERTEX_POSITION_SIZE
	
	var color_atr = RDVertexAttribute.new()
	color_atr.format = RenderingDevice.DATA_FORMAT_R32G32B32_SFLOAT
	color_atr.frequency = RenderingDevice.VERTEX_FREQUENCY_VERTEX
	color_atr.location = 1
	color_atr.offset = 0
	color_atr.stride = VERTEX_COLOR_SIZE
	
	vertex_format = rd.vertex_format_create([vertex_atr, color_atr])
	
	
func _create_vertex_array() -> void:
	var vertices_position_packed = PackedFloat32Array([
		0.0, -0.5,
		0.5, 0.5,
		-0.5, 0.5,
	]).to_byte_array()
	vertex_position_buffer = rd.vertex_buffer_create(vertices_position_packed.size(), vertices_position_packed, false)
	
	var vertices_color_packed = PackedFloat32Array([
		1.0, 0.0, 0.0,
		0.0, 1.0, 0.0,
		0.0, 0.0, 1.0,
	]).to_byte_array()
	var vertices_color_buffer = rd.vertex_buffer_create(vertices_color_packed.size(), vertices_color_packed)
	
	vertex_array = rd.vertex_array_create(3, vertex_format, [vertex_position_buffer, vertices_color_buffer])


func _create_rasterization_state() -> RDPipelineRasterizationState:
	var rasterization_state = RDPipelineRasterizationState.new()
	rasterization_state.wireframe = false
	rasterization_state.cull_mode = RenderingDevice.POLYGON_CULL_DISABLED
	rasterization_state.enable_depth_clamp = false
	rasterization_state.line_width = 1.0;
	rasterization_state.front_face = RenderingDevice.POLYGON_FRONT_FACE_CLOCKWISE
	rasterization_state.depth_bias_enabled = false
	return rasterization_state
	
	
func _create_color_blend_state() -> RDPipelineColorBlendState:
	var color_blend_state = RDPipelineColorBlendState.new()
	var color_attachment = RDPipelineColorBlendStateAttachment.new()
	color_attachment.enable_blend = true
	color_attachment.write_a = true
	color_attachment.write_b = true
	color_attachment.write_g = true
	color_attachment.write_r = true
	color_attachment.alpha_blend_op = RenderingDevice.BLEND_OP_ADD
	color_attachment.color_blend_op = RenderingDevice.BLEND_OP_ADD
	color_attachment.src_color_blend_factor = RenderingDevice.BLEND_FACTOR_ONE
	color_attachment.dst_color_blend_factor = RenderingDevice.BLEND_FACTOR_ZERO
	color_attachment.src_alpha_blend_factor = RenderingDevice.BLEND_FACTOR_ONE
	color_attachment.dst_alpha_blend_factor = RenderingDevice.BLEND_FACTOR_ZERO
	color_blend_state.attachments.push_back(color_attachment)
	color_blend_state.enable_logic_op = false
	color_blend_state.logic_op = RenderingDevice.LOGIC_OP_COPY
	return color_blend_state
	
	
func _create_framebuffers(rsb: RenderSceneBuffers) -> void:
	var new_image_texture = rsb.get_color_texture()
	var new_depth_texture = rsb.get_depth_texture()
	if new_image_texture != image_texture or new_depth_texture != depth_texture:
		image_texture = new_image_texture
		depth_texture = new_depth_texture
		if rd.framebuffer_is_valid(screen_buffer):
			rd.free_rid(screen_buffer)
		screen_buffer = rd.framebuffer_create([image_texture, depth_texture])
