using Godot;
using System;

public partial class LoadingCamera : Camera3D
{
    private Label _loadingLabel;

    public override void _Ready()
    {
        base._Ready();
        _loadingLabel = GetNode<Label>("%LoadingLabel");
    }

    public void HideText()
    {
        _loadingLabel.Hide();
    }
}
