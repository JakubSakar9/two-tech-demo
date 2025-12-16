using Godot;
using System;
using System.Threading.Tasks;

public partial class Terrain : StaticBody3D
{
    const int NOISE_SWAP_COUNT = 4;

    [Export] public Player Player;
    [Export] public int ChunkSizeUnits = 256;
    [Export] public int CollisionSizeUnits = 8;
    [Export] public float MaxHeight = 32.0f;
    [Export] public float ChunkThresholdMultiplier = 1.125f;

    private Vector2 _chunkOrigin = Vector2.Zero;

    private MeshInstance3D _terrainMesh;
    private CollisionShape3D _terrainCollider;
    private HeightMapShape3D _heightMapShape;

    private FastNoiseLite[] _noiseFunctions;
    private NoiseTexture2D[] _heightMaps;
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
        _heightMaps = new NoiseTexture2D[NOISE_SWAP_COUNT];
        _normalMaps = new NoiseTexture2D[NOISE_SWAP_COUNT];
        _heightMapShape = new HeightMapShape3D();

        for (int i = 0; i < NOISE_SWAP_COUNT; i++)
        {
            _noiseFunctions[i] = new FastNoiseLite();
            _heightMaps[i] = new NoiseTexture2D();
            _normalMaps[i] = new NoiseTexture2D();

            _noiseFunctions[i].FractalLacunarity = 1.7f;
            _noiseFunctions[i].Offset = new Vector3(-1.5f * ChunkSizeUnits, -1.5f * ChunkSizeUnits, 0.0f);
            _heightMaps[i].Noise = _noiseFunctions[i];
            _heightMaps[i].Width = 3 * ChunkSizeUnits;
            _heightMaps[i].Height = 3 * ChunkSizeUnits;
            
            _normalMaps[i].Noise = _noiseFunctions[i];
            _normalMaps[i].Width = 3 * ChunkSizeUnits;
            _normalMaps[i].Height = 3 * ChunkSizeUnits;
            _normalMaps[i].AsNormalMap = true;
        }

        _collisionNoiseFunction = new FastNoiseLite();
        _UpdateCollisionHeightMap();

        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("max_height", MaxHeight);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", _heightMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("normal_map", _normalMaps[_noiseIndex]);
    }

    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Keycode == Key.Q && keyEvent.IsPressed())
            {
                GetViewport().DebugDraw = Viewport.DebugDrawEnum.Wireframe;
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        Vector2 position2D = new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z);
        _terrainMesh.GlobalPosition = new Vector3(position2D.X, 0, position2D.Y);
        _CheckChunkChange(in position2D);
    }

    private void _CheckChunkChange(ref readonly Vector2 position2D)
    {
        Vector2 playerOffset = position2D - _chunkOrigin;
        float thresholdDistance = ChunkSizeUnits * ChunkThresholdMultiplier / 2.0f;
        bool updateChunk = false;
        if (playerOffset.X < -thresholdDistance)
        {
            _chunkOrigin.X -= ChunkSizeUnits;
            updateChunk = true;
        }
        if (playerOffset.X > thresholdDistance)
        {
            _chunkOrigin.X += ChunkSizeUnits;
            updateChunk = true;
        }
        if (playerOffset.Y < -thresholdDistance)
        {
            _chunkOrigin.Y -= (float)ChunkSizeUnits;
            updateChunk = true;
        }
        if (playerOffset.Y > thresholdDistance)
        {
            _chunkOrigin.Y += (float)ChunkSizeUnits;
            updateChunk = true;
        }

        if (updateChunk)
        {
            GD.Print("Moved chunk origin to " + _chunkOrigin);
            CallDeferred(MethodName._UpdateHeightMap);
        }
        _UpdateCollisionHeightMap();
    }

    private void _UpdateCollisionHeightMap()
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
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, -6.7f, MaxHeight + 7f);
        _terrainCollider.Shape = _heightMapShape;
        _terrainCollider.GlobalPosition = new Vector3(Player.GlobalPosition.X, 0, Player.GlobalPosition.Z);
    }

    private async void _UpdateHeightMap()
    {
        _noiseIndex = (_noiseIndex + 1) % NOISE_SWAP_COUNT;
        _noiseFunctions[_noiseIndex].Offset = new Vector3(_chunkOrigin.X - 1.5f * ChunkSizeUnits, _chunkOrigin.Y - 1.5f * ChunkSizeUnits, 0);
        // _heightMaps[_noiseIndex].Noise = _noiseFunctions[_noiseIndex];
        var timer = GetTree().CreateTimer(1.0f);
        await ToSignal(timer, SceneTreeTimer.SignalName.Timeout);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", _heightMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("normal_map", _normalMaps[_noiseIndex]);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("chunk_origin", _chunkOrigin);
    }
}
