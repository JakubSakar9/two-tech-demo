@tool
extends CompositorEffect
class_name TerrainRenderer

var TerrainRendererBackend = load("res://entities/terrain_renderer/TerrainRendererBackend.cs")
var renderer_backend = TerrainRendererBackend.new()
	

func _init():
	effect_callback_type = CompositorEffect.EFFECT_CALLBACK_TYPE_PRE_OPAQUE
	renderer_backend = TerrainRendererBackend.new()
	

func _notification(what: int) -> void:
	match what:
		NOTIFICATION_PREDELETE:
			renderer_backend.Cleanup()
			

func _render_callback(callback_type: int, render_data: RenderData) -> void:
	if callback_type == EFFECT_CALLBACK_TYPE_PRE_OPAQUE:
		var rsb: RenderSceneBuffersRD = render_data.get_render_scene_buffers()
		var rsd: RenderSceneDataRD = render_data.get_render_scene_data()
		if rsb:
			var size = rsb.get_internal_size()
			if size.x == 0 and size.y == 0:
				return
			if !renderer_backend.Initialized():
				renderer_backend.InitRendering(rsb, rsd)
			else:
				renderer_backend.CreateFramebuffers(rsb)
			renderer_backend.Draw(rsd)
