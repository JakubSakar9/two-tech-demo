using Godot;
using System;

public partial class TerrainGenerator : Node3D
{
    [Signal] public delegate void PatchChangedEventHandler();

    [Export] Texture2D HeightMap;

    private TerrainPatch _terrainPatch;

    public override void _Ready()
    {
        base._Ready();
        _terrainPatch = new TerrainPatch();
        AddChild(_terrainPatch);
        if (HeightMap != null)
        {
            _terrainPatch.GenerateFromHeightMap(HeightMap);
            EmitSignal(SignalName.PatchChanged);
        }
    }

    public TerrainPatch GetCurrentPatch()
    {
        return _terrainPatch;
    }
}
