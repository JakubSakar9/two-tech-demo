using System.Runtime.CompilerServices;
using Godot;

public partial class TerrainDeformer : Node3D
{
    const float VELOCITY_THRESHOLD = 2.0f;

    [Export] public Player Player;
    [Export] public uint RadiusChunks = 2;
    [Export] public float DisplacementMapRange = 64.0f;
    [Export] public float SnowHeight = 0.1f;

    private TexturePainter _painter;

    public override void _Ready()
    {
        base._Ready();
        InitNodes();

        _painter.Params.DownscaleFactor = 1.6f * DisplacementMapRange;
        _painter.InitPool(RadiusChunks);
        _painter.Pool.DisplacementMapRange = DisplacementMapRange;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        _painter.Pool.UpdateActiveChunks(new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z));
        var deformationCenter = Player.LeftFootPosition / DisplacementMapRange;
        _painter.Params.CenterLeft = new Vector2(0.5f, 0.5f) + deformationCenter;
        float carveDepth = 0.0f;
        if (Player.LeftFootHitDistance < 1.0f && Player.Velocity.Length() > 0.1f)
        {
            carveDepth = (SnowHeight - Player.LeftFootHitDistance) / SnowHeight;
            carveDepth = Mathf.Clamp(carveDepth, 0.0f, 1.0f);
        }
        _painter.Params.DepthLeft = carveDepth;

        deformationCenter = Player.RightFootPosition / DisplacementMapRange;
        _painter.Params.CenterRight = new Vector2(0.5f, 0.5f) + deformationCenter;
        if (Player.RightFootHitDistance < 1.0f && Player.Velocity.Length() > 0.1f)
        {
            carveDepth = (SnowHeight - Player.RightFootHitDistance) / SnowHeight;
            carveDepth = Mathf.Clamp(carveDepth, 0.0f, 1.0f);
        }
        _painter.Params.DepthRight = carveDepth;
        _painter.SetAngle(-Player.GlobalRotation.Y);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public ref readonly Texture2Drd GetDisplacement()
    {
        return ref _painter.Pool.GetCurrentTexture();
    }

    public Texture2Drd[] GetDisplacementTextures()
    {
        Texture2Drd[] textures = new Texture2Drd[_painter.Pool.NChunks];
        for (uint i = 0; i < _painter.Pool.NChunks; i++)
        {
            textures[i] = _painter.Pool.GetTextureAtIdx(i);
        }
        return textures;
    }

    private void InitNodes()
    {
        _painter = GetNode<TexturePainter>("%TexturePainter");
    }
}