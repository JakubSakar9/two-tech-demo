using Godot;

public struct HeightMap
{
    public byte[] bytes;
    public FastNoiseLite noiseFn;
    public Image heightImage;
    public ImageTexture height;
    private int _size;

    public HeightMap(int size)
    {
        bytes = new byte[size * size * sizeof(float)];
        noiseFn = new();
        heightImage = new();
        height = new();
        _size = size;

        noiseFn.Offset = new Vector3(-size / 2.0f, -size / 2.0f, 0.0f);
    }

    public unsafe void Generate()
    {
        fixed(byte* bytePointer = bytes)
        {
            float* floatPointer = (float*)bytePointer;
            for (int i = 0; i < _size; i++)
            {
                for (int j = 0; j < _size; j++)
                {
                    float noiseValue = (noiseFn.GetNoise2D(j, i) + 1.0f) / 2.0f;
                    floatPointer[i * _size + j] = noiseValue;
                }
            }
        }
        heightImage = Image.CreateFromData(_size, _size, false, Image.Format.Rf, bytes);
        heightImage.GenerateMipmaps();
        height.SetImage(heightImage);
    }

    public void MoveOrigin(Vector2 origin)
    {
        noiseFn.Offset = new Vector3(origin.X - _size/2, origin.Y - _size/2, 0.0f);
        Generate();
    }
}

public partial class Terrain : StaticBody3D
{
    const int NOISE_SWAP_COUNT = 4;

    [Signal] public delegate void MapShiftedEventHandler();

    [Export] public Player Player;
    [Export] public TerrainDeformer Deformer;
    [Export] public int ChunkSizeUnits = 256;
    [Export] public int CollisionSizeUnits = 8;
    [Export] public float MaxHeight = 32.0f;
    [Export] public float ChunkThresholdMultiplier = 1.125f;

    public Vector2 ChunkOrigin = Vector2.Zero;

    private MeshInstance3D _terrainMesh;
    private CollisionShape3D _terrainCollider;
    private HeightMapShape3D _heightMapShape;


    private HeightMap[] _heightMaps;

    private FastNoiseLite _collisionNoiseFunction;
    private Image _collisionImage;
    
    private int _noiseIndex = 0;

    public override void _Ready()
    {
        base._Ready();

        _terrainMesh = GetNode<MeshInstance3D>("%TerrainMesh");
        _terrainCollider = GetNode<CollisionShape3D>("%TerrainCollider");
        
        _heightMaps = new HeightMap[NOISE_SWAP_COUNT];
        _heightMapShape = new HeightMapShape3D();

        int heightmapSize = 3 * ChunkSizeUnits;

        for (int i = 0; i < NOISE_SWAP_COUNT; i++)
        {
            _heightMaps[i] = new(heightmapSize);
            _heightMaps[i].noiseFn.FractalLacunarity = 1.7f;
            _heightMaps[i].Generate();
        }

        _collisionNoiseFunction = new FastNoiseLite();
        UpdateCollisionHeightMap();

        CallDeferred(MethodName.AssignTexture);

        SetShaderParam("max_height", MaxHeight);
        SetShaderParam("height_map", _heightMaps[_noiseIndex].height);
        SetShaderParam("snow_height", 0.3f);

        // GetViewport().DebugDraw = Viewport.DebugDrawEnum.Wireframe;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        Vector2 playerPos2D = new(Player.GlobalPosition.X, Player.GlobalPosition.Z);
        Vector2 pos2D = new(_terrainMesh.GlobalPosition.X, _terrainMesh.GlobalPosition.Z);
        Vector2 positionDelta = playerPos2D - pos2D;
        while (positionDelta.X > 1f)
        {
            positionDelta.X -= 1f;
            pos2D.X += 1f;
        }
        while (positionDelta.X < -1f)
        {
            positionDelta.X += 1f;
            pos2D.X -= 1f;
        }
        while (positionDelta.Y > 1f)
        {
            positionDelta.Y -= 1f;
            pos2D.Y += 1f;
        }
        while (positionDelta.Y < -1f)
        {
            positionDelta.Y += 1f;
            pos2D.Y -= 1f;
        }
        _terrainMesh.GlobalPosition = new Vector3(pos2D.X, 0, pos2D.Y);
        CheckChunkChange(in pos2D);
    }

    public ImageTexture GetHeightMap()
    {
        return _heightMaps[_noiseIndex].height;
    }

    private void CheckChunkChange(ref readonly Vector2 position2D)
    {
        Vector2 playerOffset = position2D - ChunkOrigin;
        float thresholdDistance = ChunkSizeUnits * ChunkThresholdMultiplier / 2.0f;
        bool updateChunk = false;
        if (playerOffset.X < -thresholdDistance)
        {
            ChunkOrigin.X -= ChunkSizeUnits;
            updateChunk = true;
        }
        if (playerOffset.X > thresholdDistance)
        {
            ChunkOrigin.X += ChunkSizeUnits;
            updateChunk = true;
        }
        if (playerOffset.Y < -thresholdDistance)
        {
            ChunkOrigin.Y -= ChunkSizeUnits;
            updateChunk = true;
        }
        if (playerOffset.Y > thresholdDistance)
        {
            ChunkOrigin.Y += ChunkSizeUnits;
            updateChunk = true;
        }

        if (updateChunk)
        {
            GD.Print("Moved chunk origin to " + ChunkOrigin);
            CallDeferred(MethodName.UpdateHeightMap);
        }
        UpdateCollisionHeightMap();
    }

    private void UpdateCollisionHeightMap()
    {
        _collisionNoiseFunction.FractalLacunarity = 1.7f;
        _collisionNoiseFunction.Offset = new Vector3(Player.GlobalPosition.X - 0.5f, Player.GlobalPosition.Z - 0.5f, 0.0f);
        _collisionImage = Image.CreateEmpty(CollisionSizeUnits + 1, CollisionSizeUnits + 1, false, Image.Format.Rf);
        for (int i = 0; i <= CollisionSizeUnits; i++)
        {
            float y = i - CollisionSizeUnits / 2;
            for (int j = 0; j <= CollisionSizeUnits; j++)
            {
                float x = j - CollisionSizeUnits / 2;
                float value = (_collisionNoiseFunction.GetNoise2D(x, y) + 1.0f) / 2.0f;
                _collisionImage.SetPixel(j, i, new Color(value, 0.0f, 0.0f, 1.0f));
            }
        }
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, 0f, MaxHeight);
        _terrainCollider.Shape = _heightMapShape;
        _terrainCollider.GlobalPosition = new Vector3(Player.GlobalPosition.X, 0, Player.GlobalPosition.Z);
    }

    private async void UpdateHeightMap()
    {
        _noiseIndex = (_noiseIndex + 1) % NOISE_SWAP_COUNT;
        _heightMaps[_noiseIndex].MoveOrigin(ChunkOrigin);
        var timer = GetTree().CreateTimer(1.0f);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        SetShaderParam("height_map", _heightMaps[_noiseIndex].height);
        SetShaderParam("chunk_origin", ChunkOrigin);
        EmitSignal(SignalName.MapShifted);
    }

    private async void AssignTexture()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        SetShaderParam("displacement_map_range", Deformer.DisplacementMapRange);
        SetShaderParam("displacement_map", Deformer.GetDisplacement());
    }

    private void SetShaderParam(string property, Variant value)
    {
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter(property, value);
    }
}
