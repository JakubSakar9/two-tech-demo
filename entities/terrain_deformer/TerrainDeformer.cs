using Godot;
using System;
using System.Collections.Generic;

public partial class TerrainDeformer : Node3D
{
    const float DEFORMER_RADIUS = 0.4f;
    const float CELL_SIZE = 64.0f;

    [Export] public Player PlayerRef;
    [Export] public Terrain TerrainRef;
    [Export] public float PatchSize = 20.0f;
    [Export] public float SnowHeight = 0.3f;

    public Texture2D[] DisplacementMaps;

    private Path3D _trailPath;
    private CsgPolygon3D _deformerMesh;
    private MeshInstance3D _depthFilter;
    private SubViewport _subViewport;
    private Camera3D _deformCamera;
    private Vector3 _lastPosition = Vector3.Zero;
    private Queue<Vector3> _recentPositions;
    private Vector2I _currentCell;
    private Vector2 _currentCellCenter;
    private int _currentCellIdx = 4;

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
        _currentCell = Vector2I.Zero;
        
        _deformCamera.Size = CELL_SIZE;
        _deformCamera.GlobalPosition = Vector3.Zero + 2.0f * _deformCamera.Near * Vector3.Down;

        DisplacementMaps = new Texture2D[9];
        for (int i = 0; i < 9; i++)
        {
            DisplacementMaps[i] = new ImageTexture();
            (DisplacementMaps[i] as ImageTexture).SetImage(_subViewport.GetTexture().GetImage());
        }
        DisplacementMaps[4] = _subViewport.GetTexture();

        (_depthFilter.Mesh as PlaneMesh).Size = Vector2.One * CELL_SIZE;

        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("snow_height", SnowHeight);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("patch_offset", -_deformCamera.Near);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("patch_range", _deformCamera.Far - _deformCamera.Near);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", TerrainRef.GetHeightMap());
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("max_terrain_height", TerrainRef.MaxHeight);

        _currentCellIdx = 3 * ((_currentCell.X + 1) % 3) + (_currentCell.Y + 1) % 3;
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
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("last_texture", DisplacementMaps[_currentCellIdx]);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("feet_altitude", PlayerRef.GlobalPosition.Y);
        // DisplacementMaps[mapIdx] = _subViewport.GetTexture();
        TerrainRef.UpdateDisplacement(in DisplacementMaps);
    }

    public Texture2D GetTexture()
    {
        int mapIdx = 3 * ((_currentCell.X + 300001) % 3) + (_currentCell.Y + 300001) % 3;
        return DisplacementMaps[mapIdx];
    }

    private void CheckCellChange()
    {
        float xDiff = PlayerRef.GlobalPosition.X - _currentCellCenter.X;
        float yDiff = PlayerRef.GlobalPosition.Z - _currentCellCenter.Y;
        if (xDiff > CELL_SIZE / 2)
        {
            _currentCellCenter.X += CELL_SIZE;
            _currentCell.X += 1;
        }
        else if (xDiff < -CELL_SIZE / 2)
        {
            _currentCellCenter.X -= CELL_SIZE;
            _currentCell.X -= 1;
        }
        if (yDiff > CELL_SIZE / 2)
        {
            _currentCellCenter.Y += CELL_SIZE;
            _currentCell.Y += 1;
        }
        else if (yDiff < -CELL_SIZE / 2)
        {
            _currentCellCenter.Y -= CELL_SIZE;
            _currentCell.Y -= 1;
        }

        _currentCellIdx = 3 * ((_currentCell.X + 1) % 3) + (_currentCell.Y + 1) % 3;
        CallDeferred(MethodName._SwapTargetTexture);
    }

    private void _SwapTargetTexture()
    {
        Image lastImage = DisplacementMaps[_currentCellIdx].GetImage();
        DisplacementMaps[_currentCellIdx] = new ImageTexture();
        (DisplacementMaps[_currentCellIdx] as ImageTexture).SetImage(lastImage);
    }
}
