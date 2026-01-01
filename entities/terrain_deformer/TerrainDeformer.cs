using Godot;
using System;
using System.Collections.Generic;

public partial class TerrainDeformer : Node3D
{
    const float DEFORMER_RADIUS = 0.4f;

    [Export] public Player PlayerRef;
    [Export] public Terrain TerrainRef;
    [Export] public float PatchSize = 20.0f;
    [Export] public float SnowHeight = 0.3f;

    private Path3D _trailPath;
    private CsgPolygon3D _deformerMesh;
    private MeshInstance3D _depthFilter;
    private SubViewport _subViewport;
    private Camera3D _deformCamera;
    private Vector3 _lastPosition = Vector3.Zero;
    private Queue<Vector3> _recentPositions;

    public override void _Ready()
    {
        base._Ready();
        // Node init
        _deformerMesh = GetNode<CsgPolygon3D>("%DeformerMesh");
        _depthFilter = GetNode<MeshInstance3D>("%DepthFilter");
        _subViewport = GetNode<SubViewport>("%SubViewport");
        _deformCamera = GetNode<Camera3D>("%DeformCamera");
        _trailPath = GetNode<Path3D>("%TrailPath");
        _trailPath.Curve.ClearPoints();

        _recentPositions = new Queue<Vector3>();
        
        _deformCamera.Size = TerrainRef.ChunkSizeUnits / 4;
        _deformCamera.GlobalPosition = Vector3.Zero + 2.0f * _deformCamera.Near * Vector3.Down;

        (_depthFilter.Mesh as PlaneMesh).Size = Vector2.One * TerrainRef.ChunkSizeUnits / 4;

        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("snow_height", SnowHeight);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("patch_offset", -_deformCamera.Near);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("patch_range", _deformCamera.Far - _deformCamera.Near);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", TerrainRef.GetHeightMap());
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("max_terrain_height", TerrainRef.MaxHeight);

    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        Vector3 playerPos = PlayerRef.GlobalPosition + Vector3.Up * 0.4f;
        if (playerPos != _lastPosition)
        {
            _lastPosition = playerPos;
            _recentPositions.Enqueue(_lastPosition);
            if (_recentPositions.Count > 5)
            {
                _recentPositions.Dequeue();
                _trailPath.Curve.RemovePoint(0);
            }
            _trailPath.Curve.AddPoint(_lastPosition);
        }
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("last_texture", _subViewport.GetTexture());
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("feet_altitude", PlayerRef.GlobalPosition.Y);
        TerrainRef.UpdateDisplacement(_subViewport.GetTexture());
    }

    public ViewportTexture GetTexture()
    {
        return _subViewport.GetTexture();
    }
}
