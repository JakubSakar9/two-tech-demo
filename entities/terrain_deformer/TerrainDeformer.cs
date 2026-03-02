using Godot;

public partial class TerrainDeformer : Node3D
{
    const float VELOCITY_THRESHOLD = 2.0f;

    [Export] public Player Player;
    [Export] public float DisplacementMapRange = 64.0f;
    [Export] public float SnowHeight = 0.1f;

    private TexturePainter _painter;

    public override void _Ready()
    {
        base._Ready();
        InitNodes();

        _painter.Params.DownscaleFactor = 1.2f * DisplacementMapRange;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        var deformationCenter = Player.RightFootPosition / DisplacementMapRange;
        _painter.Params.SpriteCenter = new Vector2(0.5f, 0.5f) + deformationCenter;
        _painter.SetAngle(-Player.GlobalRotation.Y);
        float carveDepth = 0.0f;
        if (Player.RightFootHitDistance < 1.0f && Player.Velocity.Length() > 0.1f)
        {
            carveDepth = (SnowHeight - Player.RightFootHitDistance) / SnowHeight;
            carveDepth = Mathf.Clamp(carveDepth, 0.0f, 1.0f);
        }
        _painter.Params.CarveDepth = carveDepth;
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
    }

    public ref Texture2Drd GetDisplacement()
    {
        return ref _painter.DisplacementTexture;
    }

    private void InitNodes()
    {
        _painter = GetNode<TexturePainter>("%TexturePainter");
    }
}