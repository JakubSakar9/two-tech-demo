using Godot;
using System;

public partial class Terrain : StaticBody3D
{
    [Export] public Player Player;
    [Export] public int ChunkSizeUnits = 256;
    [Export] public float MaxHeight = 32.0f;
    [Export] public float ChunkThresholdMultiplier = 1.125f;

    private Vector2 _chunkOrigin = Vector2.Zero;

    private MeshInstance3D _terrainMesh;
    private CollisionShape3D _terrainCollider;
    private HeightMapShape3D _heightMapShape;

    private FastNoiseLite _noiseFunction;
    private NoiseTexture2D _heightMap;
    private NoiseTexture2D _normalMap;
    private Image _collisionImage;

    public override void _Ready()
    {
        base._Ready();
        _terrainMesh = GetNode<MeshInstance3D>("%TerrainMesh");
        _terrainCollider = GetNode<CollisionShape3D>("%TerrainCollider");
        _heightMap = new NoiseTexture2D();
        _heightMapShape = new HeightMapShape3D();

        _noiseFunction = new FastNoiseLite();
        _noiseFunction.FractalLacunarity = 1.7f;
        _heightMap.Noise = _noiseFunction;
        _heightMap.Width = 3 * ChunkSizeUnits;
        _heightMap.Height = 3 * ChunkSizeUnits;
        
        _normalMap = new NoiseTexture2D();
        _normalMap.Noise = _noiseFunction;
        _normalMap.Width = 3 * ChunkSizeUnits;
        _normalMap.Height = 3 * ChunkSizeUnits;
        _normalMap.AsNormalMap = true;

        _UpdateCollisionHeightMap();

        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("max_height", MaxHeight);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", _heightMap);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("normal_map", _normalMap);
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
            _UpdateHeightMap();
            _UpdateCollisionHeightMap();
        }
    }

    private void _UpdateCollisionHeightMap()
    {
        FastNoiseLite offsetNoise = new FastNoiseLite();
        offsetNoise.FractalLacunarity = 1.7f;
        offsetNoise.Offset = new Vector3(_chunkOrigin.X, _chunkOrigin.Y, 0);

        _collisionImage = offsetNoise.GetImage(3 * ChunkSizeUnits, 3 * ChunkSizeUnits);
        _collisionImage.Resize(3 * ChunkSizeUnits + 1, 3 * ChunkSizeUnits + 1);
        _collisionImage.Convert(Image.Format.R8);
        float shapeOffset = 0.0f;
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, shapeOffset, MaxHeight - shapeOffset);
        _terrainCollider.Shape = _heightMapShape;
        _terrainCollider.GlobalPosition = new Vector3(_chunkOrigin.X, 0, _chunkOrigin.Y);
    }

    private void _UpdateHeightMap()
    {
        _noiseFunction.Offset = new Vector3(_chunkOrigin.X, _chunkOrigin.Y, 0);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("chunk_origin", _chunkOrigin);
    }
}
