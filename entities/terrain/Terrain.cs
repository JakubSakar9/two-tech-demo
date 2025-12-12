using Godot;
using System;

public partial class Terrain : StaticBody3D
{
    [Export] public Player Player;
    [Export] public int SizeUnits = 256;
    [Export] public float MaxHeight = 32.0f;

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
        _heightMap.Width = 3 * SizeUnits;
        _heightMap.Height = 3 * SizeUnits;
        
        _normalMap = new NoiseTexture2D();
        _normalMap.Noise = _noiseFunction;
        _normalMap.Width = 3 * SizeUnits;
        _normalMap.Height = 3 * SizeUnits;
        _normalMap.AsNormalMap = true;

        FastNoiseLite offsetNoise = new FastNoiseLite();
        offsetNoise.FractalLacunarity = 1.7f;
        offsetNoise.Offset = new Vector3(SizeUnits, SizeUnits, 0);

        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("max_height", MaxHeight);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("height_map", _heightMap);
        (_terrainMesh.MaterialOverride as ShaderMaterial).SetShaderParameter("normal_map", _normalMap);
        _collisionImage = offsetNoise.GetImage(SizeUnits, SizeUnits);
        _collisionImage.Resize(SizeUnits + 1, SizeUnits + 1);
        _collisionImage.Convert(Image.Format.R8);
        float shapeOffset = 3.0f;
        _heightMapShape.UpdateMapDataFromImage(_collisionImage, shapeOffset, MaxHeight - shapeOffset);
        _terrainCollider.Shape = _heightMapShape;
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
        _terrainMesh.GlobalPosition = new Vector3(Player.GlobalPosition.X, 0, Player.GlobalPosition.Z);
    }
}
