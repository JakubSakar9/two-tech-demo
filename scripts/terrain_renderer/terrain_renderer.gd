@tool
extends CompositorEffect
class_name TerrainRenderer

# var rd: RenderingDevice
# var pipeline: RID
# var shader: RID
# var vertex_position_buffer: RID
# var vertex_color_buffer: RID
# var vertex_array: RID
# var clear_colors: Array[Color]
# var image_texture: RID
# var depth_texture: RID
# var screen_buffer: RID
# var vertex_format: int
var TerrainRendererBackend = load("res://scripts/terrain_renderer/TerrainRendererBackend.cs")
var renderer_backend

const VERTEX_POSITION_SIZE: int = 2 * 4
const VERTEX_COLOR_SIZE: int = 3 * 4
	

func _init():
	effect_callback_type = EFFECT_CALLBACK_TYPE_POST_TRANSPARENT
	renderer_backend = TerrainRendererBackend.new()
	

func _notification(what: int) -> void:
	match what:
		NOTIFICATION_PREDELETE:
			renderer_backend.Cleanup()
			

func _render_callback(callback_type: int, render_data: RenderData) -> void:
	if callback_type == EFFECT_CALLBACK_TYPE_POST_TRANSPARENT:
		var rsb : RenderSceneBuffersRD = render_data.get_render_scene_buffers()
		if rsb:
			var size = rsb.get_internal_size()
			if size.x == 0 and size.y == 0:
				return
			if !renderer_backend.Initialized():
				# _initialize_rendering(rsb)
				renderer_backend.InitRendering(rsb)
			else:
				# _create_framebuffers(rsb)
				renderer_backend.CreateFramebuffers(rsb)

			# _draw_terrain()
			renderer_backend.Draw()
