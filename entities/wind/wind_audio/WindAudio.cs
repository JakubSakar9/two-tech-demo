using Godot;
using System;

public partial class WindAudio : Node3D
{
    private AudioStreamPlayer3D _directional;
    private AudioStreamPlayer _ambient;
    private Terrain _terrain;

    public override void _Ready()
    {
        base._Ready();
        _terrain = GetTree().GetFirstNodeInGroup("terrain") as Terrain;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        Vector3 windVec = _terrain.GetWindAtPoint(GetParent<Camera3D>().GlobalPosition);
        
    }
}
