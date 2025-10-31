using Godot;
using Godot.Collections;
using System;

public partial class DeformableGeometryProcessor : Node
{
    public Array<Mesh> Meshes = new Array<Mesh>();
    public Array<Transform3D> GlobalTransforms = new Array<Transform3D>();
    public bool Synced = true;

    public static DeformableGeometryProcessor Instance { get; private set; }

    public override void _Ready()
    {
        Instance = this;
    }

    public void FetchGeometry()
    {
        foreach (var dInstance in GetTree().GetNodesInGroup("deformable"))
        {
            Meshes.Add((dInstance as MeshInstance3D).Mesh);
            GlobalTransforms.Add((dInstance as MeshInstance3D).GlobalTransform);
        }
        Synced = false;
    }

}