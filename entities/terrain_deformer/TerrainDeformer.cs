using Godot;
using System;
using System.Collections.Generic;

public partial class TerrainDeformer : Node3D
{
    const float DEFORMER_RADIUS = 0.4f;
    const float CELL_SIZE = 64.0f;
    const int CELL_COUNT = 9;
    const int CELL_COUNT_ROW = 3;
    const int CELL_MOD_OFFSET = CELL_COUNT_ROW * 100000 + 1;

    [Signal] public delegate void CellChangedEventHandler();

    [Export] Shader DepthFilterShader;
    [Export] public Player PlayerRef;
    [Export] public Terrain TerrainRef;
    [Export] public float SnowHeight = 0.3f;
    [Export] public int Resolution = 2048;
    [Export] public float DepthCameraRange = 512.0f;
    [Export] public float DepthCameraNear = 0.5f;

    public Texture2D[] DisplacementMaps;

    private Path3D _trailPath;
    private CsgPolygon3D _deformerMesh;
    // private MeshInstance3D _depthFilter;
    private SubViewport[] _depthViewports;
    private Camera3D[] _depthCameras;
    private MeshInstance3D[] _depthFilters;
    private ShaderMaterial[] _depthMaterials;

    // private SubViewport _subViewport;
    // private Camera3D _deformCamera;
    private Vector3 _lastPosition = Vector3.Zero;
    private Queue<Vector3> _recentPositions;
    private Vector2I _currentCell;
    private Vector2 _currentCellCenter;
    private int _currentCellIdx = 4;
    private Vector2 _terrainUVOffset = Vector2.Zero;

    public override void _Ready()
    {
        base._Ready();
        _InitNodes();

        _currentCell = Vector2I.Zero;
        _UpdateCellIdx();

        _InitDepthViewports();
        _InitShaderParameters();

        _trailPath.Curve.ClearPoints();

        _recentPositions = new Queue<Vector3>();
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

    public void UpdateTerrainUVOffset()
    {
        
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        float xOffset = _currentCell.X / 12.0f;
        float yOffset = _currentCell.Y / 12.0f;
        Vector2 tuv = _terrainUVOffset + new Vector2(xOffset, yOffset);

        _depthMaterials[_currentCellIdx].SetShaderParameter("last_texture", DisplacementMaps[_currentCellIdx]);
        _depthMaterials[_currentCellIdx].SetShaderParameter("feet_altitude", PlayerRef.GlobalPosition.Y);
        _depthMaterials[_currentCellIdx].SetShaderParameter("terrain_uv_offset", _terrainUVOffset + tuv);
        TerrainRef.UpdateDisplacement(in DisplacementMaps);
        _CheckCellChange();
    }

    private void _InitNodes()
    {
        _deformerMesh = GetNode<CsgPolygon3D>("%DeformerMesh");
        _trailPath = GetNode<Path3D>("%TrailPath");
    }

    private void _InitDepthViewports()
    {
        PlaneMesh filterMesh = new PlaneMesh();
        filterMesh.Size = Vector2.One * CELL_SIZE;
        filterMesh.FlipFaces = true;
        
        DisplacementMaps = new Texture2D[CELL_COUNT];
        _depthViewports = new SubViewport[CELL_COUNT];
        _depthCameras = new Camera3D[CELL_COUNT];
        _depthMaterials = new ShaderMaterial[CELL_COUNT];
        _depthFilters = new MeshInstance3D[CELL_COUNT];
        for (int i = 0; i < CELL_COUNT; i++)
        {
            float xOffset = (i % 3 - 1) * CELL_SIZE;
            float yOffset = -(i / 3 - 1) * CELL_SIZE;

            _CreateDepthViewport(i);
            _CreateDepthCamera(i, xOffset, yOffset);
            
            _depthMaterials[i] = new ShaderMaterial();
            _depthMaterials[i].Shader = DepthFilterShader;
            _depthFilters[i] = new MeshInstance3D();
            _depthViewports[i].AddChild(_depthFilters[i]);
            _depthFilters[i].Mesh = filterMesh;
            _depthFilters[i].MaterialOverride = _depthMaterials[i];
            _depthFilters[i].GlobalPosition = new Vector3(xOffset, 0, yOffset);
            _depthFilters[i].SetLayerMaskValue(1, false);
            _depthFilters[i].SetLayerMaskValue(2, true);
            
            DisplacementMaps[i] = _depthViewports[i].GetTexture();
        }
    }

    private void _CreateDepthViewport(int idx)
    {
        SubViewport sv = new SubViewport();
        sv.Size = Vector2I.One * Resolution;
        sv.SetCanvasCullMaskBit(0, false);
        _depthViewports[idx] = sv;
        AddChild(_depthViewports[idx]);
        if (idx != _currentCellIdx)
        {
            sv.RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
        }
    }

    private void _CreateDepthCamera(int idx, float x, float z)
    {
        Camera3D cam;
        if (idx != 0)
        {
            cam = _depthCameras[0].Duplicate() as Camera3D;
        }
        else
        {
            cam = new Camera3D();
            cam.Near = DepthCameraNear;
            cam.Far = DepthCameraRange + cam.Near;
            cam.Projection = Camera3D.ProjectionType.Orthogonal;
            cam.Size = CELL_SIZE;
            cam.CullMask = 2;
            cam.RotationDegrees = new Vector3(90.0f, 0.0f, 0.0f);
        }

        _depthCameras[idx] = cam;
        _depthViewports[idx].AddChild(_depthCameras[idx]);
        _depthCameras[idx].GlobalPosition = new Vector3(x, -2.0f * DepthCameraNear, z);
    }

    private void _InitShaderParameters()
    {
        for (int i = 0; i < CELL_COUNT; i++)
        {
            _depthMaterials[i].SetShaderParameter("snow_height", SnowHeight);
            _depthMaterials[i].SetShaderParameter("patch_offset", -DepthCameraNear);
            _depthMaterials[i].SetShaderParameter("patch_range", DepthCameraRange);
            _depthMaterials[i].SetShaderParameter("height_map", TerrainRef.GetHeightMap());
            _depthMaterials[i].SetShaderParameter("max_terrain_height", TerrainRef.MaxHeight);
        }
    }

    private void _CheckCellChange()
    {
        float xDiff = PlayerRef.GlobalPosition.X - _currentCellCenter.X;
        float yDiff = PlayerRef.GlobalPosition.Z - _currentCellCenter.Y;

        bool changed = false;

        if (xDiff > CELL_SIZE / 2)
        {
            _currentCellCenter.X += CELL_SIZE;
            _currentCell.X += 1;
            changed = true;
        }
        else if (xDiff < -CELL_SIZE / 2)
        {
            _currentCellCenter.X -= CELL_SIZE;
            _currentCell.X -= 1;
            changed = true;
        }
        if (yDiff > CELL_SIZE / 2)
        {
            _currentCellCenter.Y += CELL_SIZE;
            _currentCell.Y += 1;
            changed = true;
        }
        else if (yDiff < -CELL_SIZE / 2)
        {
            _currentCellCenter.Y -= CELL_SIZE;
            _currentCell.Y -= 1;
            changed = true;
        }

        if (changed)
        {
            _depthViewports[_currentCellIdx].RenderTargetUpdateMode = SubViewport.UpdateMode.Disabled;
            _UpdateCellIdx();
            GD.Print("Changed to ", _currentCellCenter);
            GD.Print("Idx: ", _currentCellIdx);
            _depthViewports[_currentCellIdx].RenderTargetUpdateMode = SubViewport.UpdateMode.Always;
            // EmitSignal(SignalName.CellChanged);
        }
    }

    private void _UpdateCellIdx()
    {
        // _currentCellIdx = 3 * ((_currentCell.Y + CELL_MOD_OFFSET) % CELL_COUNT_ROW) + (_currentCell.X + CELL_MOD_OFFSET) % CELL_COUNT_ROW;
        int x = (_currentCell.X + CELL_MOD_OFFSET) % CELL_COUNT_ROW;
        int y = CELL_COUNT_ROW - 1 - ((_currentCell.Y + CELL_MOD_OFFSET) % CELL_COUNT_ROW);
        _currentCellIdx = CELL_COUNT_ROW * y + x;
    }
}
