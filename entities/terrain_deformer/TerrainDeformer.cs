using Godot;

public partial class TerrainDeformer : Node3D
{
    const float VELOCITY_THRESHOLD = 2.0f;

    [Export] public Player Player;
    [Export] public float DisplacementMapRange = 64.0f;
    [Export] public float StepInterval = 0.35f;

    private Timer _stepTimer;
    private TexturePainter _painter;

    public override void _Ready()
    {
        base._Ready();
        InitNodes();

        _stepTimer.WaitTime = StepInterval;
        _painter.Params.DownscaleFactor = 0.8f * DisplacementMapRange;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
    }

    public override void _Process(double delta)
    {
        base._Process(delta);

        var deformationCenter = new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z) / DisplacementMapRange;
		_painter.Params.SpriteCenter = new Vector2(0.5f, 0.5f) + deformationCenter;
        _painter.SetAngle(-Player.GlobalRotation.Y);
		if (_stepTimer.IsStopped())
		{
            _stepTimer.Start();
            if (Player.Velocity.Length() < 2.0f) return;
			_painter.Params.CarveDepth = 1.0f;
			_painter.FlipSprite();
		}
    }

    public ref Texture2Drd GetDisplacement()
    {
        return ref _painter.DisplacementTexture;
    }

    private void InitNodes()
    {
        _stepTimer = GetNode<Timer>("%StepTimer");
        _painter = GetNode<TexturePainter>("%TexturePainter");
    }
}