using System;
using System.Diagnostics;
using System.Threading;
using Godot;

public partial class HeightMap : GodotObject
{
    public byte[] Bytes;
    public Image HeightImage;
    public ImageTexture Height; // R = terrain height, G = snow height, B = powdered snow height
    // public ImageTexture3D windTexture;
    public FastNoiseLite NoiseFnHf;
    public FastNoiseLite NoiseFnLf;

    private readonly int _size;
    private float _maxHeight;
    private Terrain _terrain;

    public HeightMap(FastNoiseLite pNoiseFnHF, FastNoiseLite pNoiseFnLF, int size, Terrain terrain)
    {
        NoiseFnHf = pNoiseFnHF.DuplicateDeep() as FastNoiseLite;
        NoiseFnLf = pNoiseFnLF.DuplicateDeep() as FastNoiseLite;
        HeightImage = new();
        Height = new();
        _size = size;
        _terrain = terrain;
        NoiseFnHf.Offset = new Vector3(-size / 2.0f, -size / 2.0f, 0.0f);
        NoiseFnLf.Offset = new Vector3(-size / 2.0f, -size / 2.0f, 0.0f);
    }

    public void Generate(float maxHeight)
    {
        _maxHeight = maxHeight;
        ThreadPool.QueueUserWorkItem(GenerateHeightmapData);
    }

    public void MoveOrigin(Vector2 origin, float maxHeight)
    {
        NoiseFnHf.Offset = new Vector3(origin.X - _size/2, origin.Y - _size/2, 0.0f);
        NoiseFnLf.Offset = new Vector3(origin.X - _size/2, origin.Y - _size/2, 0.0f);
        Generate(maxHeight);
    }

    public unsafe void ClearSnowBytes()
    {
        fixed(byte* bytePointer = Bytes)
        {
            float* floatPointer = (float*)bytePointer;
            for (int i = 0; i < _size; i++)
            {
                for (int j = 0; j < _size; j++)
                {
                    floatPointer[4 * (i * _size + j) + 1] = 0.0f;
                    floatPointer[4 * (i * _size + j) + 2] = 0.0f;
                    floatPointer[4 * (i * _size + j) + 3] = 0.0f;
                }
            }
        }
    }

    private unsafe void GenerateHeightmapData(object stateInfo)
    {
        Bytes = new byte[4 * _size * _size * sizeof(float)];
        fixed(byte* bytePointer = Bytes)
        {
            float* floatPointer = (float*)bytePointer;
            for (int i = 0; i < _size; i++)
            {
                for (int j = 0; j < _size; j++)
                {
                    float noiseValueHF = (NoiseFnHf.GetNoise2D(j, i) + 1.0f) / 2.0f;
                    float noiseValueLF = (NoiseFnLf.GetNoise2D(j, i) + 1.0f) / 2.0f;
                    float combined = noiseValueHF * noiseValueLF;
                    float height = _maxHeight * combined;
                    floatPointer[4 * (i * _size + j)] = height;
                }
            }
        }
        CallDeferred(MethodName.PopulateHeightImage);
    }

    private void PopulateHeightImage()
    {
        HeightImage = Image.CreateFromData(_size, _size, false, Image.Format.Rgbaf, Bytes);
        HeightImage.GenerateMipmaps();
        Height.SetImage(HeightImage);
        _terrain.EmitSignal(Terrain.SignalName.FinishedGenerating);
        Stopwatch stw = new();
        stw.Start();
        RenderingServer.CallOnRenderThread(Callable.From(_terrain.ComputeTextures));
        stw.Stop();
        GD.Print("Populate height image: " + stw.Elapsed.TotalMilliseconds + "ms");
    }
}

public partial class Terrain : StaticBody3D
{
    const int HEIGHTMAP_SWAP_COUNT = 4;

    [ExportCategory("References")]
    [Export] public Player Player;
    [Export] public TerrainDeformer Deformer;
    [Export] public WindGenerator WindGen;
    [Export] public LoadingCamera LoadCam;
    [Export] public VegetationManager VManager;

    [ExportCategory("Generation")]
    [Export] public float MaxAltitude = 32.0f;
    [Export] public float RockGroundHeight = 11.0f;
    [Export] public FastNoiseLite NoiseFunctionHF;
    [Export] public FastNoiseLite NoiseFunctionLF;
    [Export] public int ChunkSizeUnits = 256;

    [ExportCategory("Misc")]
    [Export] public bool RenderWireframe = false;
    [Export] public int CollisionSizeUnits = 8;
    [Export] public float ChunkThresholdMultiplier = 1.125f;

    [Signal] public delegate void FinishedGeneratingEventHandler();
    
    public Vector2 ChunkOrigin = Vector2.Zero;
    public Vector3 LocalWind = Vector3.Zero;

    private MeshInstance3D _terrainMesh;
    private CollisionShape3D _terrainCollider;
    private HeightMapShape3D _heightMapShape;
    private GpuParticlesAttractorVectorField3D _windField;
    private SnowCoverGenerator _scGen;



    private HeightMap[] _heightmaps;
    private Image _surfaceImage;
    private Texture3Drd _windTexture;
    private Image _collisionImage;
    
    private int _heightmapIndex = HEIGHTMAP_SWAP_COUNT - 1;
    private bool _initial = true;

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

        _windTexture = new();
        WindGen.Init(heightmapSize, ref _windTexture);
        _scGen.Init((uint)(3 * ChunkSizeUnits), WindGen);

        Random rng = new Random(DateTime.Now.Microsecond);
        NoiseFunctionLF.Seed = rng.Next();
        NoiseFunctionHF.Seed = rng.Next();

        for (uint i = 0; i < HEIGHTMAP_SWAP_COUNT; i++)
        {
            _heightmaps[i] = new(NoiseFunctionHF, NoiseFunctionLF, heightmapSize, this);
            Godot.Collections.Array<Image> initImages = [];
            for (uint j = 0; j < heightmapSize; j++)
            {
                initImages.Add(Image.CreateEmpty(heightmapSize, WindGen.LayerCount, false, Image.Format.Rgba8));
            }
        }
        _windField.Size = new Vector3(heightmapSize, MaxAltitude * 1.25f, heightmapSize);
        _windField.Strength = WindGen.MaxWindSpeed;
        _windField.Texture = _windTexture;
        SetShaderParam("rock_fade_start", RockGroundHeight);
        SetShaderParam("rock_fade_end", RockGroundHeight + 0.3f);
        SetShaderParam("chunk_origin", ChunkOrigin);
        CallDeferred(MethodName.GenerateInitial);

        if (RenderWireframe)
        {
            GetViewport().DebugDraw = Viewport.DebugDrawEnum.Wireframe;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        Vector2 playerPos2D = new(Player.GlobalPosition.X, Player.GlobalPosition.Z);
        Vector2 pos2D = new(_terrainMesh.GlobalPosition.X, _terrainMesh.GlobalPosition.Z);
        Vector2 positionDelta = playerPos2D - pos2D;
        while (positionDelta.X > 2f)
        {
            positionDelta.X -= 2f;
            pos2D.X += 2f;
        }
        while (positionDelta.X < -2f)
        {
            positionDelta.X += 2f;
            pos2D.X -= 2f;
        }
        while (positionDelta.Y > 2f)
        {
            positionDelta.Y -= 2f;
            pos2D.Y += 2f;
        }
        while (positionDelta.Y < -2f)
        {
            positionDelta.Y += 2f;
            pos2D.Y -= 2f;
        }
        _terrainMesh.GlobalPosition = new Vector3(pos2D.X, 0, pos2D.Y);
        CheckChunkChange(in pos2D);

        CallDeferred(MethodName.AssignTexture);
        SetShaderParam("used_maps", Deformer.GetUsedChunks());
    }

    public ImageTexture GetHeightMap()
    {
        return _heightmaps[_heightmapIndex].Height;
    }

    public void GetWindAtPoint(Vector3 point)
    {
        if (_surfaceImage == null || _surfaceImage.IsEmpty())
        {
            return;
        }
        int size = 3 * ChunkSizeUnits;
        Aabb windAabb = _windField.GetAabb();
        Vector3 b = windAabb.Position + _windField.GlobalPosition;
        Vector3 e = windAabb.End + _windField.GlobalPosition;
        Vector3 uvw = (point - b) / (e - b);
        Vector2 uv = new Vector2(uvw.X, uvw.Z);
        uv = uv.Clamp(Vector2.Zero, Vector2.One);

        float tx = uv.X * (size - 1);
        float ty = uv.Y * (size - 1);

        int x0 = (int)tx;
        int y0 = (int)ty;
        int x1 = Mathf.Min((int)tx + 1, size - 1);
        int y1 = Mathf.Min((int)ty + 1, size - 1);

        float fx = tx - (int)tx;
        float fy = ty - (int)ty;

        Vector3 c00 = GetImgVec3(_surfaceImage, x0, y0);
        Vector3 c10 = GetImgVec3(_surfaceImage, x1, y0);
        Vector3 c01 = GetImgVec3(_surfaceImage, x0, y1);
        Vector3 c11 = GetImgVec3(_surfaceImage, x1, y1);

        Vector3 c0 = c00.Lerp(c10, fx);
        Vector3 c1 = c01.Lerp(c11, fx);
        Vector3 sampled = c0.Lerp(c1, fy);
        LocalWind = _windField.Strength * (2.0f * sampled - Vector3.One);
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

        float c00 = GetImgSH(_heightmaps[_heightmapIndex].HeightImage, x0, y0);
        float c01 = GetImgSH(_heightmaps[_heightmapIndex].HeightImage, x0, y1);
        float c10 = GetImgSH(_heightmaps[_heightmapIndex].HeightImage, x1, y0);
        float c11 = GetImgSH(_heightmaps[_heightmapIndex].HeightImage, x1, y1);

        float c0 = Mathf.Lerp(c00, c10, fx);
        float c1 = Mathf.Lerp(c01, c11, fx);
        return Mathf.Lerp(c0, c1, fy);
    }

    public void AlignPlayer(bool ignoreAboveGround = false)
    {
        Vector2 p2d = new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z);
        p2d += 1.5f * ChunkSizeUnits * Vector2.One - ChunkOrigin;
        float y = _heightmaps[_heightmapIndex].HeightImage.GetPixelv((Vector2I)p2d).R;
        if (ignoreAboveGround && Player.GlobalPosition.Y > y) return;
        Player.GlobalPosition = new Vector3(Player.GlobalPosition.X, y, Player.GlobalPosition.Z);
    }

    public void ComputeTextures()
    {
        Stopwatch stw = new();
        stw.Start();
        _surfaceImage = null;
        WindGen.Generate(ref _heightmaps[_heightmapIndex]);
        _windField.Position = new Vector3(ChunkOrigin.X, _windField.Size.Y / 2.0f, ChunkOrigin.Y);
        int hmSize = 3 * ChunkSizeUnits;
        _heightmaps[(_heightmapIndex + HEIGHTMAP_SWAP_COUNT - 1) % HEIGHTMAP_SWAP_COUNT].Bytes = new byte[4 * hmSize * hmSize * sizeof(float)];
        _scGen.Generate(ref _heightmaps[_heightmapIndex]);
        stw.Stop();
        GD.Print("Texture compute in " + stw.ElapsedMilliseconds + "ms");
    }

    public void Pause()
    {
        SetPhysicsProcess(false);
        Deformer.Pause();
    }

    public void Unpause()
    {
        SetPhysicsProcess(true);
        Deformer.Unpause();
    }

    public void RegenerateSnow()
    {
        _scGen.GenerateCycleSequence();
        _heightmaps[_heightmapIndex].ClearSnowBytes();
        RenderingServer.CallOnRenderThread(Callable.From(() => _scGen.Generate(ref _heightmaps[_heightmapIndex])));
    }

    private async void GenerateInitial()
    {
        Player.SetProcess(false);
        LoadCam.Current = true;
        Player.Hide();
        UpdateHeightMap();
        await ToSignal(this, SignalName.FinishedGenerating);
        LoadCam.HideText();
        AlignPlayer();
        Player.Show();
        Player.MakeFirstPerson();
        Player.SetProcess(true);
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
            UpdateHeightMap();
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
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, 0f, MaxAltitude);
        _terrainCollider.Shape = _heightMapShape;
        _terrainCollider.GlobalPosition = new Vector3(Player.GlobalPosition.X, 0, Player.GlobalPosition.Z);
    }

    private void UpdateHeightMap()
    {
        _heightmapIndex = (_heightmapIndex + 1) % HEIGHTMAP_SWAP_COUNT;
        _heightmaps[_heightmapIndex].MoveOrigin(ChunkOrigin, MaxAltitude);
    }
    
    private void SyncHeightmap(byte[] data)
    {
        _heightmaps[_heightmapIndex].Bytes = data;
        _heightmaps[_heightmapIndex].HeightImage = Image.CreateFromData(3 * ChunkSizeUnits, 3 * ChunkSizeUnits, false, Image.Format.Rgbaf, _heightmaps[_heightmapIndex].Bytes);

        if (_scGen.SaveDebugTexture)
        {
            string suffix = _scGen.DebugTextureStep++.ToString("D3") + ".exr";
            _heightmaps[_heightmapIndex].HeightImage.SaveExr("res://debug_output/height_map" + suffix);
        }

        _heightmaps[_heightmapIndex].HeightImage.GenerateMipmaps();
        _heightmaps[_heightmapIndex].Height.SetImage(_heightmaps[_heightmapIndex].HeightImage);

        SetShaderParam("height_map", _heightmaps[_heightmapIndex].Height);
        SetShaderParam("chunk_origin", ChunkOrigin);

        if (_initial)
        {
            VManager.GenerateInitial(_heightmaps[_heightmapIndex]);
        }
        else
        {
            // Generate just one row
        }

        _initial = false;
    }

    private void SyncWindSurface(Image surfaceImage)
    {
        _surfaceImage = surfaceImage.Duplicate() as Image;
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
        if (x >= img.GetWidth())
        {
            return 0.0f;
        }
        Color c = img.GetPixel(x, y);
        return c.G + c.B;
    }
}
