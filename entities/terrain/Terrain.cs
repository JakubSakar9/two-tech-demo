using Godot;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

public partial class Terrain : StaticBody3D
{
    const int NOISE_SWAP_COUNT = 4;

    [Signal] public delegate void MapShiftedEventHandler();

    [Export] public Player Player;
    [Export] public int ChunkSizeUnits = 256;
    [Export] public int CollisionSizeUnits = 8;
    [Export] public float MaxHeight = 32.0f;
    [Export] public float ChunkThresholdMultiplier = 1.125f;

    public Vector2 ChunkOrigin = Vector2.Zero;

    private MeshInstance3D _terrainMesh;
    private CollisionShape3D _terrainCollider;
    private HeightMapShape3D _heightMapShape;

    private FastNoiseLite[] _noiseFunctions;
    private ImageTexture[] _heightMaps;
    private NoiseTexture2D[] _normalMaps;

    private FastNoiseLite _collisionNoiseFunction;
    private Image _collisionImage;
    
    private int _noiseIndex = 0;

    public override void _Ready()
    {
        base._Ready();
        _terrainMesh = GetNode<MeshInstance3D>("%TerrainMesh");
        _terrainCollider = GetNode<CollisionShape3D>("%TerrainCollider");
        
        _noiseFunctions = new FastNoiseLite[NOISE_SWAP_COUNT];
        _heightMaps = new ImageTexture[NOISE_SWAP_COUNT];
        _normalMaps = new NoiseTexture2D[NOISE_SWAP_COUNT];
        _heightMapShape = new HeightMapShape3D();

        int heightmapSize = 3 * ChunkSizeUnits;

        for (int i = 0; i < NOISE_SWAP_COUNT; i++)
        {
            _noiseFunctions[i] = new FastNoiseLite();
            _heightMaps[i] = new ImageTexture();
            _normalMaps[i] = new NoiseTexture2D();

            _noiseFunctions[i].FractalLacunarity = 1.7f;
            _noiseFunctions[i].Offset = new Vector3(-1.5f * ChunkSizeUnits, -1.5f * ChunkSizeUnits, 0.0f);

            byte[] imageData = new byte[heightmapSize * heightmapSize * sizeof(float)];
            GenerateHeightMap(ref imageData, i);
            var heightImage = Image.CreateFromData(3 * ChunkSizeUnits, 3 * ChunkSizeUnits, false, Image.Format.Rf, imageData);
            heightImage.GenerateMipmaps();
            GD.Print(heightImage.GetPixel(0, 0).R);
            _heightMaps[i].SetImage(heightImage);
            
            _normalMaps[i].Noise = _noiseFunctions[i];
            _normalMaps[i].Width = 3 * ChunkSizeUnits;
            _normalMaps[i].Height = 3 * ChunkSizeUnits;
            _normalMaps[i].AsNormalMap = true;
        }

        _collisionNoiseFunction = new FastNoiseLite();
        UpdateCollisionHeightMap();

        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("max_height", MaxHeight);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", _heightMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("normal_map", _normalMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("snow_height", 0.3);

        // GenDebugFloats();
        // (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("tex_floats", _debugFloatsTex);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        Vector2 position2D = new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z);
        _terrainMesh.GlobalPosition = new Vector3(position2D.X, 0, position2D.Y);
        _CheckChunkChange(in position2D);
    }

    public void UpdateDisplacement(ref readonly Texture2D[] displacement)
    {
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("displacement_tex", displacement);
    }

    public NoiseTexture2D GetNormalTexture()
    {
        return _normalMaps[_noiseIndex];
    }

    public ImageTexture GetHeightMap()
    {
        return _heightMaps[_noiseIndex];
    }

    private void _CheckChunkChange(ref readonly Vector2 position2D)
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
        // NOTE: These numbers are arbitrarily set to make the collider work. It should be investigated why this is needed.
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, 0f, MaxHeight);
        _terrainCollider.Shape = _heightMapShape;
        _terrainCollider.GlobalPosition = new Vector3(Player.GlobalPosition.X, 0, Player.GlobalPosition.Z);
    }

    private async void UpdateHeightMap()
    {
        _noiseIndex = (_noiseIndex + 1) % NOISE_SWAP_COUNT;
        _noiseFunctions[_noiseIndex].Offset = new Vector3(ChunkOrigin.X - 1.5f * ChunkSizeUnits, ChunkOrigin.Y - 1.5f * ChunkSizeUnits, 0);
        var timer = GetTree().CreateTimer(1.0f);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", _heightMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("normal_map", _normalMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("chunk_origin", ChunkOrigin);
        EmitSignal(SignalName.MapShifted);
    }

    private unsafe void GenerateHeightMap(ref byte[] bytes, int noiseIndex = -1)
    {
        if (noiseIndex == -1)
        {
            noiseIndex = _noiseIndex;
        }

        int heightmapSize = 3 * ChunkSizeUnits;

        fixed(byte* bytePointer = bytes)
        {
            float* floatPointer = (float*)bytePointer;
            for (int i = 0; i < heightmapSize; i++)
            {
                for (int j = 0; j < heightmapSize; j++)
                {
                    float noiseValue = (_noiseFunctions[noiseIndex].GetNoise2D(j, i) + 1.0f) / 2.0f;
                    floatPointer[i * heightmapSize + j] = noiseValue;
                }
            }
        }
    }
}
