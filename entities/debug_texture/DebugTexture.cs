using Godot;
using System;

public partial class DebugTexture : TextureRect
{
    [Export] Terrain TerrainRef;

    public override void _Draw()
    {
        base._Draw();
        Texture = TerrainRef.GetNormalTexture();
        Scale = Vector2.One * 1024 / Texture.GetSize().X;
    }
}
