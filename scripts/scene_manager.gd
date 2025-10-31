extends Node3D

func _ready():
	DeformableGeometryProcessor.call_deferred("FetchGeometry")
