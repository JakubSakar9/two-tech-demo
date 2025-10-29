extends Node

var deformable_surface_arrays: Array[Mesh]
#var access_mutex: Mutex

func _ready():
	pass
	

func _process(delta: float) -> void:
	pass


func fetch_terrain_buffers():
	for t_instance in get_tree().get_nodes_in_group("deformable"):
		deformable_surface_arrays.push_back(t_instance.mesh)
