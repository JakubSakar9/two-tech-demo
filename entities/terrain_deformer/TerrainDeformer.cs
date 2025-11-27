using Godot;
using System;

public partial class TerrainDeformer : Node3D
{
    const float DEFORMER_RADIUS = 0.4f;

    [Export] public Player Player;
    [Export] public MeshInstance3D UnderlyingSurface;
    [Export] public Material SnowMaterial;
    // [Export] public Image DefaultDisplacementMap;
    [Export] public float PatchSize = 20.0f;
    [Export] public float SnowHeight = 0.3f;

    private MeshInstance3D _deformerMesh;
    private MeshInstance3D _depthFilter;
    private MeshInstance3D _snowSurface;
    private SubViewport _subViewport;
    private Camera3D _deformCamera;
    // private ImageTexture _combinedTexture;

    // private GodotObject _displacementTextureCompute;

    public override void _Ready()
    {
        base._Ready();
        // Node init
        _deformerMesh = GetNode<MeshInstance3D>("%DeformerMesh");
        _depthFilter = GetNode<MeshInstance3D>("%DepthFilter");
        _subViewport = GetNode<SubViewport>("%SubViewport");
        _deformCamera = GetNode<Camera3D>("%DeformCamera");


        // Snow surface generation
        _snowSurface = new MeshInstance3D();
        var snowPlane = new PlaneMesh();
        snowPlane.Size = 20.0f * Vector2.One;
        snowPlane.SubdivideWidth = 255;
        snowPlane.SubdivideDepth = 255;
        snowPlane.Material = SnowMaterial;
        _snowSurface.Mesh = snowPlane;
        AddChild(_snowSurface);
        _snowSurface.Position = Vector3.Up * SnowHeight;

        // Compute shader setup
        // Image inputImage = _subViewport.GetTexture().GetImage();
        // inputImage.Convert(Image.Format.Rgbaf);
        // _displacementTextureCompute = (GodotObject)GD.Load<GDScript>(DISPLACEMENT_TEXTURE_COMPUTE_PATH).New();
        // _displacementTextureCompute.Call("init", inputImage, DefaultDisplacementMap);
        
        _deformCamera.GlobalPosition = Vector3.Zero + 2.0f * _deformCamera.Near * Vector3.Down;

        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("feet_altitude", Player.GlobalPosition.Y);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("snow_height", SnowHeight);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("patch_offset", -_deformCamera.Near);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("patch_range", _deformCamera.Far - _deformCamera.Near);
        (SnowMaterial as ShaderMaterial).SetShaderParameter("snow_height", SnowHeight);
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        _deformerMesh.GlobalPosition = Player.GlobalPosition + DEFORMER_RADIUS * Vector3.Up;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("last_texture", _subViewport.GetTexture());
        (_depthFilter.MaterialOverride as ShaderMaterial).SetShaderParameter("feet_altitude", Player.GlobalPosition.Y);
        (SnowMaterial as ShaderMaterial).SetShaderParameter("displacement_tex", _subViewport.GetTexture());
    }

    public ViewportTexture GetTexture()
    {
        return _subViewport.GetTexture();
    }
}
