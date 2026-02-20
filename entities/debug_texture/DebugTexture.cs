using Godot;
using System;

public partial class DebugTexture : Control
{
    [Export] public TerrainDeformer Deformer;

    private TextureRect _textureRect;

    public override void _Ready()
    {
        base._Ready();
        if (!Visible) return;
        _textureRect = GetNode<TextureRect>("%TextureRect");
        CallDeferred(MethodName.AssignTexture);
    }

    private async void AssignTexture()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
        _textureRect.Texture = Deformer.GetDisplacement();
    }
}
