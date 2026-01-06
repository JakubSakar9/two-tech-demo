using Godot;
using System;

public partial class DebugTexture : GridContainer
{
    [Export] Terrain TerrainRef;
    [Export] TerrainDeformer Deformer;
    [Export] int CellTextureSize = 256;
    
    private TextureRect[] _texRects;

    public override void _Ready()
    {
        base._Ready();
        _texRects = new TextureRect[9];
        for (int i = 0; i < 9; i++)
        {
            _texRects[i] = new TextureRect();
            _texRects[i].ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
            _texRects[i].CustomMinimumSize = new Vector2(CellTextureSize, CellTextureSize);
            AddChild(_texRects[i]);
        }
    }

    public override void _Draw()
    {
        base._Draw();
        // Texture = TerrainRef.GetNormalTexture();
        for (int i = 0; i < 9; i++)
        {
            _texRects[i].Texture = Deformer.DisplacementMaps[i];
        }
    }
}
