using Godot;
using System;
using System.Collections.Generic;

public partial class TerrainDeformer : Node3D
{
    public override void _Ready()
    {
        base._Ready();
        InitNodes();
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    private void InitNodes()
    {
    }
}