using Godot;
using System;

public partial class TerrainDeformer : Node3D
{
    const float DEFORMER_RADIUS = 0.4f;

    [Export] public Player Player;

    private MeshInstance3D _deformerMesh;
    private SubViewport _subViewport;
    private Camera3D _deformCamera;

    public override void _Ready()
    {
        base._Ready();
        _deformerMesh = GetNode<MeshInstance3D>("%DeformerMesh");
        _subViewport = GetNode<SubViewport>("%SubViewport");
        _deformCamera = GetNode<Camera3D>("%DeformCamera");
        _deformCamera.GlobalPosition = Vector3.Zero + _deformCamera.Near * Vector3.Down;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        _deformerMesh.GlobalPosition = Player.GlobalPosition + (DEFORMER_RADIUS) * Vector3.Up;
    }

    public ViewportTexture GetTexture()
    {
        return _subViewport.GetTexture();
    }
}
