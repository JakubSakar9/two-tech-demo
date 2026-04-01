using Godot;

public struct HeightMap
{
    public byte[] bytes;
    public Image heightImage;
    public ImageTexture height; // R = terrain height, G = snow height, B = powdered snow height
    public ImageTexture3D windTexture;
    public FastNoiseLite noiseFnHF;
    public FastNoiseLite noiseFnLF;
    private readonly int _size;

    public HeightMap(FastNoiseLite pNoiseFnHF, FastNoiseLite pNoiseFnLF, int size)
    {
        bytes = new byte[4 * size * size * sizeof(float)];
        noiseFnHF = pNoiseFnHF.DuplicateDeep() as FastNoiseLite;
        noiseFnLF = pNoiseFnLF.DuplicateDeep() as FastNoiseLite;
        heightImage = new();
        height = new();
        windTexture = new();
        _size = size;
        noiseFnHF.Offset = new Vector3(-size / 2.0f, -size / 2.0f, 0.0f);
        noiseFnLF.Offset = new Vector3(-size / 2.0f, -size / 2.0f, 0.0f);
    }

    public unsafe void Generate(float maxHeight, float maxSnowHeight)
    {
        fixed(byte* bytePointer = bytes)
        {
            float* floatPointer = (float*)bytePointer;
            for (int i = 0; i < _size; i++)
            {
                for (int j = 0; j < _size; j++)
                {
                    float noiseValueHF = (noiseFnHF.GetNoise2D(j, i) + 1.0f) / 2.0f;
                    float noiseValueLF = (noiseFnLF.GetNoise2D(j, i) + 1.0f) / 2.0f;
                    float combined = noiseValueHF * noiseValueLF;
                    float height = maxHeight * combined;
                    floatPointer[4 * (i * _size + j)] = height;
                }
            }
        }
        heightImage = Image.CreateFromData(_size, _size, false, Image.Format.Rgbaf, bytes);
        heightImage.GenerateMipmaps();
        height.SetImage(heightImage);
    }

    public void MoveOrigin(Vector2 origin, float maxHeight, float maxSnowHeight)
    {
        noiseFnHF.Offset = new Vector3(origin.X - _size/2, origin.Y - _size/2, 0.0f);
        noiseFnLF.Offset = new Vector3(origin.X - _size/2, origin.Y - _size/2, 0.0f);
        Generate(maxHeight, maxSnowHeight);
    }
}

public partial class Terrain : StaticBody3D
{
    const int HEIGHTMAP_SWAP_COUNT = 4;

    [Export] public Player Player;
    [Export] public TerrainDeformer Deformer;
    [Export] public WindGenerator WindGen;
    [Export] public FastNoiseLite NoiseFunctionHF;
    [Export] public FastNoiseLite NoiseFunctionLF;
    [Export] public int ChunkSizeUnits = 256;
    [Export] public int CollisionSizeUnits = 8;
    [Export] public int WindLayerCount = 1;
    [Export] public float MaxHeight = 32.0f;
    [Export] public float ChunkThresholdMultiplier = 1.125f;
    [Export] public float MaxSnowHeight = 0.25f;

    

    public Vector2 ChunkOrigin = Vector2.Zero;

    private MeshInstance3D _terrainMesh;
    private CollisionShape3D _terrainCollider;
    private HeightMapShape3D _heightMapShape;
    private GpuParticlesAttractorVectorField3D _windField;
    private SnowCoverGenerator _scGen;


    private HeightMap[] _heightmaps;
    private Godot.Collections.Array<Image> _windImages;
    private Image _collisionImage;
    
    private int _heightmapIndex = HEIGHTMAP_SWAP_COUNT - 1;

    public override void _Ready()
    {
        base._Ready();

        _terrainMesh = GetNode<MeshInstance3D>("%TerrainMesh");
        _terrainCollider = GetNode<CollisionShape3D>("%TerrainCollider");
        _windField = GetNode<GpuParticlesAttractorVectorField3D>("%WindField");
        _scGen = GetNode<SnowCoverGenerator>("%SnowCoverGenerator");
        
        _heightmaps = new HeightMap[HEIGHTMAP_SWAP_COUNT];
        _heightMapShape = new HeightMapShape3D();

        int heightmapSize = 3 * ChunkSizeUnits;

        WindGen.Init(heightmapSize, WindLayerCount);
        _scGen.Init((uint)(3 * ChunkSizeUnits));

        for (uint i = 0; i < HEIGHTMAP_SWAP_COUNT; i++)
        {
            _heightmaps[i] = new(NoiseFunctionHF, NoiseFunctionLF, heightmapSize)
            {
                windTexture = new ImageTexture3D()
            };
            Godot.Collections.Array<Image> initImages = [];
            for (uint j = 0; j < heightmapSize; j++)
            {
                initImages.Add(Image.CreateEmpty(heightmapSize, WindLayerCount, false, Image.Format.Rgba8));
            }
            _heightmaps[i].windTexture.Create(Image.Format.Rgba8, heightmapSize, WindLayerCount, heightmapSize,
                false, initImages);
        }
        _windField.Size = new Vector3(heightmapSize, MaxHeight * 1.25f, heightmapSize);
        UpdateHeightMap();
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

        CallDeferred(MethodName.AssignTexture);
    }

    public ImageTexture GetHeightMap()
    {
        return _heightmaps[_heightmapIndex].height;
    }

    public Vector3 GetWindAtPoint(Vector3 point)
    {
        Aabb windAabb = _windField.GetAabb();
        Vector3 b = windAabb.Position + _windField.GlobalPosition;
        Vector3 e = windAabb.End + _windField.GlobalPosition;
        Vector3 uvw = (point - b) / (e - b);
        uvw = uvw.Clamp(Vector3.Zero, Vector3.One);

        int size = 3 * ChunkSizeUnits;
        int lCount = WindLayerCount;
        float tx = uvw.X * (size   - 1);
        float ty = uvw.Y * (lCount - 1);
        float tz = uvw.Z * (size   - 1);

        int x0 = (int)tx;
        int y0 = (int)ty;
        int z0 = (int)tz;
        int x1 = Mathf.Min((int)tx + 1, size   - 1);
        int y1 = Mathf.Min((int)ty + 1, lCount - 1);
        int z1 = Mathf.Min((int)tz + 1, size   - 1);

        float fx = tx - (int)tx;
        float fy = ty - (int)ty;
        float fz = tz - (int)tz;

        Vector3 c000 = GetImgVec3(_windImages[z0], x0, y0);
        Vector3 c100 = GetImgVec3(_windImages[z0], x1, y0);
        Vector3 c010 = GetImgVec3(_windImages[z0], x0, y1);
        Vector3 c110 = GetImgVec3(_windImages[z0], x1, y1);
        Vector3 c001 = GetImgVec3(_windImages[z1], x0, y0);
        Vector3 c101 = GetImgVec3(_windImages[z1], x1, y0);
        Vector3 c011 = GetImgVec3(_windImages[z1], x0, y1);
        Vector3 c111 = GetImgVec3(_windImages[z1], x1, y1);

        Vector3 c00 = c000.Lerp(c100, fx);
        Vector3 c01 = c001.Lerp(c101, fx);
        Vector3 c10 = c010.Lerp(c110, fx);
        Vector3 c11 = c011.Lerp(c111, fx);

        Vector3 c0 = c00.Lerp(c10, fy);
        Vector3 c1 = c01.Lerp(c11, fy);
        Vector3 sampled = c0.Lerp(c1, fz);
        return _windField.Strength * (2.0f * sampled - Vector3.One);
    }

    public float GetSnowHeight()
    {
        int size = 3 * ChunkSizeUnits;
        Vector2 b = ChunkOrigin - size * Vector2.One / 2.0f;
        Vector2 e = ChunkOrigin + size * Vector2.One / 2.0f;
        Vector3 plPos = Player.GlobalPosition;
        Vector2 uv = (new Vector2(plPos.X, plPos.Z) - b) / (e - b);
        uv = uv.Clamp(Vector2.Zero, Vector2.One);

        float tx = uv.X * (size - 1);
        float ty = uv.Y * (size - 1);

        int x0 = (int)tx;
        int y0 = (int)ty;
        int x1 = Mathf.Min((int)tx + 1, size - 1);
        int y1 = Mathf.Min((int)ty + 1, size - 1);

        float fx = tx - (int)tx;
        float fy = ty - (int)ty;

        float c00 = GetImgSH(_heightmaps[_heightmapIndex].heightImage, x0, y0);
        float c01 = GetImgSH(_heightmaps[_heightmapIndex].heightImage, x0, y1);
        float c10 = GetImgSH(_heightmaps[_heightmapIndex].heightImage, x1, y0);
        float c11 = GetImgSH(_heightmaps[_heightmapIndex].heightImage, x1, y1);

        float c0 = Mathf.Lerp(c00, c10, fx);
        float c1 = Mathf.Lerp(c01, c11, fx);
        return Mathf.Lerp(c0, c1, fy);
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
        Vector3 noiseOffset = new(Player.GlobalPosition.X - 0.5f, Player.GlobalPosition.Z - 0.5f, 0.0f);
        NoiseFunctionHF.Offset = noiseOffset;
        NoiseFunctionLF.Offset = noiseOffset;
        _collisionImage = Image.CreateEmpty(CollisionSizeUnits + 1, CollisionSizeUnits + 1, false, Image.Format.Rf);
        for (int i = 0; i <= CollisionSizeUnits; i++)
        {
            float y = i - CollisionSizeUnits / 2;
            for (int j = 0; j <= CollisionSizeUnits; j++)
            {
                float x = j - CollisionSizeUnits / 2;
                float valueHF = (NoiseFunctionHF.GetNoise2D(x, y) + 1.0f) / 2.0f;
                float valueLF = (NoiseFunctionLF.GetNoise2D(x, y) + 1.0f) / 2.0f;
                _collisionImage.SetPixel(j, i, new Color(valueHF * valueLF, 0.0f, 0.0f, 1.0f));
            }
        }
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, 0f, MaxHeight);
        _terrainCollider.Shape = _heightMapShape;
        _terrainCollider.GlobalPosition = new Vector3(Player.GlobalPosition.X, 0, Player.GlobalPosition.Z);
    }

    private void UpdateHeightMap()
    {
        _heightmapIndex = (_heightmapIndex + 1) % HEIGHTMAP_SWAP_COUNT;
        _heightmaps[_heightmapIndex].MoveOrigin(ChunkOrigin, MaxHeight, MaxSnowHeight);
        WindGen.Generate(ref _heightmaps[_heightmapIndex]);

        _windField.Position = new Vector3(ChunkOrigin.X, _windField.Size.Y / 2.0f, ChunkOrigin.Y);
        WindGen.CopyWindTexture(ref _heightmaps[_heightmapIndex].windTexture, ref _windImages);
        _windField.Texture = _heightmaps[_heightmapIndex].windTexture;

        _scGen.UseHeightMap(in _heightmaps[_heightmapIndex]);
        _scGen.Preprocess();
        _scGen.Iterate(4);
        _scGen.Postprocess();
        _scGen.UpdateHeightMap(ref _heightmaps[_heightmapIndex]);

        GD.Print("Using uniform hm...");
        SetShaderParam("height_map", _heightmaps[_heightmapIndex].height);
        SetShaderParam("chunk_origin", ChunkOrigin);
    }

    private async void AssignTexture()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        SetShaderParam("displacement_map_range", Deformer.DisplacementMapRange);
        SetShaderParam("displacement_maps", Deformer.GetDisplacementTextures());
    }

    private void SetShaderParam(string property, Variant value)
    {
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter(property, value);
    }

    private static Vector3 GetImgVec3(Image img, int x, int y)
    {
        Color c = img.GetPixel(x, y);
        return new Vector3(c.R, c.G, c.B);
    }

    private static float GetImgSH(Image img, int x, int y)
    {
        Color c = img.GetPixel(x, y);
        return c.G + c.B;
    }
}
