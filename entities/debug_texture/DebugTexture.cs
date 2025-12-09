using Godot;
using System;

public partial class DebugTexture : TextureRect
{
    [Export] TerrainDeformer Deformer;

    public override void _Draw()
    {
        base._Draw();
        Texture = Deformer.GetTexture();
        Scale = Vector2.One * 1024 / Texture.GetSize().X;
    }
}
