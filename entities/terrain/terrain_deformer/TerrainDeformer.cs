using System.Runtime.CompilerServices;
using Godot;

public partial class TerrainDeformer : Node3D
{
    const float VELOCITY_THRESHOLD = 2.0f;

    [Export] public Player Player;
    [Export] public FootprintStorage FpStorage;
    [Export] public uint RadiusChunks = 2;
    [Export] public float DisplacementMapRange = 64.0f;
    [Export] public float MinSoundHeight = 0.05f;
    [Export] public float GrassThreshold = 0.02f;

    private Terrain _terrain;
    private TexturePainter _painter;
    private bool _leftCarving;
    private bool _rightCarving;

    public override void _Ready()
    {
        base._Ready();
        InitNodes();

        _painter.Params.DownscaleFactor = 1.6f * DisplacementMapRange;
        _painter.InitPool(RadiusChunks, ref FpStorage);
        _painter.Pool.DisplacementMapRange = DisplacementMapRange;

        _terrain = GetTree().GetFirstNodeInGroup("terrain") as Terrain;
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        _painter.Pool.UpdateActiveChunks(new Vector2(Player.GlobalPosition.X, Player.GlobalPosition.Z));
        var deformationCenter = Player.LeftFootPosition / DisplacementMapRange;
        _painter.Params.CenterLeft = new Vector2(0.5f, 0.5f) + deformationCenter;
        float snowHeight = _terrain.GetSnowHeight();
        float snowHeightCl = Mathf.Max(MinSoundHeight, snowHeight);
        

        float carveDepth = 0.0f;
        if (Player.LeftFootHitDistance <= snowHeightCl && !_leftCarving)
        {
            _leftCarving = true;
            Player.PlayFootstep(Player.FootSide.Left, snowHeight <= GrassThreshold);
        }
        else if (Player.LeftFootHitDistance > snowHeightCl)
        {
            _leftCarving = false;
        }
        if (Player.LeftFootHitDistance < 1.0f && Player.Velocity.Length() > 0.1f)
        {
            carveDepth = (snowHeight - Player.LeftFootHitDistance) / snowHeight;
            carveDepth = Mathf.Clamp(carveDepth, 0.0f, 1.0f);
        }
        if (carveDepth != 0)
        {
            FpStorage.SaveEntryLeft(_painter.Params.CenterLeft, carveDepth, -Player.GlobalRotation.Y);
        }
        _painter.Params.DepthLeft = carveDepth;

        deformationCenter = Player.RightFootPosition / DisplacementMapRange;
        _painter.Params.CenterRight = new Vector2(0.5f, 0.5f) + deformationCenter;
        if (Player.RightFootHitDistance <= snowHeightCl && !_rightCarving)
        {
            _rightCarving = true;
            Player.PlayFootstep(Player.FootSide.Right, snowHeight <= GrassThreshold);
        }
        else if (Player.RightFootHitDistance > snowHeightCl)
        {
            _rightCarving = false;
        }
        if (Player.RightFootHitDistance < 1.0f && Player.Velocity.Length() > 0.1f)
        {
            carveDepth = (snowHeight - Player.RightFootHitDistance) / snowHeight;
            carveDepth = Mathf.Clamp(carveDepth, 0.0f, 1.0f);
        }
        if (carveDepth != 0)
        {
            FpStorage.SaveEntryRight(_painter.Params.CenterRight, carveDepth, -Player.GlobalRotation.Y);
        }
        _painter.Params.DepthRight = carveDepth;
        
        _painter.SetAngle(-Player.GlobalRotation.Y);
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