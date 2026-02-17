using Godot;
using System;

public partial class DebugTexture : Control
{
    public override void _Input(InputEvent @event)
    {
        base._Input(@event);
        // if (@event is InputEventKey keyEvent)
        // {
        //     if (keyEvent.Keycode == Key.Q && keyEvent.IsPressed())
        //     {
        //         Visible = !Visible;
        //     }
        // }
    }

    public override void _Ready()
    {
        base._Ready();
    }

    public override void _Draw()
    {
        base._Draw();
    }
}
